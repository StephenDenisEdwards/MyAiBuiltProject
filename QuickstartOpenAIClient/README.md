# Quickstart OpenAI MCP Client

This project is a console MCP client that connects an OpenAI model to any MCP server you launch via stdio. It discovers the server’s tools and exposes them to the model using `Microsoft.Extensions.AI` function invocation, so the model can call MCP tools during the conversation.

## What it does
- Starts an MCP server process from a single argument you pass (one of: a `.py` script, a `.js` script, or a directory/`.csproj` for `dotnet run`).
- Connects to the MCP server over stdio using `ModelContextProtocol.Client`.
- Lists and registers the server’s tools with the OpenAI chat client.
- Runs a streaming REPL where your prompts can trigger tool calls (for example, the sample `SampleMcpServer` exposes `RandomNumberTools` and `TodoTools`).

## Key files and libraries
- `Program.cs`: console entry point; starts the MCP server, registers tools, and runs a streaming chat loop.
- `QuickstartOpenAIClient.csproj`: includes `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI` (provider), `OpenAI`, and `ModelContextProtocol` packages.
- Uses `OpenAI.OpenAIClient(...).GetChatClient(model).AsIChatClient()` and then `.AsBuilder().UseFunctionInvocation().Build()` to enable tool/function invocation.

## Prerequisites
- .NET9 SDK
- OpenAI API key
- An MCP server to run (for example, the `SampleMcpServer` in this repo)

## Configure secrets
- Environment variables (recommended):
 - `OPENAI_API_KEY` – your OpenAI API key
 - `OPENAI_MODEL` – model id (for example, `gpt-4o-mini`)
- Or user secrets (from the `QuickstartOpenAIClient` project directory):
 - `dotnet user-secrets init`
 - `dotnet user-secrets set OPENAI_API_KEY "sk-..."`
 - `dotnet user-secrets set OPENAI_MODEL "gpt-4o-mini"`
- Alternate key names also supported by `Program.cs`:
 - `OpenAIKey` (API key), `ModelName` (model id)

## Run it
- Visual Studio: select the `QuickstartOpenAIClient - SampleMcpServer` launch profile and run.
- CLI (from the solution root), pass the path to the MCP server you want to run:
 - `dotnet run --project QuickstartOpenAIClient/QuickstartOpenAIClient.csproj ..\SampleMcpServer\SampleMcpServer.csproj`
 - `dotnet run --project QuickstartOpenAIClient/QuickstartOpenAIClient.csproj path\to\server.js`
 - `dotnet run --project QuickstartOpenAIClient/QuickstartOpenAIClient.csproj path\to\server.py`

## Use the REPL
- Type natural prompts. The model may call exposed MCP tools and stream the response.
- Examples:
 - "Give me3 random numbers between10 and20."
 - "Create a todo: title=Write tests, notes=end-to-end"
 - "Run the TestTodos prompt and summarize results."
- Type `exit` to quit.

## Troubleshooting
- Unsupported server argument: ensure you pass a `.py`, `.js`, or a directory/`.csproj` path.
- No tools listed: verify the MCP server starts, uses stdio, and exposes tools.
- Auth errors or401: ensure `OPENAI_API_KEY` (or `OpenAIKey`) is set.
- Model not found: set a valid `OPENAI_MODEL` (or `ModelName`) matching your OpenAI account access.
- Rate limit (429): reduce frequency or change model/tier.

## Customization
- Change the model: update the `OPENAI_MODEL` secret/env var or edit `Program.cs` default.
- Add middleware/telemetry: wrap the `IChatClient` with `.AsBuilder().UseOpenTelemetry().UseRateLimiting()...`.
- Extend MCP usage: point to any MCP server that speaks stdio; the client will auto-discover and register its tools.




