using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Tm2020Mcp.EditorBridge;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(_ =>
{
    var bridgeUrl = Environment.GetEnvironmentVariable("TM2020_BRIDGE_URL")
        ?? "http://127.0.0.1:29100";

    return new OpenPlanetClient(new HttpClient { Timeout = TimeSpan.FromSeconds(5) }, bridgeUrl);
});

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "tm2020-mcp",
            Version = "0.1.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
