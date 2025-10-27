# Model Context Protocol (MCP) and Agents in this project

This repository contains a minimal MCP server implemented in .NET9 and C#13 that communicates over stdio and exposes Tools, Resources, and Prompts. It is intended to be connected to an MCP-capable client such as GitHub Copilot in Visual Studio17.14.17, which can register MCP servers and surface their capabilities in chat and in the MCP menus.

## What MCP is
- MCP (Model Context Protocol) is an open protocol that lets AI clients connect to external capabilities via pluggable servers.
- Roles:
 - Client: an AI application (e.g., Copilot, Claude Desktop, Continue) that talks MCP.
 - Server: a host process (this app) that exposes capabilities for the client to use.
- Transport: JSON-RPC2.0 over stdio using Content-Length framing (no sockets required).
- Why: secure, local-first extension of AI clients with least privilege and explicit capability discovery.

## Core MCP capabilities
- Tools: callable functions. Methods: `tools/list`, `tools/call`.
- Resources: discoverable data sources you can preview/insert. Methods: `resources/list`, `resources/read`.
- Prompts: reusable prompt templates with typed arguments. Methods: `prompts/list`, `prompts/get`.

## Protocol flow (simplified)
- Client starts the server command and speaks JSON-RPC over stdio.
- `initialize` announces protocol and what capability groups are supported.
- The client queries/list/uses capabilities (`tools/*`, `resources/*`, `prompts/*`).
- Optional `ping` for health checks.

## What this server implements
- Initialize: returns `protocolVersion` "2024-11-05", `serverInfo` name `MyAiBuiltProject.McpServer`, and capabilities for Tools, Resources, Prompts.
- Tool: `echo`
 - Input schema: `{ message: string }`.
 - Call via `tools/call` and it returns a text content part with the same message.
- Resource: `resource://hello.txt`
 - `resources/list` exposes a single text resource.
 - `resources/read` returns "Hello from MCP resource!" as text content.
- Prompt: `greet`
 - Argument: `name` (required).
 - `prompts/get` returns a user message instructing to greet the given name.

## Using with GitHub Copilot in Visual Studio17.14.17
1) Publish the server
- Framework-dependent: `dotnet publish -c Release`
 - Command: `dotnet`
 - Arguments: `C:\path\to\publish\MyAiBuiltProject.dll`
- Self-contained: `dotnet publish -c Release -r win-x64 --self-contained true`
 - Command: `C:\path\to\publish\MyAiBuiltProject.exe`

2) Register in Copilot MCP Servers
- Open the MCP Servers list in Visual Studio.
- Add Server:
 - Name: `MyAiBuiltProject`
 - Transport: stdio
 - Command/Arguments: from publish step above
 - Working directory: optional (publish folder)
 - Environment variables: none required

3) Use from Copilot
- MCP Resources: you should see `hello.txt`. Insert it into chat to include the resource content.
- MCP Prompts: you should see `greet`. Run it and provide `name`.
- Tools: ask Copilot to call `echo` with a `message`, or the client may call it during an agent run.

## How MCP relates to Agents
- Agent mode is a client behavior (planning/executing multi-step tasks). MCP is a protocol for connecting external capabilities.
- They are complementary: Agent mode can discover and call MCP Tools, preview/insert MCP Resources, and use MCP Prompts as building blocks while executing a plan.
- Agent mode does not require MCP, but MCP expands what the agent can do without baking those integrations into the client.

## Troubleshooting
- If the client cannot connect, run the published command in a terminal to verify the server stays running.
- This server logs exceptions to stderr. Check the Copilot server connection details or run it in a console to view logs.
- If Tools/Resources/Prompts do not appear, ensure you published the right build and that `initialize` advertises the corresponding capabilities (already done here).

## Extending this server
- Add Tools: implement new cases in `tools/list` and `tools/call` for your functions.
- Add Resources: extend `resources/list` to enumerate data (files, APIs) and `resources/read` to stream content.
- Add Prompts: add entries in `prompts/list` and compose messages in `prompts/get`.
- You can wrap external systems (e.g., Azure OpenAI, REST APIs, databases) behind Tools/Resources/Prompts.

## Security notes
- Stdio transport runs under your user account. Keep least privilege: expose only what the client needs.
- Validate inputs in `tools/call` and avoid executing untrusted commands.
- If you expose filesystem resources, restrict to safe directories and sanitize paths.

