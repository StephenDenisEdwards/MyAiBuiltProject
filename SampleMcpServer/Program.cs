using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SampleMcpServer.Prompts;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio), the tools, and the prompts to register.
builder.Services
 .AddMcpServer()
 .WithStdioServerTransport()
 .WithTools<RandomNumberTools>()
 .WithTools<SampleMcpServer.Tools.TodoTools>()
 .WithPrompts<SampleMcpServer.Prompts.PromptDefinitions>();

await builder.Build().RunAsync();
