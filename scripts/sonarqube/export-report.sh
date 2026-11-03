#!/usr/bin/env bash
# Exporta os resultados da análise SonarQube do katch-backend para ficheiros JSON e um resumo em Markdown (I066).
# A instância da pipeline é efémera: estes ficheiros, publicados como artefacto, são o registo da análise.
#
# Variáveis: SONAR_HOST_URL (por omissão http://localhost:9000), SONAR_TOKEN, SONAR_PROJECT_KEY (por omissão
# katch-backend) e SONAR_REPORT_DIR (por omissão sonarqube-report).
set -euo pipefail

HOST="${SONAR_HOST_URL:-http://localhost:9000}"
TOKEN="${SONAR_TOKEN:?Defina SONAR_TOKEN.}"
PROJECT="${SONAR_PROJECT_KEY:-katch-backend}"
OUT="${SONAR_REPORT_DIR:-sonarqube-report}"
mkdir -p "${OUT}"

get() {
  curl -fsS -u "${TOKEN}:" "${HOST}$1"
}

metrics="ncloc,files,coverage,line_coverage,duplicated_lines_density,software_quality_reliability_rating,software_quality_security_rating,software_quality_maintainability_rating,software_quality_reliability_issues,software_quality_security_issues,software_quality_maintainability_issues,security_hotspots,sqale_debt_ratio"

get "/api/system/status" > "${OUT}/instancia.json"
get "/api/qualitygates/project_status?projectKey=${PROJECT}" > "${OUT}/quality-gate.json"
get "/api/measures/component?component=${PROJECT}&metricKeys=${metrics}" > "${OUT}/medidas.json"
get "/api/qualityprofiles/search?project=${PROJECT}" > "${OUT}/quality-profiles.json"

# Problemas em aberto (todas as páginas de 500).
page=1
echo '[]' > "${OUT}/problemas.json"
while :; do
  chunk="$(get "/api/issues/search?components=${PROJECT}&resolved=false&ps=500&p=${page}")"
  jq -s '.[0] + .[1].issues' "${OUT}/problemas.json" <(echo "${chunk}") > "${OUT}/problemas.tmp"
  mv "${OUT}/problemas.tmp" "${OUT}/problemas.json"
  total="$(echo "${chunk}" | jq '.paging.total')"
  [[ $((page * 500)) -ge "${total}" ]] && break
  page=$((page + 1))
done
get "/api/hotspots/search?project=${PROJECT}&ps=500" > "${OUT}/hotspots.json"

rating() {
  case "${1%%.*}" in 1) echo A ;; 2) echo B ;; 3) echo C ;; 4) echo D ;; 5) echo E ;; *) echo "${1}" ;; esac
}
measure() {
  jq -r --arg m "$1" '.component.measures[] | select(.metric == $m) | .value' "${OUT}/medidas.json"
}

{
  echo "## Análise SonarQube (QG-05)"
  echo
  echo "| Item | Valor |"
  echo "| --- | --- |"
  echo "| Versão da instância | $(jq -r '.version' "${OUT}/instancia.json") |"
  echo "| Quality Profile (C#) | $(jq -r '[.profiles[] | select(.language == "cs") | .name] | join(", ")' "${OUT}/quality-profiles.json") |"
  echo "| Estado do Quality Gate | **$(jq -r '.projectStatus.status' "${OUT}/quality-gate.json")** |"
  echo "| Linhas de código | $(measure ncloc) |"
  echo "| Fiabilidade | $(rating "$(measure software_quality_reliability_rating)") ($(measure software_quality_reliability_issues | jq -r '.total // .' 2>/dev/null || echo '?') problemas) |"
  echo "| Segurança | $(rating "$(measure software_quality_security_rating)") |"
  echo "| Manutenção | $(rating "$(measure software_quality_maintainability_rating)") (dívida técnica $(measure sqale_debt_ratio)%) |"
  echo "| Duplicação | $(measure duplicated_lines_density)% |"
  echo "| Cobertura de linhas (âmbito do QG-04) | $(measure line_coverage)% |"
  echo "| Security hotspots | $(jq -r '.paging.total' "${OUT}/hotspots.json") |"
  echo "| Problemas em aberto | $(jq 'length' "${OUT}/problemas.json") |"
  echo
  echo "| Condição | Valor | Limite | Estado |"
  echo "| --- | --- | --- | --- |"
  jq -r '.projectStatus.conditions[] | "| \(.metricKey) \(.comparator) | \(.actualValue) | \(.errorThreshold) | \(.status) |"' \
    "${OUT}/quality-gate.json"
} > "${OUT}/resumo.md"

cat "${OUT}/resumo.md"
