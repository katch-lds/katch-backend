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
9. [Regras a respeitar](#9-regras-a-respeitar)

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
| PostgreSQL | 17 ou superior | O serviço aparece em "Serviços" do Windows |
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

Corre bem quando aparece `Applying migration '...'` para cada migration e termina com `Done.` Pode ver
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
Test summary: total: 97; failed: 0; succeeded: 97; skipped: 0
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

## 9. Regras a respeitar

- **Nunca** pôr palavras-passe ou cadeias de ligação reais neste README nem em ficheiros versionados
  (`appsettings.json`, `.env`, etc.). A configuração local vai na variável de ambiente.
- Para guardar o seu setup pessoal (porto, utilizador, notas), use um ficheiro `LOCAL-NOTAS.md`, que está no
  `.gitignore` e nunca é enviado para o repositório.
- Dados de teste são sempre fictícios; é proibido importar dados reais de utilizadores.
- Não fazer push direto para `main`: todas as alterações entram por Pull Request aprovado e com a pipeline verde.
- Antes de acrescentar funcionalidades ou tabelas, confirmar que estão no âmbito aprovado (F001 a F011).
