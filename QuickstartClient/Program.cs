using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
	.AddEnvironmentVariables()
	.AddUserSecrets<Program>();

var (command, arguments) = GetCommandAndArguments(args);

var clientTransport = new StdioClientTransport(new()
{
	Name = "Demo Server",
	Command = command,
	Arguments = arguments,
});

await using var mcpClient = await McpClient.CreateAsync(clientTransport);

var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
	Console.WriteLine($"Connected to server with tools: {tool.Name}");
}

static (string command, string[] arguments) GetCommandAndArguments(string[] args)
{
	return args switch
	{
		[var script] when script.EndsWith(".py") => ("python", args),
		[var script] when script.EndsWith(".js") => ("node", args),
		[var script] when Directory.Exists(script) || (File.Exists(script) && script.EndsWith(".csproj")) => ("dotnet", ["run", "--project", script, "--no-build"]),
		_ => throw new NotSupportedException("An unsupported server script was provided. Supported scripts are .py, .js, or .csproj")
	};
}