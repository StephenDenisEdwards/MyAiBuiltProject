# MCP Server Overview

This project contains a minimal Model Context Protocol (MCP) server implemented with .NET9 and C#13. It communicates with clients over stdio using JSON-RPC2.0 and exposes Tools, Resources, and Prompts that GitHub Copilot (Visual Studio17.14.17) can discover and use.

What this server provides
- Transport: JSON-RPC2.0 over stdio with Content-Length framing.
- Methods implemented:
 - `ping` – health check.
 - `initialize` – announces protocol version and capabilities.
 - `tools/list`, `tools/call` – one tool: `echo`.
 - `resources/list`, `resources/read` – one sample text resource: `resource://hello.txt`.
 - `prompts/list`, `prompts/get` – one prompt: `greet(name)`.
- Capabilities in `initialize.capabilities`:
 - `tools`, `resources`, `prompts`.

Tool, Resource, and Prompt details
- Tool `echo`:
 - Input schema: `{ message: string }`.
 - Result: returns a content array with one text item echoing the message.
- Resource `resource://hello.txt`:
 - `resources/list` exposes the URI and metadata.
 - `resources/read` returns a text content "Hello from MCP resource!".
- Prompt `greet`:
 - Arguments: `name` (required).
 - `prompts/get` returns a user message asking to greet the name.

Build and publish
1) Restore and build as usual from Visual Studio or CLI.
2) Publish your preferred deployment:
- Framework-dependent (requires a system .NET runtime):
 ```bash
 dotnet publish -c Release
 ```
 - Command: `dotnet`
 - Arguments: `<publish-folder>\MyAiBuiltProject.dll`
- Self-contained (single OS/runtime, no system .NET required):
 ```bash
 dotnet publish -c Release -r win-x64 --self-contained true
 ```
 - Command: `<publish-folder>\MyAiBuiltProject.exe`

Register with Visual Studio Copilot (MCP Servers)
1) Open the MCP Servers list in Visual Studio (Copilot settings).
2) Add server:
- Name: `MyAiBuiltProject`
- Transport: stdio
- Command / Arguments:
 - Framework-dependent: `dotnet` with args pointing to the published `.dll`.
 - Self-contained: the path to the published `.exe` (no args required).
- Working directory: optional (set to the publish folder if you prefer).
- Environment variables: none required.
3) Save and connect. Copilot should show the server as connected.

Use in Visual Studio Copilot
- Tools: ask Copilot to call the `echo` tool, e.g., "Use tool echo with message: hello".
- MCP Resources menu: you should see `hello.txt`; insert it to include the resource content in chat.
- MCP Prompts menu: you should see `greet`; run it and provide `name`.
- Agent mode: if enabled, Copilot can autonomously call Tools/Resources/Prompts exposed by this server during its plan.

Troubleshooting
- Connection fails immediately:
 - Run the published command in a terminal to ensure the server starts and stays running.
 - Ensure you pointed to the publish output, not an intermediate Debug build.
- No tools/resources/prompts appear:
 - Confirm `initialize` advertises capabilities (this project does).
 - Check stderr logs (the server writes exceptions to stderr).
- Access denied or path issues:
 - Verify the Visual Studio process has permission to execute the command and read the publish folder.

Update or remove
- Update: re-publish, then update the command path in the MCP Server entry if the publish location changed.
- Remove: delete the entry from the MCP Servers list in Visual Studio.

Security considerations
- The server runs with your user privileges. Expose only minimal functionality and validate inputs.
- If you extend Resources to the filesystem or networks, restrict scope and sanitize inputs.

Extending the server
- Add more tools by extending `tools/list` and `tools/call`.
- Add more resources by extending `resources/list` and implementing `resources/read` for each URI.
- Add more prompts via `prompts/list` and compose messages in `prompts/get`.
- You can wrap external services (e.g., Azure OpenAI, REST APIs, databases) behind tools/resources/prompts.
