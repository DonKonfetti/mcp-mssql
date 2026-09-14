# MCP SQL Server Tool

<!-- mcp-name: io.github.alyiox/mcp-mssql -->

[![Build Status](https://github.com/alyiox/mcp-mssql/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/alyiox/mcp-mssql/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/Alyio.McpMssql.svg)](https://www.nuget.org/packages/Alyio.McpMssql)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

A read-only-by-default [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server for Microsoft SQL Server. Enables AI agents to discover schemas, execute parameterized SELECT queries, analyze execution plans, and optionally execute write operations (DDL/DML).

Query tools enforce SELECT-only statements (no DML/DDL mutations). An optional `run_command` tool can execute arbitrary write T-SQL, but only on profiles that explicitly opt in via `AllowWrite` configuration (disabled by default for safety).

**Requirements:** .NET 8.0+ runtime (targets `net8.0` and `net10.0`), SQL Server instance, and a connection string. Building from source requires .NET 10.0 SDK.

## Quick Start

Set the `MCPMSSQL_CONNECTION_STRING` environment variable and choose a deployment method:

### Option 1: Run from NuGet package with MCP Inspector

```bash
export MCPMSSQL_CONNECTION_STRING="Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
npx -y @modelcontextprotocol/inspector@latest dotnet dnx Alyio.McpMssql --prerelease
```

### Option 2: Install as a global .NET tool

```bash
dotnet tool install --global Alyio.McpMssql --prerelease
export MCPMSSQL_CONNECTION_STRING="Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
npx -y @modelcontextprotocol/inspector@latest mcp-mssql
```

### Option 3: Run from source

```bash
export MCPMSSQL_CONNECTION_STRING="Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
npx -y @modelcontextprotocol/inspector@latest dotnet run --project src/Alyio.McpMssql -f net10.0
```

Use `--prerelease` flag for pre-release builds.

## Configuration

All settings use the **MCPMSSQL** prefix. Flat environment variables (e.g., `MCPMSSQL_CONNECTION_STRING`) configure the default profile for single-connection setups. For multiple connections, use a configuration file.

### Single Connection via Environment Variables

```bash
# Connection string (required)
export MCPMSSQL_CONNECTION_STRING="Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"

# Description for the default profile (optional, used for tooling/AI discovery)
export MCPMSSQL_DESCRIPTION="Primary connection"

# Max rows per interactive query (default: 500, hard ceiling: 1000)
export MCPMSSQL_QUERY_MAX_ROWS="500"

# Query timeout in seconds (default: 30)
export MCPMSSQL_QUERY_COMMAND_TIMEOUT_SECONDS="60"

# Max rows for snapshot queries (default: 10000, hard ceiling: 50000)
export MCPMSSQL_QUERY_SNAPSHOT_MAX_ROWS="10000"

# Snapshot query timeout in seconds (default: 120)
export MCPMSSQL_QUERY_SNAPSHOT_COMMAND_TIMEOUT_SECONDS="120"

# Execution plan analysis timeout in seconds (default: 300, hard ceiling: 600)
export MCPMSSQL_ANALYZE_COMMAND_TIMEOUT_SECONDS="300"

# Enable write commands via run_command (default: false, soft guard only)
# For hard read-only guarantee, connect with a db_datareader login
export MCPMSSQL_ALLOW_WRITE="false"

# Write command timeout in seconds (default: 60, hard ceiling: 600)
export MCPMSSQL_WRITE_COMMAND_TIMEOUT_SECONDS="60"
```

### Multiple Connections via Configuration File

Use the user-scoped `appsettings.json` file (recommended for multiple profiles). Environment variables also work via .NET host conventions (e.g., `MCPMSSQL__PROFILES__<NAME>__CONNECTIONSTRING`).

**File locations:**
- Unix-like: `~/.config/mcp-mssql/appsettings.json`
- Windows: `%USERPROFILE%\.config\mcp-mssql\appsettings.json`

**Example configuration:**

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

Configuration values exceeding hard ceilings are automatically clamped at startup with warnings logged to stderr.

### Local Development with Secrets

Store sensitive connection strings in .NET user-secrets:

```bash
dotnet user-secrets set "MCPMSSQL_CONNECTION_STRING" "Server=localhost,1433;..." --project src/Alyio.McpMssql
npx -y @modelcontextprotocol/inspector -e DOTNET_ENVIRONMENT=Development dotnet run --project src/Alyio.McpMssql
```

### Azure SQL / Microsoft Entra ID

This server uses [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient), which supports Microsoft Entra (Azure AD) authentication. Provide connection strings using Entra credentials or managed identities as documented by SqlClient.

## Tools and Resources

All tools accept an optional `profile` parameter; when omitted, the default profile is used.

### Available Tools

| Tool | Description | Key Parameters |
|---|---|---|
| **`list_profiles`** | List all configured connection profiles. | — |
| **`get_object`** | Retrieve metadata for a table/view (columns, indexes, constraints, relationships) or routine definition. Accepts names like `Users`, `dbo.Users`, or `[dbo].[Users]`. | `name`, `kind` (relation/routine), `includes` (columns, indexes, constraints, relationships, definition) |
| **`run_query`** | Execute a read-only SELECT query with parameterized binding. Returns results as CSV inline (up to limit) or as a snapshot resource URI. | `sql`, `params`, `snapshot`, `profile` |
| **`analyze_query`** | Analyze a SELECT query's execution plan without fetching results. Returns compact JSON with cost, operators, cardinality, warnings, missing indexes, waits, and stats. Full XML plan available via resource URI. | `sql`, `params`, `profile` |
| **`run_command`** | Execute write T-SQL (DDL/DML). Rejected unless `AllowWrite=true` for the target profile. Caller manages transactions. | `sql`, `params`, `profile` |

### Available Resources

| URI Template | Description |
|---|---|
| `mssql://profiles` | List configured connection profiles (same as `list_profiles` tool). |
| `mssql://plans/{id}` | Retrieve full XML execution plan by ID from `analyze_query`. Plans expire after 7 days. |
| `mssql://snapshots/{id}` | Retrieve full query results as CSV by ID from `run_query` with `snapshot=true`. Results expire after 1 day. |

## Security

### Read-Only Query Enforcement

Query tools (`run_query`, `analyze_query`) enforce strict SELECT-only semantics using SQL ScriptDom parsing. The SQL must be exactly one `SELECT` statement in a single batch — not merely text starting with `SELECT`. Multi-statement batches and `GO` separators are rejected.

**Rejected patterns:**

| Pattern | Reason |
|---|---|
| `SELECT ... INTO` | Creates a new table (DDL). |
| `SELECT @v = ...` | Mutates session state via variable assignment. |
| `NEXT VALUE FOR` | Advances sequences. |
| `OPENQUERY`, `OPENDATASOURCE`, `OPENROWSET(BULK ...)` | Ad-hoc external data source access. |
| `UPDLOCK`, `XLOCK`, `TABLOCK`, `TABLOCKX`, `HOLDLOCK`, `SERIALIZABLE`, `REPEATABLEREAD` | Acquire locks that impede concurrent writers. |

**Allowed hints:** Concurrency-safe hints like `NOLOCK`, `ROWLOCK`, and `READPAST` are permitted.

**Input limits:** SQL longer than 64 KB or nested more than 100 parentheses deep is rejected.

### Parameterized Queries

All query parameters use named `@paramName` binding to prevent SQL injection. Provide connection strings and credentials via environment variables, configuration files, or .NET user-secrets — never hardcode them.

### Write Operations (Opt-In)

The `run_command` tool is rejected by default. Enable it only by setting `AllowWrite: true` in a profile's configuration.

**Important:** `AllowWrite` is a soft, application-level guard, **not a security boundary**. It constrains this server's behavior, not database permissions. For a genuine read-only guarantee, connect with a database login restricted to `db_datareader` role.

## MCP Host Configuration Examples

Snippets for popular MCP clients. Replace the connection string with your own and ensure `dotnet` is on your `PATH`. The `env` block is optional if the connection string is already configured via `appsettings.json`.

### Cursor

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

### Gemini

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

### Codex

```toml
[mcp_servers.mssql]
command = "dotnet"
args = ["dnx", "Alyio.McpMssql", "--prerelease", "--yes"]
[mcp_servers.mssql.env]
MCPMSSQL_CONNECTION_STRING = "Server=127.0.0.1;User ID=sa;Password=<YourStrong@Passw0rd>;Encrypt=True;TrustServerCertificate=True;"
```

### Open Code

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

### Claude Code

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

### GitHub Copilot

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

## Testing

### Integration Tests

Integration tests use a real SQL Server instance and expect a database named **`McpMssqlTest`**. Configure the test connection string via .NET user-secrets:

```bash
dotnet user-secrets set "MCPMSSQL_CONNECTION_STRING" \
  "Server=localhost,1433;User ID=sa;Password=...;TrustServerCertificate=True;Encrypt=True;Initial Catalog=McpMssqlTest;" \
  --project test/Alyio.McpMssql.Tests
```

Run tests for a single framework:

```bash
dotnet test --framework net8.0
dotnet test --framework net10.0
```

**Note:** The test fixtures drop and recreate the shared `McpMssqlTest` database on each initialization. This is safe within a single test process but requires sequential framework execution in CI.

## Comparison: This vs. Data API Builder

| Aspect | MCP SQL Server | Data API Builder |
|---|---|---|
| **Purpose** | Lightweight MCP server for AI agents | Full REST/GraphQL CRUD API |
| **Transport** | Standard input/output (stdio) | HTTP/REST or GraphQL |
| **Query Support** | Parameterized SELECT only | CRUD, relationships, subscriptions |
| **Authentication** | Database login via connection string | API-level auth (Azure AD, JWT, etc.) |
| **Use Case** | Agent-driven schema discovery and analytics | Public/internal APIs, data applications |

Choose this project for agent-based SQL analysis with minimal surface area; choose Data API Builder for production APIs.

## Roadmap

### MCP Tasks Extension

Support for [MCP Tasks extension (SEP-2663)](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/2663) is planned. Snapshot queries and execution-plan analysis currently run under long timeouts (120 s and 300 s, respectively) but would benefit from Tasks' structured long-running operation model.

Tasks is an opt-in extension (`io.modelcontextprotocol/tasks`) that a server uses only when the client declares support in its per-request capabilities. Adoption remains the key blocker.

## JSON Schema Compatibility

Nullable members emit JSON Schema union types — `"type": ["string", "null"]` — matching `System.Text.Json`'s behavior for `string?` and similar nullable types. This is valid in JSON Schema 2020-12.

**Note:** This server never serializes `null` values; absent members are omitted from responses. No nullable member appears in a `required` list, so clients can safely ignore the null branch when needed.

## Contributing

We welcome issues and pull requests. Please follow the existing code style and add tests for new features.

## License

MIT. See [LICENSE](LICENSE).
