# Scripts da base de dados

Scripts SQL para criar, repor e preencher a base de dados `katch` em PostgreSQL 18 de forma reproduzível
(CCA `OF-M3-003`; modelo de dados v02, secções 5 e 9). Todos os dados são fictícios (P18; RI, secção 14.3).

| Ficheiro | O que faz | Origem |
| --- | --- | --- |
| `00-reset-schema.sql` | Repõe a base: reverte todas as migrations e apaga as tabelas, os tipos enumerados e os dados | Gerado a partir das migrations |
| `01-create-schema.sql` | Cria o esquema completo (17 tabelas, tipos enumerados, restrições e índices) e a conta de Administrador; idempotente | Gerado a partir das migrations |
| `02-seed-reference-data.sql` | Listas pré-definidas: 30 localidades com coordenadas, 30 competências e 12 benefícios; idempotente | Escrito à mão |
| `03-seed-demo-data.sql` | Dados de demonstração: 20 Candidatos ativos, 7 Recrutadores, 5 Empresas aprovadas, 2 pendentes, 50 vagas publicadas, respostas a vagas, 3 matches com conversa e o registo de autoria; idempotente | Escrito à mão |
| `04-verify.sql` | Conta as tabelas e os registos de cada tabela e confirma o P18, a ausência da tabela `notification` e que não há palavras-passe em claro; só lê | Escrito à mão |

## Palavras-passe

Nenhuma palavra-passe fica em claro no repositório (RNF003; RI, secção 14.2):

- A conta de Administrador (`admin@katch.pt`) é criada pela migration `AddAdmin`. Ao gerar o `01-create-schema.sql`,
  a palavra-passe é lida de `Katch__AdminPassword` e só o resumo fica no script.
- As contas de demonstração (`candidato01@demo.katch.pt` a `candidato20@demo.katch.pt` e
  `recrutador1@demo.katch.pt` a `recrutador7@demo.katch.pt`) partilham uma palavra-passe de demonstração. O script
  guarda só os resumos, no formato do ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100 000 iterações), com sal próprio
  por conta. A palavra-passe de demonstração consta das instruções de execução arquivadas na pasta do projeto
  (`04.13-scripts-de-base-de-dados`), fora do repositório.

## Executar numa base vazia

Precisa do `psql`, que vem com o PostgreSQL (no Windows, normalmente em `C:\Program Files\PostgreSQL\18\bin`).
Na raiz do repositório, no PowerShell:

```powershell
$env:PGHOST = "localhost"
$env:PGPORT = "<porto>"            # ver o README principal, secção 3.2
$env:PGUSER = "postgres"
$env:PGPASSWORD = "<palavra-passe do postgres>"

createdb katch                      # só se a base ainda não existir
psql -d katch -v ON_ERROR_STOP=1 -f database/01-create-schema.sql
psql -d katch -v ON_ERROR_STOP=1 -f database/02-seed-reference-data.sql
psql -d katch -v ON_ERROR_STOP=1 -f database/03-seed-demo-data.sql
psql -d katch -f database/04-verify.sql
```

Resultado esperado do `04-verify.sql`: 17 tabelas do modelo, `existe_notification` igual a `f`, 20 Candidatos ativos,
5 Empresas aprovadas, 2 pendentes, 50 vagas publicadas, 1 Administrador e 0 resumos fora do formato.

Os scripts 01 a 03 podem ser executados de novo sem erros nem linhas duplicadas.

## Locale da base

A base tem de usar a codificação `UTF8` e uma locale UTF-8 (por exemplo, a predefinida do PostgreSQL no Windows ou
`en_US.utf8` na imagem `postgres:18` da CI), e não a locale `C`. O CHECK `ck_candidate_custom_skill_label` usa a classe
`[[:alnum:]]`, que depende da locale: com a locale `C`, recusa as letras acentuadas que o P07 admite (por exemplo,
«Gestão de equipas»). A locale de uma base existente consulta-se com `SHOW lc_ctype;`.

## Repor a base

```powershell
psql -d katch -v ON_ERROR_STOP=1 -f database/00-reset-schema.sql
```

Depois, volte a executar os scripts 01 a 03. Repor apaga todos os dados: use só na base local, nunca numa base
partilhada. O `00-reset-schema.sql` exige uma base criada pelas migrations (com a tabela `__EFMigrationsHistory`).

## Gerar de novo os scripts 00 e 01

Os scripts 00 e 01 têm de ser gerados de novo sempre que for acrescentada uma migration. Na raiz do repositório:

```powershell
dotnet tool restore
$env:Katch__AdminPassword = "<palavra-passe de demonstração>"
dotnet ef migrations script --idempotent --project src/Katch.Backend -o database/01-create-schema.sql
dotnet ef migrations script <ÚltimaMigration> 0 --idempotent --project src/Katch.Backend -o database/00-reset-schema.sql
```

`<ÚltimaMigration>` é o nome da migration mais recente (por exemplo, `AddMessageAndOperationLog`). Não use a sua
palavra-passe pessoal em `Katch__AdminPassword`: o resumo fica no script.

A pipeline CI (`database-scripts`) executa os scripts sobre PostgreSQL 18 numa base vazia em cada Merge Request.
