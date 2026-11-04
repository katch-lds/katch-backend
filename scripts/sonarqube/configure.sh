#!/usr/bin/env bash
# Configura uma instância SonarQube Community Build acabada de arrancar, para a análise do katch-backend (I066; QG-05).
#
# 1. Espera que a instância fique disponível (UP).
# 2. Substitui a palavra-passe inicial do administrador pela de SONAR_ADMIN_PASSWORD.
# 3. Cria o Quality Gate «Katch QG-05» (código total, só com as condições do grupo) e define-o como predefinido.
# 4. Define o Quality Profile «Sonar way» como predefinido de C#, se existir, e confirma-o.
# 5. Gera um token e escreve-o em SONAR_TOKEN (GITHUB_ENV na pipeline, ou no ficheiro indicado).
#
# Variáveis:
#   SONAR_HOST_URL        endereço da instância (por omissão http://localhost:9000)
#   SONAR_ADMIN_PASSWORD  palavra-passe nova do administrador (secret do GitHub; localmente, à escolha)
#   SONAR_TOKEN_FILE      ficheiro onde escrever o token, se não houver GITHUB_ENV (por omissão .sonar-token)
#
# O script é idempotente: pode ser executado de novo sobre a mesma instância.
set -euo pipefail

HOST="${SONAR_HOST_URL:-http://localhost:9000}"
PASSWORD="${SONAR_ADMIN_PASSWORD:?Defina SONAR_ADMIN_PASSWORD (secret do GitHub ou variável local).}"
GATE="Katch QG-05"

if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
  echo "::add-mask::${PASSWORD}"
fi

echo "A aguardar a instância SonarQube em ${HOST}..."
for attempt in $(seq 1 90); do
  status="$(curl -fsS "${HOST}/api/system/status" 2>/dev/null | jq -r '.status' 2>/dev/null || true)"
  if [[ "${status}" == "UP" ]]; then
    echo "Instância disponível (tentativa ${attempt})."
    break
  fi
  if [[ "${attempt}" == "90" ]]; then
    echo "A instância SonarQube não ficou disponível (último estado: ${status:-sem resposta})." >&2
    exit 1
  fi
  sleep 5
done

# Palavra-passe do administrador: na primeira execução é a inicial (admin); depois, já é a do secret.
if curl -fsS -o /dev/null -u "admin:admin" "${HOST}/api/authentication/validate" \
  && [[ "$(curl -fsS -u "admin:admin" "${HOST}/api/authentication/validate" | jq -r '.valid')" == "true" ]]; then
  curl -fsS -X POST -u "admin:admin" "${HOST}/api/users/change_password" \
    --data-urlencode "login=admin" \
    --data-urlencode "previousPassword=admin" \
    --data-urlencode "password=${PASSWORD}" > /dev/null
  echo "Palavra-passe inicial do administrador substituída."
fi

auth=(-u "admin:${PASSWORD}")
if [[ "$(curl -fsS "${auth[@]}" "${HOST}/api/authentication/validate" | jq -r '.valid')" != "true" ]]; then
  echo "A palavra-passe do administrador não é válida nesta instância." >&2
  exit 1
fi

api() {
  local method="$1"
  local path="$2"
  shift 2
  curl -fsS -X "${method}" "${auth[@]}" "${HOST}${path}" "$@"
}

# Quality Gate do grupo (QG-05), sobre o código total: a instância da pipeline é nova em cada execução e, numa
# primeira análise, as condições sobre o código novo do «Sonar way» não se aplicam. As classificações usam as métricas
# do modo MQR (Multi-Quality Rule), o modo predefinido das instalações novas: 1 = A, 2 = B, 3 = C, 4 = D, 5 = E
# (B = pelo menos um problema de impacto baixo).
declare -A CONDITIONS=(
  [software_quality_reliability_rating]="GT 2"
  [software_quality_security_rating]="GT 1"
  [software_quality_maintainability_rating]="GT 1"
  [duplicated_lines_density]="GT 3"
)
gate_uri="$(jq -rn --arg v "${GATE}" '$v|@uri')"
if ! api GET "/api/qualitygates/show?name=${gate_uri}" > /dev/null 2>&1; then
  api POST /api/qualitygates/create --data-urlencode "name=${GATE}" > /dev/null
  echo "Quality Gate «${GATE}» criado."
fi

# Um Quality Gate novo vem com as condições de código novo do «Sonar way»: ficam só as do grupo, com os seus limites.
current="$(api GET "/api/qualitygates/show?name=${gate_uri}")"
while read -r id metric op error; do
  [[ -z "${id}" ]] && continue
  if [[ "${CONDITIONS[${metric}]:-}" != "${op} ${error}" ]]; then
    api POST /api/qualitygates/delete_condition --data-urlencode "id=${id}" > /dev/null
  fi
done < <(echo "${current}" | jq -r '.conditions[] | "\(.id) \(.metric) \(.op) \(.error)"')

current="$(api GET "/api/qualitygates/show?name=${gate_uri}")"
for metric in "${!CONDITIONS[@]}"; do
  read -r op error <<< "${CONDITIONS[${metric}]}"
  if ! echo "${current}" | jq -e --arg m "${metric}" '.conditions[] | select(.metric == $m)' > /dev/null; then
    api POST /api/qualitygates/create_condition \
      --data-urlencode "gateName=${GATE}" \
      --data-urlencode "metric=${metric}" \
      --data-urlencode "op=${op}" \
      --data-urlencode "error=${error}" > /dev/null
  fi
done

api POST /api/qualitygates/set_as_default --data-urlencode "name=${GATE}" > /dev/null
echo "Quality Gate predefinido «${GATE}»:"
api GET "/api/qualitygates/show?name=${gate_uri}" | jq -r '.conditions[] | "  \(.metric) \(.op) \(.error)"'

# Quality Profile «Sonar way» para C# (convenções de código e quality gates, secção 5). Nas versões recentes o perfil
# predefinido pode ter outro nome («Sonar way core»); se o «Sonar way» existir, passa a ser o predefinido.
profiles="$(api GET "/api/qualityprofiles/search?language=cs")"
echo "Quality Profiles de C# na instância: $(echo "${profiles}" | jq -r '[.profiles[] | .name + (if .isDefault then " (predefinido)" else "" end)] | join(", ")')"
if echo "${profiles}" | jq -e '.profiles[] | select(.name == "Sonar way")' > /dev/null; then
  api POST /api/qualityprofiles/set_default --data-urlencode "language=cs" --data-urlencode "qualityProfile=Sonar way" \
    > /dev/null
fi
profile="$(api GET "/api/qualityprofiles/search?language=cs&defaults=true" | jq -r '.profiles[0].name')"
echo "Quality Profile predefinido de C#: ${profile}"
if [[ "${profile}" != Sonar\ way* ]]; then
  echo "O Quality Profile predefinido de C# devia ser o «Sonar way»." >&2
  exit 1
fi

# Token renovado em cada execução. É um token de utilizador do administrador da instância temporária, e não só de
# análise, porque o export-report.sh também lê o Quality Gate, as medidas e os problemas (permissão de consulta).
token_name="katch-backend-$(date +%s)"
token="$(api POST /api/user_tokens/generate \
  --data-urlencode "name=${token_name}" \
  --data-urlencode "type=USER_TOKEN" | jq -r '.token')"

if [[ -n "${GITHUB_ENV:-}" ]]; then
  echo "::add-mask::${token}"
  echo "SONAR_TOKEN=${token}" >> "${GITHUB_ENV}"
  echo "Token gerado e guardado em SONAR_TOKEN (mascarado)."
else
  file="${SONAR_TOKEN_FILE:-.sonar-token}"
  umask 077
  printf '%s' "${token}" > "${file}"
  echo "Token gerado e guardado em ${file} (fora do controlo de versões)."
fi
