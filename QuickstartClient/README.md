# Quickstart Anthropic MCP Client

This project is a console MCP client that connects an Anthropic model to any MCP server you launch via stdio. It discovers the server’s tools and exposes them to the model using Microsoft.Extensions.AI function invocation, so the model can call MCP tools during the conversation.

What it does
- Starts an MCP server process from a single argument you pass (one of: a `.py` script, a `.js` script, or a directory/`.csproj` for `dotnet run`).
- Connects to the MCP server over stdio using `ModelContextProtocol.Client`.
- Lists and registers the server’s tools with the Anthropic chat client.
- Runs a streaming REPL where your prompts can trigger tool calls (for example, the sample `SampleMcpServer` exposes `RandomNumberTools` and `TodoTools`).

Key files and libraries
- `Program.cs`: main console entry point; starts the MCP server, registers tools, and runs a streaming chat loop.
- Libraries: `Anthropic.SDK`, `Microsoft.Extensions.AI`, `ModelContextProtocol.Client`.
- Model: defaults to `claude-sonnet-4-5-20250929` via `ChatOptions.ModelId`.

Prerequisites
- .NET9 SDK
- Anthropic API key
- An MCP server to run (for example, the `SampleMcpServer` in this repo)

Configure secrets
- Environment variable: `ANTHROPIC_API_KEY`
- Or user secrets (from this project directory):
 - `dotnet user-secrets init`
 - `dotnet user-secrets set ANTHROPIC_API_KEY "your-key"`

Run it
- With the included launch profile (Visual Studio): select profile `QuickstartClient - SampleMcpServer` and run.
- Or CLI (from the solution root), providing the MCP server you want to run:
 - `dotnet run --project QuickstartClient/QuickstartAnthropicClient.csproj ..\SampleMcpServer\SampleMcpServer.csproj`
 - `dotnet run --project QuickstartClient/QuickstartAnthropicClient.csproj path\to\server.js`
 - `dotnet run --project QuickstartClient/QuickstartAnthropicClient.csproj path\to\server.py`

Use the REPL
- Type natural prompts. The model may call exposed MCP tools and stream the response.
- Examples:
 - "Give me3 random numbers between10 and20."
 - "Create a todo: title=Write tests, notes=end-to-end"
 - "Run the TestTodos prompt and summarize results."
- Type `exit` to quit.

Troubleshooting
- "An unsupported server script was provided": pass a `.py`, `.js`, or a directory/`.csproj` path.
- No tools appear: verify the MCP server starts and exposes tools via stdio.
- Auth errors: ensure `ANTHROPIC_API_KEY` is set (env var or user secrets).

Customization
- Change the model: edit `Program.cs` to set a different `ChatOptions.ModelId`.
- Add middleware: the Anthropic client is built with `Microsoft.Extensions.AI` and can be extended via the builder pipeline.
