// MIT License

using System.Reflection;
using Alyio.McpMssql.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

var serverAssembly = Assembly.GetExecutingAssembly();
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

builder.Configuration.Sources.Clear();

builder.Configuration.AddJsonFile(userConfigPath, optional: true, reloadOnChange: false);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile(
            $"appsettings.{builder.Environment.EnvironmentName}.json",
            optional: true,
            reloadOnChange: false)
        .AddUserSecrets(
            System.Reflection.Assembly.GetExecutingAssembly(),
            optional: true,
            reloadOnChange: false);
}

builder.Configuration
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services
    .AddMcpMssql(builder.Configuration)
    .AddMcpServer(options => options.ServerInfo = new Implementation
    {
        // Name and Version are read from the assembly so the csproj stays
        // their single source. Only Title is stated here: it is a display
        // string the build carries no equivalent of.
        Name = serverAssembly.GetName().Name!,
        Title = "Microsoft SQL Server",
        // SourceLink appends "+<commit sha>" to the informational version;
        // trim it so this reports what the csproj and server.json state.
        Version = (serverAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? serverAssembly.GetName().Version!.ToString())
            .Split('+')[0],
    })
    .WithStdioServerTransport()
    .WithMcpMssqlTools(McpJsonDefaults.Options)
    .WithResourcesFromAssembly();

await builder.Build().RunAsync();

