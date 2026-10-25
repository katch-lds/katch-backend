# Katch Backend

API em ASP.NET Core (.NET 10) com EF Core e PostgreSQL. Projeto Katch, LDS 2026/2027, grupo 11.

Este documento explica como pôr o backend a funcionar numa máquina Windows, da primeira vez e no dia a dia.
Os comandos são para o **PowerShell** (o prompt começa por `PS`). Se vir `C:\...>` sem `PS`, está no cmd; abra o
PowerShell (menu Iniciar, "Windows PowerShell") ou escreva `powershell` no terminal.

## Índice

1. [Estrutura do repositório](#1-estrutura-do-repositório)
2. [O que instalar](#2-o-que-instalar)
3. [Primeira vez, passo a passo](#3-primeira-vez-passo-a-passo)
4. [No dia a dia](#4-no-dia-a-dia)
5. [Testes](#5-testes)
6. [Migrations](#6-migrations)
7. [Resumo dos comandos](#7-resumo-dos-comandos)
8. [Erros comuns](#8-erros-comuns)
9. [Autenticação JWT](#9-autenticação-jwt)
10. [Regras a respeitar](#10-regras-a-respeitar)
11. [Quota de interesses e tarefa periódica](#11-quota-de-interesses-e-tarefa-periódica)
12. [Recusa, interesse e quota do Candidato](#12-recusa-interesse-e-quota-do-candidato)

## 1. Estrutura do repositório

| Pasta ou ficheiro | O que é |
| --- | --- |
| `src/Katch.Backend/` | A API: controllers, serviços, repositórios, entidades e migrations |
| `tests/Katch.Backend.UnitTests/` | Testes unitários |
| `tests/Katch.Backend.IntegrationTests/` | Testes de integração (usam SQLite em memória) |
| `Katch.Backend.sln` | Solução .NET que junta os três projetos |
| `global.json` | Versão mínima do SDK do .NET |
| `.config/dotnet-tools.json` | Ferramentas locais (`dotnet-ef`, para as migrations) |

## 2. O que instalar

| Ferramenta | Versão | Como confirmar |
| --- | --- | --- |
| SDK do .NET | 10.0.401 ou superior | `dotnet --version` mostra um número 10.x |
| PostgreSQL | 18 | O serviço aparece em "Serviços" do Windows |
| pgAdmin | qualquer | Vem com o instalador do PostgreSQL (opcional, mas usado neste guia) |
| Git | qualquer | `git --version` |

Para instalar o SDK: `winget install Microsoft.DotNet.SDK.10`. Depois **feche e abra o terminal**, porque o PATH só
é atualizado em terminais novos. Se `dotnet --version` disser "No .NET SDKs were found", só tem o runtime instalado
e falta o SDK.

## 3. Primeira vez, passo a passo

### 3.1 Criar a base de dados `katch`

No pgAdmin:

1. Ligue-se ao servidor PostgreSQL (se ainda não existir, clique com o botão direito em *Servers*, *Register*,
   *Server*; no separador *Connection* ponha Host `localhost`, o porto, o utilizador `postgres` e a palavra-passe
   definida na instalação).
2. Expanda o servidor, clique com o botão direito em **Databases**, *Create*, *Database…*.
3. Em *Database* escreva `katch` e clique em **Save**.

A base fica vazia. As tabelas são criadas no passo 3.5.

### 3.2 Descobrir o porto do PostgreSQL

O porto por omissão é **5432**, mas se houver mais do que uma versão instalada (por exemplo a 17 e a 18), a segunda
fica noutro porto (por exemplo **5433**). Para confirmar: no pgAdmin, clique com o botão direito no servidor,
*Properties*, separador *Connection*, campo *Port*. É este o valor a usar no passo seguinte.

### 3.3 Definir a variável de ambiente `ConnectionStrings__Katch`

A aplicação lê a ligação à base de dados desta variável (note os **dois sublinhados**). Sem ela, a API não arranca e
os comandos de migrations falham. A palavra-passe nunca vai para o repositório.

```powershell
$env:ConnectionStrings__Katch = "Host=localhost;Port=<porto>;Database=katch;Username=postgres;Password=<palavra-passe>"
```

Substitua `<porto>` e `<palavra-passe>` pelos seus valores. Para confirmar que ficou definida:

```powershell
$env:ConnectionStrings__Katch
```

> **A variável só existe na janela onde foi definida.** Se abrir outra janela do PowerShell, tem de a definir outra
> vez. Para a guardar de forma permanente (só para o seu utilizador do Windows), use o comando abaixo e abra depois
> um terminal novo:
>
> ```powershell
> [Environment]::SetEnvironmentVariable("ConnectionStrings__Katch", "Host=localhost;Port=<porto>;Database=katch;Username=postgres;Password=<palavra-passe>", "User")
> ```

No cmd, a sintaxe é outra: `set "ConnectionStrings__Katch=Host=localhost;Port=<porto>;Database=katch;Username=postgres;Password=<palavra-passe>"`.
No PowerShell, `set "..."` não define variáveis de ambiente e `$env:` não funciona no cmd.

**Palavra-passe do Administrador (`Katch__AdminPassword`).** A migration `AddAdmin` cria a conta de Administrador
dos dados iniciais (`admin@katch.pt`, modelo de dados v02, secção 9) e lê a palavra-passe desta variável, que só é
precisa para **aplicar** as migrations. Tem de ter pelo menos 8 caracteres, com uma letra e um algarismo (P05). Na
base de dados fica só o resumo; a palavra-passe nunca vai para o repositório (RI, secção 14.2).

```powershell
$env:Katch__AdminPassword = "<palavra-passe do Administrador>"
```

**Segredo da credencial (`Jwt__Secret`).** A API assina as credenciais de sessão (JWT) com um segredo lido desta
variável e **não arranca** sem ele. Tem de ter pelo menos 32 bytes e nunca vai para o repositório. Mais
pormenores na [secção 9](#9-autenticação-jwt).

```powershell
$env:Jwt__Secret = "<segredo com pelo menos 32 bytes>"
```

### 3.4 Instalar a ferramenta das migrations

Na raiz do repositório (a pasta onde está o `Katch.Backend.sln`):

```powershell
dotnet tool restore
```

Deve terminar com `Restore was successful.` Só é preciso uma vez por clone.

### 3.5 Criar as tabelas (aplicar as migrations)

```powershell
dotnet ef database update --project src/Katch.Backend
```

Exige as variáveis `ConnectionStrings__Katch` e `Katch__AdminPassword` (secção 3.3). Corre bem quando aparece
`Applying migration '...'` para cada migration e termina com `Done.` Pode ver
"Failed executing DbCommand ... `__EFMigrationsHistory`" na primeira vez: é esperado, porque a tabela de histórico
ainda não existia e foi criada nesse momento.

Para ver o resultado, no pgAdmin faça *Refresh* em *katch*, *Schemas*, *public*, *Tables*.

### 3.6 Arrancar a API

```powershell
dotnet run --project src/Katch.Backend --launch-profile http
```

Corre bem quando aparece:

```text
Now listening on: http://localhost:5180
Application started. Press Ctrl+C to shut down.
```

Abra `http://localhost:5180/swagger` no browser. Se a lista de endpoints estiver vazia, é porque ainda não há
controllers implementados; o que importa é a API ter arrancado sem erros. Para parar, **Ctrl+C**.

Para usar HTTPS (`https://localhost:7180`), corra uma vez `dotnet dev-certs https --trust` e use
`--launch-profile https`.

## 4. No dia a dia

Sempre que abrir um terminal novo para trabalhar:

```powershell
cd "<caminho para a pasta do repositório>"
$env:ConnectionStrings__Katch = "Host=localhost;Port=<porto>;Database=katch;Username=postgres;Password=<palavra-passe>"
$env:Katch__AdminPassword = "<palavra-passe do Administrador>"   # só para aplicar migrations
$env:Jwt__Secret = "<segredo com pelo menos 32 bytes>"             # obrigatório para a API arrancar (secção 9)
git pull
dotnet ef database update --project src/Katch.Backend    # só é necessário se o git pull trouxe migrations novas
dotnet run --project src/Katch.Backend --launch-profile http
```

Se alguém acrescentou uma migration e não a aplicar, a base fica desatualizada em relação ao código e a API pode
falhar a ler ou gravar dados.

## 5. Testes

```powershell
dotnet test
```

Corre os testes unitários e os de integração. Os de integração usam SQLite em memória (Regulamento Interno,
secção 11.2), por isso **não precisam de PostgreSQL nem da variável `ConnectionStrings__Katch`**.

Corre bem quando termina com `failed: 0`, por exemplo:

```text
Test summary: total: 109; failed: 0; succeeded: 109; skipped: 0
```

## 6. Migrations

| Objetivo | Comando |
| --- | --- |
| Criar uma migration nova a partir das alterações às entidades | `dotnet ef migrations add <Nome> --project src/Katch.Backend` |
| Aplicar todas as migrations | `dotnet ef database update --project src/Katch.Backend` |
| Reverter **todas** (apaga as tabelas) | `dotnet ef database update 0 --project src/Katch.Backend` |
| Voltar a uma migration concreta | `dotnet ef database update <NomeDaMigration> --project src/Katch.Backend` |

- `migrations add` não precisa da variável definida: sem ela usa uma cadeia sem credenciais, suficiente para gerar.
  Aplicar ou reverter exige a variável.
- Aplicar a `AddAdmin` exige também `Katch__AdminPassword`; reverter não. Reverter a `AddAdmin` apaga a tabela
  `admin` e as contas de Administrador.
- Reverter apaga dados. Use só na sua base local, nunca numa base partilhada.
- Para verificar o resultado no pgAdmin (*Query Tool* na base `katch`, uma query de cada vez: selecione-a e carregue
  em F5, porque o pgAdmin só mostra o resultado da última instrução):

```sql
-- CHECK constraints
SELECT conrelid::regclass AS tabela, conname, pg_get_constraintdef(oid) AS definicao
FROM pg_constraint WHERE contype = 'c' AND connamespace = 'public'::regnamespace;

-- Índices
SELECT tablename, indexname, indexdef FROM pg_indexes WHERE schemaname = 'public';
```

## 7. Resumo dos comandos

Executar na raiz do repositório.

| Objetivo | Comando |
| --- | --- |
| Instalar o `dotnet-ef` (uma vez por clone) | `dotnet tool restore` |
| Compilar | `dotnet build` |
| Aplicar migrations | `dotnet ef database update --project src/Katch.Backend` |
| Arrancar a API (HTTP) | `dotnet run --project src/Katch.Backend --launch-profile http` |
| Testes | `dotnet test` |
| Swagger | `http://localhost:5180/swagger` |

## 8. Erros comuns

| Erro ou sintoma | Causa | Solução |
| --- | --- | --- |
| `dotnet --version` falha, ou `No .NET SDKs were found` | Só o runtime está instalado, ou o terminal é anterior à instalação | Instalar o SDK (ver secção 2) e abrir um terminal novo |
| `Falta a cadeia de ligação ao PostgreSQL` ao arrancar | `ConnectionStrings__Katch` não definida nesta janela | Definir a variável (secção 3.3) na mesma janela e voltar a correr |
| `Falta o segredo JWT ou tem menos de 32 bytes` ao arrancar | `Jwt__Secret` não definida nesta janela, ou curta demais | Definir a variável (secção 9) e voltar a correr |
| `Falta a palavra-passe da conta de Administrador` no `dotnet ef database update` | `Katch__AdminPassword` não definida nesta janela | Definir a variável (secção 3.3) e repetir |
| `A palavra-passe do Administrador deve ter pelo menos 8 caracteres` | A variável não cumpre o P05 | Usar pelo menos 8 caracteres, com uma letra e um algarismo |
| `Failed to connect to 127.0.0.1:6000` no `dotnet ef` | Variável não definida; o `dotnet ef` usa um porto por omissão | Definir a variável e repetir |
| `A sintaxe do nome do ficheiro, do nome do diretório ou da etiqueta do volume é incorreta` | Usou `$env:` no cmd | Abrir o PowerShell, ou usar `set "..."` no cmd |
| A variável não fica definida depois de `set "..."` | `set` só funciona no cmd, não no PowerShell | Usar `$env:ConnectionStrings__Katch = "..."` |
| `Failed to connect ... connection refused` ou `connection timeout expired` | Porto errado ou serviço PostgreSQL parado | Confirmar o porto (secção 3.2) e se o serviço está a correr |
| `password authentication failed for user "postgres"` | Palavra-passe ou utilizador errados | Corrigir a variável; a palavra-passe é a definida na instalação do PostgreSQL |
| `database "katch" does not exist` | A base ainda não foi criada | Criá-la no pgAdmin (secção 3.1) |
| pgAdmin: `Either Host name or Service must be specified` | O campo *Host name/address* está vazio | Preencher com `localhost` |
| pgAdmin mostra os índices mas não os CHECK | O pgAdmin só mostra o resultado da última instrução | Selecionar apenas uma query e carregar em F5 |
| A API arranca mas a base está sem tabelas | Faltou aplicar as migrations | `dotnet ef database update --project src/Katch.Backend` |
| Erros de compilação logo a seguir a um `git pull` | O código trazido ainda não compila, ou faltam pacotes | `dotnet restore` e `dotnet build`; se persistir, falar com quem fez o commit |

## 9. Autenticação JWT

A API assina as credenciais JWT (HS256) com um segredo lido da variável de ambiente `Jwt__Secret` (dois
sublinhados; corresponde à configuração `Jwt:Secret`). Tem de ter **pelo menos 32 bytes**. Sem ela, ou com um
segredo mais curto, a API **não arranca** e a mensagem de erro diz como configurar (nunca mostra o valor).
Só o segredo é configurável: o emissor (`katch-backend`), o destinatário (`katch-clients`) e a validade (8 horas) são
fixos no código, conforme a API v02 (secção 3.1).

- **Windows (PowerShell):** gere um valor aleatório e guarde-o para o seu utilizador com
  `setx Jwt__Secret "<segredo com pelo menos 32 bytes>"`. O `setx` só vale em terminais novos; na janela atual
  use também `$env:Jwt__Secret = "<segredo com pelo menos 32 bytes>"`.
- **CI (GitHub Actions):** os testes definem um segredo aleatório em cada execução, por isso não precisam de
  nenhum segredo. Para uma API a correr num ambiente partilhado, o valor vai num *secret* do repositório e
  passa à aplicação como variável de ambiente `Jwt__Secret`.
- **O valor nunca vai para o repositório**: nem no `appsettings.json`, nem neste README, nem em logs ou commits.

### Políticas de autorização e erros

Os controllers usam as constantes de `AuthorizationPolicies`: `Candidate`, `Recruiter`, `CandidateOrRecruiter`,
`Admin` e `ApprovedCompany` (Recrutador cuja Empresa está aprovada); `[Authorize]` sem política aceita qualquer
conta autenticada. **Todos os endpoints exigem autenticação por omissão**: os endpoints anónimos (registo,
início de sessão, lista de localidades) têm de ter `[AllowAnonymous]`. Os erros 401 e 403 têm sempre o corpo `ApiErrorDto`, pela ordem de verificação:

| Ordem | Erro | Quando |
| --- | --- | --- |
| 1 | 401 `ERR-01` | Credencial em falta, mal formada, com assinatura inválida ou expirada, ou conta inexistente |
| 2 | 403 `ERR-03` / `ERR-04` | Conta bloqueada / suspensa (verificada na base em cada pedido) |
| 3 | 403 `ERR-06` | O tipo de conta não cumpre a política |
| 4 | 403 `ERR-07` | Só em `ApprovedCompany`: a Empresa não está aprovada; a mensagem traz o estado atual (pendente, recusada, suspensa ou não registada), lido da base em cada pedido |

## 10. Regras a respeitar

- **Nunca** pôr palavras-passe ou cadeias de ligação reais neste README nem em ficheiros versionados
  (`appsettings.json`, `.env`, etc.). A configuração local vai na variável de ambiente.
- Para guardar o seu setup pessoal (porto, utilizador, notas), use um ficheiro `LOCAL-NOTAS.md`, que está no
  `.gitignore` e nunca é enviado para o repositório.
- Dados de teste são sempre fictícios; é proibido importar dados reais de utilizadores.
- Não fazer push direto para `main`: todas as alterações entram por Pull Request aprovado e com a pipeline verde.
- Antes de acrescentar funcionalidades ou tabelas, confirmar que estão no âmbito aprovado (F001 a F010).

## 11. Quota de interesses e tarefa periódica

O Candidato começa com **10 interesses**. O interesse que esgota a quota inicia um bloqueio de **24 horas contadas desse
instante** (RF020, P11), e não por dia de calendário nem até à meia-noite. Terminado o bloqueio a quota volta
**integralmente** a 10; com menos de 10 interesses consumidos a quota não aumenta com o tempo (RF021). A recusa de
vagas não consome quota. A regra está em `Candidate.ConsumeInterest`, `RestoreQuotaIfDue` e `HasInterestAvailable`;
um interesse durante o bloqueio lança `QuotaExhaustedException`, com o fim do bloqueio e o tempo em falta para o
erro `ERR-31`. O instante vem sempre do relógio do servidor (`IClock`) e nunca do cliente.

A tarefa periódica `QuotaRestoreWorker` repõe as quotas cujo bloqueio terminou a cada `BackgroundJobs:IntervalSeconds`
(variável de ambiente `BackgroundJobs__IntervalSeconds`; **60 segundos por omissão**, de 1 a 86 400). Não é ela que
garante a regra: o interesse (`ConsumeInterest`) e a consulta da quota (`RestoreQuotaIfDue`, a chamar pelo serviço
dos endpoints de swipe, I057) verificam o fim do bloqueio no próprio pedido.
Um valor inválido impede a API de arrancar, com uma mensagem a dizer como corrigir; uma execução que falhe é
registada e repetida no intervalo seguinte. Se um interesse e a reposição gravarem ao mesmo tempo, a marca de
concorrência `xmin` faz falhar o segundo, que repete com os valores atuais.

## 12. Recusa, interesse e quota do Candidato

Três endpoints do `JobExplorationController` (`api/candidate/jobs`, API v02, secções 6.3.4 a 6.3.6), todos com a política
`Candidate` (secção 9): uma conta de outro tipo recebe `ERR-06`. O Candidato é sempre o da claim `sub` da credencial,
nunca o do pedido, e o instante vem sempre do relógio do servidor (`IClock`).

| Endpoint | Sucesso | O que faz |
| --- | --- | --- |
| `POST /api/candidate/jobs/{jobId}/decline` | `204` sem corpo | Regista a recusa (`DECLINED_BY_CANDIDATE`) com a data. Não consome quota e não tem limite de recusas. |
| `POST /api/candidate/jobs/{jobId}/interest` | `200` com `QuotaDto` | Numa só gravação, cria o `match` em `WAITING` e consome um interesse da quota. O décimo interesse traz `blockedUntil`, 24 horas depois. |
| `GET /api/candidate/jobs/quota` | `200` com `QuotaDto` | Devolve a quota. Se o bloqueio já terminou, repõe-na e grava antes de responder (RF021). |

O `QuotaDto` tem `available` (0 a 10), `total` (10) e `blockedUntil` (`null` fora do bloqueio):
`{ "available": 9, "total": 10, "blockedUntil": null }`.

### Ordem das verificações

Na recusa e no interesse, a primeira que falhar decide o erro e nada fica registado:

1. A vaga não existe, está eliminada ou nunca foi publicada: `404` `ERR-20`.
2. O Candidato já respondeu a esta vaga (recusa ou interesse): `409` `ERR-32`. A violação do índice único
   `uq_match_candidate_job`, por dois pedidos simultâneos, dá o mesmo erro.
3. A vaga já não está disponível (não publicada, suspensa, encerrada, expirada ou com a Empresa não aprovada):
   `409` `ERR-33`. É só a parte de estado do RF014; a distância, o salário e as preferências são do cartão de vaga.
4. Só no interesse, a quota: esgotada e dentro do bloqueio é `409` `ERR-31`.

A concorrência usa o `xmin` de `candidate`: se vários pedidos do mesmo Candidato chegam ao mesmo tempo, só um grava de
cada vez; os que perdem descartam o contexto, esperam entre 2 e 25 ms (valor aleatório, para não colidirem outra vez) e
repetem tudo desde a leitura (vaga, resposta existente e quota), enquanto houver conflito e **até 5 segundos no total**.
Cada repetição lê a quota já atualizada pelos outros, por isso 20 interesses simultâneos com a quota a 10 dão 10 × `200` e
10 × `ERR-31`, nunca `ERR-41`. O `ERR-41` só é devolvido se o conflito durar mais de 5 segundos, como último recurso. O
mesmo mecanismo serve a consulta da quota quando repõe e grava.

### Erros

| Erro | HTTP | Mensagem | Quando |
| --- | --- | --- | --- |
| `ERR-01` | 401 | A sessão expirou ou não é válida. Inicie sessão novamente. | Sem credencial, ou inválida ou expirada |
| `ERR-06` | 403 | A operação não está disponível para este tipo de conta. | Conta que não é de Candidato |
| `ERR-10` | 400 | Os dados enviados não cumprem a validação automática. | `jobId` que não é um `Guid` |
| `ERR-20` | 404 | O elemento pedido não existe ou não está disponível. | Vaga inexistente, eliminada ou nunca publicada |
| `ERR-31` | 409 | Esgotou a quota de interesses. Pode voltar a manifestar interesse dentro de {h} h {min} min. | Interesse com a quota esgotada; `blockedUntil` vem no corpo e os minutos são arredondados para cima (mínimo 1) |
| `ERR-32` | 409 | Já respondeu a esta vaga. | Segunda resposta à mesma vaga |
| `ERR-33` | 409 | A vaga já não está disponível. | A vaga deixou de cumprir o estado do RF014 |
| `ERR-41` | 409 | O elemento foi alterado por outro pedido. Atualize e tente novamente. | Conflito de concorrência que dura mais de 5 segundos (último recurso) |

### Como experimentar

Neste momento o Swagger (`/swagger`, em `Development`) ainda não tem o botão **Authorize** (vem com a I054), por isso
os pedidos fazem-se com `curl.exe`, enviando a credencial no cabeçalho `Authorization: Bearer`. Precisa da API a
correr (secção 3.6), de uma credencial de Candidato (`<credencial>`; o endpoint de início de sessão ainda não existe neste
ramo) e do identificador de uma vaga publicada de uma Empresa aprovada (`<jobId>`). Nunca guarde credenciais em
ficheiros versionados. No PowerShell use `curl.exe`, porque `curl` é um alias de outro comando.

```powershell
$api = "http://localhost:5180"
$h = "Authorization: Bearer <credencial>"

curl.exe -i -H $h "$api/api/candidate/jobs/quota"
curl.exe -i -X POST -H $h "$api/api/candidate/jobs/<jobId>/interest"
curl.exe -i -X POST -H $h "$api/api/candidate/jobs/<jobId>/decline"
```

Exemplos de pedido e de resposta (cabeçalhos omitidos; os identificadores e as datas são fictícios):

```http
POST /api/candidate/jobs/5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a805/decline
HTTP/1.1 204 No Content

POST /api/candidate/jobs/5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a805/interest
HTTP/1.1 200 OK
{"available":9,"total":10,"blockedUntil":null}

GET /api/candidate/jobs/quota        (depois do décimo interesse)
HTTP/1.1 200 OK
{"available":0,"total":10,"blockedUntil":"2026-10-26T12:00:00Z"}

POST /api/candidate/jobs/5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a805/interest      (ERR-31)
HTTP/1.1 409 Conflict
{"code":"ERR-31","message":"Esgotou a quota de interesses. Pode voltar a manifestar interesse dentro de 23 h 47 min.","fields":[],"blockedUntil":"2026-10-26T12:00:00Z"}

POST /api/candidate/jobs/<jobId já respondida>/interest                      (ERR-32)
HTTP/1.1 409 Conflict
{"code":"ERR-32","message":"Já respondeu a esta vaga.","fields":[],"blockedUntil":null}

POST /api/candidate/jobs/<vaga suspensa>/interest                            (ERR-33)
HTTP/1.1 409 Conflict
{"code":"ERR-33","message":"A vaga já não está disponível.","fields":[],"blockedUntil":null}

POST /api/candidate/jobs/<Guid inexistente>/interest                         (ERR-20)
HTTP/1.1 404 Not Found
{"code":"ERR-20","message":"O elemento pedido não existe ou não está disponível.","fields":[],"blockedUntil":null}

POST /api/candidate/jobs/nao-e-um-guid/interest                              (ERR-10)
HTTP/1.1 400 Bad Request
{"code":"ERR-10","message":"Os dados enviados não cumprem a validação automática.","fields":[],"blockedUntil":null}

GET /api/candidate/jobs/quota        (sem credencial)                        (ERR-01)
HTTP/1.1 401 Unauthorized
{"code":"ERR-01","message":"A sessão expirou ou não é válida. Inicie sessão novamente.","fields":[],"blockedUntil":null}

GET /api/candidate/jobs/quota        (credencial de Recrutador)              (ERR-06)
HTTP/1.1 403 Forbidden
{"code":"ERR-06","message":"A operação não está disponível para este tipo de conta.","fields":[],"blockedUntil":null}

POST /api/candidate/jobs/<jobId>/interest       (conflito de concorrência durante mais de 5 s)   (ERR-41)
HTTP/1.1 409 Conflict
{"code":"ERR-41","message":"O elemento foi alterado por outro pedido. Atualize e tente novamente.","fields":[],"blockedUntil":null}
```

Os testes estão em `JobExplorationServiceTests` (unitários, com a concorrência e o índice único) e em
`JobExplorationTests` (integração, sobre SQLite).
