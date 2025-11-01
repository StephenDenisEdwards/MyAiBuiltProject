using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using OpenAI;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
 .AddEnvironmentVariables()
 .AddUserSecrets<Program>();

var (command, arguments) = GetCommandAndArguments(args);

// Start MCP server over stdio
var clientTransport = new StdioClientTransport(new()
{
	Name = "Demo Server",
	Command = command,
	Arguments = arguments,
});

await using var mcpClient = await McpClient.CreateAsync(clientTransport);

// Discover tools from the MCP server
var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
	Console.WriteLine($"Connected to server with tools: {tool.Name}");
}

// Build an OpenAI chat client with function invocation enabled
var apiKey = builder.Configuration["OPENAI_API_KEY"] ?? builder.Configuration["OpenAIKey"];
var modelId = builder.Configuration["OPENAI_MODEL"] ?? builder.Configuration["ModelName"] ?? "gpt-4o-mini";

IChatClient baseClient = new OpenAIClient(apiKey)
	.GetChatClient(modelId)
	.AsIChatClient();

var chat = baseClient
	.AsBuilder()
	.UseFunctionInvocation()
	.Build();

var options = new ChatOptions
{
	MaxOutputTokens = 1000,
	Tools = [.. tools]
};

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("MCP Client Started!");
Console.ResetColor();

PromptForInput();

while (Console.ReadLine() is string query && !"exit".Equals(query, StringComparison.OrdinalIgnoreCase))
{
	if (string.IsNullOrWhiteSpace(query))
	{
		PromptForInput();
		continue;
	}

	await foreach (var message in chat.GetStreamingResponseAsync(query, options))
	{
		Console.Write(message);
	}

	Console.WriteLine();

	PromptForInput();
}

static void PromptForInput()
{
	Console.WriteLine("Enter a command (or 'exit' to quit):");
	Console.ForegroundColor = ConsoleColor.Cyan;
	Console.Write("> ");
	Console.ResetColor();
}

static (string command, string[] arguments) GetCommandAndArguments(string[] args)
{
	return args switch
	{
		[var script] when script.EndsWith(".py") => ("python", args),
		[var script] when script.EndsWith(".js") => ("node", args),
		[var script] when Directory.Exists(script) || (File.Exists(script) && script.EndsWith(".csproj")) => ("dotnet",
			["run", "--project", script, "--no-build"]),
		_ => throw new NotSupportedException(
			"An unsupported server script was provided. Supported scripts are .py, .js, or .csproj")
	};
}
