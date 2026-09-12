// MIT License

using Alyio.McpMssql.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
var userConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config",
    "mcp-mssql",
    "appsettings.json");

// IMPORTANT: MCP stdio transport uses stdout for protocol messages.
// Send logs to stderr to avoid corrupting the JSON-RPC stream.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

McpConfiguration.Configure(builder, userConfigPath, args);

builder.Services
    .AddMcpMssql(builder.Configuration)
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(serializerOptions: McpJsonDefaults.Options)
    .WithResourcesFromAssembly();

await builder.Build().RunAsync();

