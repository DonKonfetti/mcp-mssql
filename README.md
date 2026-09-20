# MCP SQL Server Tool

<!-- mcp-name: io.github.alyiox/mcp-mssql -->

[![Build Status](https://github.com/alyiox/mcp-mssql/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/alyiox/mcp-mssql/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/Alyio.McpMssql.svg)](https://www.nuget.org/packages/Alyio.McpMssql)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

A read-only-by-default [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server for Microsoft SQL Server with schema discovery, parameterized SELECT **queries**, execution-plan **analysis**, and **opt-in writes** per profile. Profile-based configuration serves **multiple databases and servers** from one toolset deployment.

**Requirements:** .NET 8.0 or later runtime (the tool targets `net8.0` and `net10.0`), SQL Server, and a connection string. Building from source requires the .NET 10.0 SDK.

## Quick start

Set `MCPMSSQL_CONNECTION_STRING` and run the server in one of these ways:

```bash
# Option 1: Run from NuGet package (e.g. with MCP Inspector)
export MCPMSSQL_CONNECTION_STRING="Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
npx -y @modelcontextprotocol/inspector@latest dotnet dnx Alyio.McpMssql --prerelease
```

```bash
# Option 2: Install and run as a global tool
dotnet tool install --global Alyio.McpMssql --prerelease
export MCPMSSQL_CONNECTION_STRING="Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
npx -y @modelcontextprotocol/inspector@latest mcp-mssql
```

```bash
# Option 3: Run from source (clone repo, then)
export MCPMSSQL_CONNECTION_STRING="Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
npx -y @modelcontextprotocol/inspector@latest dotnet run --project src/Alyio.McpMssql -f net10.0
```

## Configuration

A **profile** is one SQL Server connection: a connection string, the row and timeout caps that apply to it, and whether writes are allowed. A profile named `default` always exists; every tool takes an optional `profile` to reach another, and `list_profiles` reports what is configured.

Settings come from three sources, merged field by field, later winning:

1. the user-scoped `appsettings.json` — any number of profiles;
2. `McpMssql__Profiles__<NAME>__<FIELD>` environment variables — any number of profiles;
3. flat `MCPMSSQL_<FIELD>` environment variables — the `default` profile only.

Because the merge is per field rather than per profile, an `appsettings.json` can carry the full set while a flat `MCPMSSQL_CONNECTION_STRING` repoints the default profile at a local server, leaving its other fields intact. There is no fallback connection string: a profile without one — including a `default` that nothing configured — fails startup.

Each setting has one field name, spelled three ways — the JSON path under `McpMssql:Profiles:<NAME>`, that same path with `:` replaced by `__` as an environment variable, or the flat form:

| Field | Flat variable | Default | Hard ceiling |
|---|---|---|---|
| `ConnectionString` | `MCPMSSQL_CONNECTION_STRING` | required | — |
| `Description` | `MCPMSSQL_DESCRIPTION` | none | — |
| `AllowWrite` | `MCPMSSQL_ALLOW_WRITE` | `false` | — |
| `Query:MaxRows` | `MCPMSSQL_QUERY_MAX_ROWS` | 500 | 1 000 |
| `Query:CommandTimeoutSeconds` | `MCPMSSQL_QUERY_COMMAND_TIMEOUT_SECONDS` | 30 | 300 |
| `Query:SnapshotMaxRows` | `MCPMSSQL_QUERY_SNAPSHOT_MAX_ROWS` | 10 000 | 50 000 |
| `Query:SnapshotCommandTimeoutSeconds` | `MCPMSSQL_QUERY_SNAPSHOT_COMMAND_TIMEOUT_SECONDS` | 120 | 300 |
| `Analyze:CommandTimeoutSeconds` | `MCPMSSQL_ANALYZE_COMMAND_TIMEOUT_SECONDS` | 300 | 600 |
| `Write:CommandTimeoutSeconds` | `MCPMSSQL_WRITE_COMMAND_TIMEOUT_SECONDS` | 60 | 600 |

Caps are per profile. A value above its ceiling — or below 1 — is clamped at startup and the adjustment is logged as a warning on stderr; a flat value that is not an integer, or not a boolean for `AllowWrite`, is ignored, leaving whatever the other sources set.

**Single connection:** flat environment variables are the shortest path.

```bash
# Connection string (required).
export MCPMSSQL_CONNECTION_STRING="Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"

# Optional description for the default profile (tooling/AI discovery).
export MCPMSSQL_DESCRIPTION="Primary connection"

# Optional caps, defaults shown.
export MCPMSSQL_QUERY_MAX_ROWS="500"
export MCPMSSQL_QUERY_COMMAND_TIMEOUT_SECONDS="30"
export MCPMSSQL_QUERY_SNAPSHOT_MAX_ROWS="10000"
export MCPMSSQL_QUERY_SNAPSHOT_COMMAND_TIMEOUT_SECONDS="120"
export MCPMSSQL_ANALYZE_COMMAND_TIMEOUT_SECONDS="300"

# Optional write access, off by default; also controls whether run_command
# is advertised at all. A soft guard, not a database permission — prefer a
# db_datareader login for a hard read-only guarantee.
export MCPMSSQL_ALLOW_WRITE="false"
export MCPMSSQL_WRITE_COMMAND_TIMEOUT_SECONDS="60"
```

**Multiple connections:** use the user-scoped `appsettings.json`, which keeps credentials out of the host's process environment.

- Unix-like: `~/.config/mcp-mssql/appsettings.json`
- Windows: `%USERPROFILE%\.config\mcp-mssql\appsettings.json`

```json
{
  "McpMssql": {
    "Profiles": {
      "default": {
        "ConnectionString": "Server=...;User ID=...;Password=...;",
        "Description": "Primary connection",
        "Query": {
          "MaxRows": 500,
          "CommandTimeoutSeconds": 60,
          "SnapshotMaxRows": 10000,
          "SnapshotCommandTimeoutSeconds": 120
        },
        "Analyze": {
          "CommandTimeoutSeconds": 300
        }
      },
      "warehouse": {
        "ConnectionString": "Server=warehouse.example.com;...",
        "Description": "Warehouse read-only"
      },
      "migrations": {
        "ConnectionString": "Server=...;User ID=...;Password=...;",
        "Description": "Write-enabled profile for schema changes",
        "AllowWrite": true,
        "Write": {
          "CommandTimeoutSeconds": 60
        }
      }
    }
  }
}
```

Profile names are case-insensitive, and the structured environment form splits on `__`, so `McpMssql__Profiles__WAREHOUSE__ConnectionString` is profile `warehouse`, field `ConnectionString`. A missing `appsettings.json` is fine — the server starts on whatever sources remain — but one that is not valid JSON fails startup.

**Local development:** store the connection string in user-secrets, then run with `DOTNET_ENVIRONMENT=Development` so secrets and a working-directory `appsettings.json` load as extra sources.

```bash
dotnet user-secrets set "MCPMSSQL_CONNECTION_STRING" "..." --project src/Alyio.McpMssql
npx -y @modelcontextprotocol/inspector -e DOTNET_ENVIRONMENT=Development dotnet run --project src/Alyio.McpMssql
```

**Connection string syntax:** the usual `Server=host,port;Database=db;User ID=...;Password=...;Encrypt=True;` keywords of [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient), which also supports Microsoft Entra (Azure AD) authentication: set `Authentication` to a supported mode (e.g. `Active Directory Default`, `Active Directory Managed Identity`, or `Active Directory Interactive`) when connecting to Azure SQL. See [Connect to Azure SQL with Microsoft Entra authentication and SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-active-directory-authentication) for all modes and details.

## Tools and resources

All tools accept an optional `profile`; when omitted, the default profile is used.

**Tools**

| Tool | Description | Key params |
|---|---|---|
| **`list_profiles`** | List configured connection profiles. Call first when picking a non-default profile. Returns `name`, `description` and `allow_write` per profile. | — |
| **`get_object`** | Get metadata for one relation (columns, indexes, constraints, relationships) or routine (definition). `name` accepts `Users`, `dbo.Users` or `[dbo].[Users]`. `includes` omitted → `columns`. Relations also carry an approximate `row_count`. | `kind`, `name`, `profile`, `catalog`, `schema`, `includes` |
| **`run_query`** | Execute read-only T-SQL SELECT; only SELECT allowed (no DML/DDL). Returns results as CSV in the `data` field (inline) or a snapshot resource URI when `snapshot=true`. Inline limit: 500 rows (hard ceiling 1000). Snapshot limit: 10 000 rows (hard ceiling 50 000). Prefer `analyze_query` for plan tuning. | `sql`, `profile`, `catalog`, `parameters`, `snapshot` |
| **`analyze_query`** | Analyze execution plan for a read-only SELECT. Returns compact JSON summary (cost, operators, cardinality, warnings, `missing_indexes`, waits, stats); no result rows, full XML at `plan_uri`. | `sql`, `profile`, `catalog`, `parameters`, `estimated` |
| **`run_command`** | Execute write T-SQL (DDL/DML). Advertised only when some profile sets `AllowWrite=true` (off by default); still rejected at call time when the target `profile` is locked. Caller manages transactions. Returns `rows_affected` (−1 for DDL) and server `messages`. Marked destructive; intended for human-supervised use. | `sql`, `profile`, `catalog`, `parameters` |

- **`kind`** — `relation` or `routine`.
- **`includes`** — Array of detail sections: `columns`, `indexes`, `constraints`, `relationships` (relations only), `definition` (routines only). `relationships` returns foreign keys in both directions.

Catalog browsing is left to `run_query` over `sys.objects`, `sys.schemas` and `sys.databases`. `get_object` accepts `analyze_query`'s `missing_indexes[].table` as-is.

**Resources**

| URI template | Description |
|---|---|
| `mssql://profiles` | List configured connection profiles, including `allow_write`. Same data as `list_profiles`. |
| `mssql://plans/{id}` | Retrieve full XML execution plan by ID from `analyze_query`; entries expire after 7 days. |
| `mssql://snapshots/{id}` | Retrieve full query result as CSV by ID from `run_query` (snapshot=true); entries expire after 7 days. |

Plans and snapshots are written to disk, under `~/.cache/mcp-mssql/plans/` and `~/.cache/mcp-mssql/snapshots/` (`%USERPROFILE%\.cache\mcp-mssql\` on Windows). Expired files are swept the first time the server touches the store.

## Security

The query tools (`run_query`, `analyze_query`) are read-only (`SELECT` only) and use parameterized `@paramName` binding. Use environment variables, config file or user-secrets for connection strings—never commit secrets.

**What counts as read-only.** The SQL is parsed with ScriptDom and must be exactly one `SELECT` statement in a single batch — not merely text that begins with `SELECT`. Multi-statement and `GO`-separated scripts are rejected, and so are these, despite being syntactically `SELECT`s:

| Rejected | Reason |
|---|---|
| `SELECT ... INTO` | Materializes a new table. |
| `SELECT @v = ...` | Assigns a variable, mutating session state. |
| `NEXT VALUE FOR` | Advances a sequence. |
| `OPENQUERY`, `OPENDATASOURCE`, `OPENROWSET`, `OPENROWSET(BULK ...)` | Reads through an ad-hoc external data source. |
| `UPDLOCK`, `XLOCK`, `TABLOCK`, `TABLOCKX`, `HOLDLOCK`, `SERIALIZABLE`, `REPEATABLEREAD` | Take locks that impede concurrent writers. |

Hints that acquire no extra locks, such as `NOLOCK`, `ROWLOCK` and `READPAST`, stay allowed. Input longer than 64 KB or nested more than 100 parentheses deep is also refused, which keeps the recursive-descent parser clear of a stack overflow.

Like `AllowWrite` below, this constrains what this server will send — it is not a database permission.

**Writes are opt-in, and invisible until then.** The `run_command` tool executes arbitrary T-SQL. Unless at least one configured profile sets `AllowWrite=true` (default `false`), the tool is not registered at all — it never appears in `tools/list`, so a read-only deployment spends no context on it and offers no write surface for an agent to be talked into. Once any profile opts in, the tool is advertised server-wide and still rejects at call time on profiles that remain locked; `list_profiles` reports `allow_write` per profile so an agent can pick a writable one.

`AllowWrite` is a soft, application-level guard, **not** a security boundary — it constrains this server, not the database. For a genuine read-only guarantee, connect with a login restricted to `db_datareader`, and keep write-enabled profiles pointed at credentials scoped to only what they need. `run_command` is marked `destructive` via MCP tool annotations so hosts can gate it behind confirmation, but honor those annotations at the host's discretion.

## MCP host examples

Replace the connection string with your own; ensure `dotnet` is on your PATH. The `env` block is unnecessary when the connection string already comes from `appsettings.json` or the environment.

**Claude Code, Cursor and Gemini** all read the same `mcpServers` shape:

```json
{
  "mcpServers": {
    "mssql": {
      "command": "dotnet",
      "args": ["dnx", "Alyio.McpMssql", "--prerelease", "--yes"],
      "env": {
        "MCPMSSQL_CONNECTION_STRING": "Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
      }
    }
  }
}
```

<details>
<summary>Codex, Open Code and GitHub Copilot</summary>

Codex (TOML):

```toml
[mcp_servers.mssql]
command = "dotnet"
args = ["dnx", "Alyio.McpMssql", "--prerelease", "--yes"]
[mcp_servers.mssql.env]
MCPMSSQL_CONNECTION_STRING = "Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
```

Open Code:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "mssql": {
      "type": "local",
      "enabled": true,
      "command": ["dotnet", "dnx", "Alyio.McpMssql", "--prerelease", "--yes"],
      "environment": {
        "MCPMSSQL_CONNECTION_STRING": "Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
      }
    }
  }
}
```

GitHub Copilot:

```json
{
  "inputs": [],
  "servers": {
    "mssql": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["dnx", "Alyio.McpMssql", "--prerelease", "--yes"],
      "env": {
        "MCPMSSQL_CONNECTION_STRING": "Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
      }
    }
  }
}
```

</details>

## Integration tests

Tests use a real SQL Server and the `default` profile (`MCPMSSQL_CONNECTION_STRING` from environment variables or user-secrets). The suite expects a database named **`McpMssqlTest`**: the connection string must include `Initial Catalog=McpMssqlTest`. The test infrastructure creates, seeds, and drops this database. Set the secret for the test project:

```bash
dotnet user-secrets set "MCPMSSQL_CONNECTION_STRING" \
  "Server=localhost,1433;User ID=sa;Password=...;TrustServerCertificate=True;Encrypt=True;Initial Catalog=McpMssqlTest;" \
  --project test/Alyio.McpMssql.Tests
```

**One framework at a time.** The single `McpMssqlTest` database is shared by every test, and the fixtures drop and recreate it on initialization. Within one test process this is safe — the `SqlServer` collection disables parallelization. Across processes it is not: the test project targets both `net8.0` and `net10.0`, and `dotnet test` runs the two framework modules in parallel, so they race on that one database. There is no cross-process locking, so run a single framework at a time:

```bash
dotnet test --framework net8.0
dotnet test --framework net10.0
```

CI does the same, iterating over `TARGET_FRAMEWORKS` sequentially.

## Why this instead of Data API Builder?

Data API Builder (DAB) is a full REST/GraphQL API with CRUD and auth. This project is a small, read-only MCP server for agents: stdio, parameterized SELECT only, minimal surface. Choose this for agent workflows and low operational overhead; choose DAB for CRUD, REST/GraphQL, and rich policies.

## Roadmap

**MCP Tasks extension ([SEP-2663](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/2663)).** Snapshot queries and execution-plan analysis run under long timeouts (120 s and 300 s by default), which is the shape the [Tasks extension](https://modelcontextprotocol.io/extensions/tasks/overview) exists for: the server returns a durable task handle instead of blocking, and the client polls `tasks/get` until the work reaches a terminal state.

The fit is good; adoption is the blocker. Tasks is an opt-in extension (`io.modelcontextprotocol/tasks`) that a server may only use when the client declares support in its per-request capabilities, and no client currently lists it in the [extension support matrix](https://modelcontextprotocol.io/extensions/client-matrix). Deferred until clients ship support.

## Schema compatibility

Nullable members emit a JSON Schema union type — `"type": ["string", "null"]` — because that is what `System.Text.Json` produces for `string?` and friends. It is legal JSON Schema 2020-12 and permitted by the MCP spec. MCP Inspector warns on the form, on the grounds that some MCP clients read `type` as a single string; whether that rule still has evidence behind it is [under review upstream](https://github.com/modelcontextprotocol/inspector/issues/2286). Rewriting to `anyOf` is not a clear win: OpenAI documents the union form for optional parameters, Anthropic supports `anyOf` and not type arrays, and Cursor, Gemini, and Azure AI Foundry reject `anyOf`.

Nothing is lost by ignoring the null branch. This server never serializes null — absent members are omitted rather than sent as `null` — and no nullable member appears in a `required` list, so a client that reads only the first type in the union gets the exact contract. Left as the SDK emits it; revisit if the SDK changes or the rule settles.

## Contributing

Open issues or PRs; follow existing style and add tests where appropriate.

## License

[MIT](LICENSE)
