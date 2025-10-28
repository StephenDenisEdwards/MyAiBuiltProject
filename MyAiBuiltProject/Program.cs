using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyAiBuiltProject
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Minimal MCP-compatible JSON-RPC server over stdio with one tool: echo
            var server = new McpJsonRpcServer();
            await server.RunAsync();
        }
    }

    // Basic JSON-RPC2.0 request/response models
    internal sealed class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
        [JsonPropertyName("id")] public JsonElement? Id { get; set; }
        [JsonPropertyName("method")] public string Method { get; set; } = string.Empty;
        [JsonPropertyName("params")] public JsonElement? Params { get; set; }
    }

    internal sealed class JsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
        [JsonPropertyName("id")] public JsonElement? Id { get; set; }
        [JsonPropertyName("result")] public object? Result { get; set; }
        [JsonPropertyName("error")] public JsonRpcError? Error { get; set; }
    }

    internal sealed class JsonRpcError
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
        [JsonPropertyName("data")] public object? Data { get; set; }
    }

    // A minimal MCP server implementation
    internal sealed class McpJsonRpcServer
    {
        private const string ProtocolVersionLabel = "2024-11-05";
        private const string ServerName = "MyAiBuiltProject.McpServer";
        private const string ServerVersion = "0.1.0";

        private readonly Stream _stdin = Console.OpenStandardInput();
        private readonly Stream _stdout = Console.OpenStandardOutput();
        private readonly Stream _stderr = Console.OpenStandardError();
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            // Diagnostic note to aid hosts (stderr only)
            await WriteStderrAsync($"[MCP] {ServerName} v{ServerVersion} starting. Waiting for initialize...\n").ConfigureAwait(false);
            //await WriteStartupBannerAsync().ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                JsonRpcRequest? request = null;
                try
                {
                    var messageBytes = await ReadContentLengthFramedMessageAsync(cancellationToken).ConfigureAwait(false);
                    if (messageBytes is null)
                    {
                        // End of input
                        break;
                    }

                    request = JsonSerializer.Deserialize<JsonRpcRequest>(messageBytes.Value.Span, _jsonOptions);
                    if (request is null)
                    {
                        // Parse error: if request id is unknown/null, do not respond (notification semantics)
                        await WriteStderrAsync("[MCP] Parse error (null request)\n").ConfigureAwait(false);
                        continue;
                    }

                    var isNotification = request.Id is null;
                    await WriteStderrAsync($"[MCP] <= {request.Method} (notification: {isNotification})\n").ConfigureAwait(false);

                    switch (request.Method)
                    {
                        case "ping":
                            if (!isNotification)
                                await WriteResultAsync(request.Id, new { ok = true }).ConfigureAwait(false);
                            break;

                        case "initialize":
                            if (!isNotification)
                            {
                                // Minimal MCP initialize response with tools, resources, and prompts capabilities
                                var initResult = new
                                {
                                    protocolVersion = ProtocolVersionLabel, // MCP protocol version label
                                    serverInfo = new { name = ServerName, version = ServerVersion },
                                    capabilities = new
                                    {
                                        tools = new { },
                                        resources = new { },
                                        prompts = new { }
                                    }
                                };
                                await WriteResultAsync(request.Id, initResult).ConfigureAwait(false);
                            }
                            break;

                        case "tools/list":
                            if (!isNotification)
                            {
                                var toolsList = new
                                {
                                    tools = new object[]
                                    {
                                        new
                                        {
                                            name = "echo",
                                            description = "Echo back a provided message.",
                                            inputSchema = new
                                            {
                                                type = "object",
                                                properties = new
                                                {
                                                    message = new { type = "string", description = "Text to echo back" }
                                                },
                                                required = new[] { "message" }
                                            }
                                        }
                                    }
                                };
                                await WriteResultAsync(request.Id, toolsList).ConfigureAwait(false);
                            }
                            break;

                        case "tools/call":
                            if (!isNotification)
                                await HandleToolsCallAsync(request).ConfigureAwait(false);
                            break;

                        case "resources/list":
                            if (!isNotification)
                                await HandleResourcesListAsync(request).ConfigureAwait(false);
                            break;

                        case "resources/read":
                            if (!isNotification)
                                await HandleResourcesReadAsync(request).ConfigureAwait(false);
                            break;

                        case "prompts/list":
                            if (!isNotification)
                                await HandlePromptsListAsync(request).ConfigureAwait(false);
                            break;

                        case "prompts/get":
                            if (!isNotification)
                                await HandlePromptsGetAsync(request).ConfigureAwait(false);
                            break;

                        default:
                            // Unknown method: ignore notifications, respond with error for requests
                            if (!isNotification)
                                await WriteErrorAsync(request.Id, -32601, $"Method not found: {request.Method}").ConfigureAwait(false);
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await WriteStderrAsync($"[MCP] Unhandled exception: {ex}\n").ConfigureAwait(false);
                    // Don't respond to notifications on error
                    if (request?.Id is not null)
                    {
                        await WriteErrorAsync(request!.Id, -32603, "Internal error").ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task WriteStartupBannerAsync()
        {
            // Compute registration details without writing to stdout (use stderr to avoid breaking MCP framing)
            var entryPath = Assembly.GetEntryAssembly()?.Location
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? string.Empty;
            var wd = Path.GetDirectoryName(entryPath) ?? Environment.CurrentDirectory;
            var ext = Path.GetExtension(entryPath);
            var isDll = string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase);
            var isExe = string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase);

            string command;
            string arguments;
            if (isDll)
            {
                command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet" : "dotnet";
                arguments = Quote(entryPath);
            }
            else if (isExe)
            {
                command = Quote(entryPath);
                arguments = string.Empty;
            }
            else
            {
                // Fallback: assume framework-dependent .dll
                command = "dotnet";
                arguments = Quote(entryPath);
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== MCP Server Startup ===");
            sb.AppendLine($"Server: {ServerName} v{ServerVersion}");
            sb.AppendLine($"Protocol: MCP {ProtocolVersionLabel} (JSON-RPC2.0 over stdio)");
            sb.AppendLine($"Runtime: .NET {Environment.Version} on {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
            sb.AppendLine();
            sb.AppendLine("Register in Visual Studio (MCP Server):");
            sb.AppendLine($" Transport: stdio");
            sb.AppendLine($" Command: {command}");
            sb.AppendLine($" Arguments: {arguments}");
            sb.AppendLine($" WorkingDir:{wd}");
            sb.AppendLine();
            sb.AppendLine("Capabilities provided:");
            sb.AppendLine(" - Tools: echo { message: string }");
            sb.AppendLine(" - Resources: resource://hello.txt (text/plain)");
            sb.AppendLine(" - Prompts: greet(name)");
            sb.AppendLine();
            sb.AppendLine("Smoke test suggestions:");
            sb.AppendLine(" - Use tool echo with message: hello");
            sb.AppendLine(" - Insert resource hello.txt");
            sb.AppendLine(" - Run prompt greet with name=World");
            sb.AppendLine("===========================");

            await WriteStderrAsync(sb.ToString()).ConfigureAwait(false);
        }

        private static string Quote(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.Contains(' ') && !(path.StartsWith('"') && path.EndsWith('"'))
                ? $"\"{path}\""
                : path;
        }

        private async Task HandleToolsCallAsync(JsonRpcRequest request)
        {
            if (request.Params is null)
            {
                await WriteErrorAsync(request.Id, -32602, "Invalid params").ConfigureAwait(false);
                return;
            }

            string? toolName = null;
            JsonElement argsElement = default;
            try
            {
                var root = request.Params.Value;
                toolName = root.GetProperty("name").GetString();
                if (root.TryGetProperty("arguments", out var a))
                {
                    argsElement = a;
                }
            }
            catch
            {
                // fallthrough
            }

            if (string.IsNullOrWhiteSpace(toolName))
            {
                await WriteErrorAsync(request.Id, -32602, "Missing tool name"). ConfigureAwait(false);
                return;
            }

            switch (toolName)
            {
                case "echo":
                    var message = argsElement.ValueKind == JsonValueKind.Object && argsElement.TryGetProperty("message", out var msg)
                        ? msg.GetString() ?? string.Empty
                        : string.Empty;

                    var echoResult = new
                    {
                        // MCP tool results typically return a content array with text parts
                        content = new object[]
                        {
                            new { type = "text", text = message }
                        }
                    };
                    await WriteResultAsync(request.Id, echoResult).ConfigureAwait(false);
                    break;

                default:
                    await WriteErrorAsync(request.Id, -32601, $"Unknown tool: {toolName}"). ConfigureAwait(false);
                    break;
            }
        }

        private async Task HandleResourcesListAsync(JsonRpcRequest request)
        {
            // Minimal static resource list
            var result = new
            {
                resources = new object[]
                {
                    new
                    {
                        uri = "resource://hello.txt",
                        name = "hello.txt",
                        description = "Sample resource exposed by the server",
                        mimeType = "text/plain"
                    }
                }
            };
            await WriteResultAsync(request.Id, result).ConfigureAwait(false);
        }

        private async Task HandleResourcesReadAsync(JsonRpcRequest request)
        {
            if (request.Params is null)
            {
                await WriteErrorAsync(request.Id, -32602, "Invalid params"). ConfigureAwait(false);
                return;
            }

            string? uri = null;
            try
            {
                var root = request.Params.Value;
                uri = root.GetProperty("uri").GetString();
            }
            catch { }

            if (string.IsNullOrWhiteSpace(uri))
            {
                await WriteErrorAsync(request.Id, -32602, "Missing uri"). ConfigureAwait(false);
                return;
            }

            if (!string.Equals(uri, "resource://hello.txt", StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorAsync(request.Id, -32602, $"Unknown resource uri: {uri}").ConfigureAwait(false);
                return;
            }

            var result = new
            {
                // Some clients expect `contents`, others use `content`. Provide both for compatibility.
                contents = new object[] { new { type = "text", text = "Hello from MCP resource!" } },
                content = new object[] { new { type = "text", text = "Hello from MCP resource!" } }
            };
            await WriteResultAsync(request.Id, result).ConfigureAwait(false);
        }

        private async Task HandlePromptsListAsync(JsonRpcRequest request)
        {
            var result = new
            {
                prompts = new object[]
                {
                    new
                    {
                        name = "greet",
                        description = "Create a greeting for a person.",
                        arguments = new object[]
                        {
                            new { name = "name", description = "Person to greet", required = true }
                        }
                    }
                }
            };
            await WriteResultAsync(request.Id, result).ConfigureAwait(false);
        }

        private async Task HandlePromptsGetAsync(JsonRpcRequest request)
        {
            if (request.Params is null)
            {
                await WriteErrorAsync(request.Id, -32602, "Invalid params").ConfigureAwait(false);
                return;
            }

            string? promptName = null;
            JsonElement argsElement = default;
            try
            {
                var root = request.Params.Value;
                promptName = root.GetProperty("name").GetString();
                if (root.TryGetProperty("arguments", out var a))
                {
                    argsElement = a;
                }
            }
            catch { }

            if (!string.Equals(promptName, "greet", StringComparison.OrdinalIgnoreCase))
            {
                await WriteErrorAsync(request.Id, -32601, $"Unknown prompt: {promptName}"). ConfigureAwait(false);
                return;
            }

            var name = argsElement.ValueKind == JsonValueKind.Object && argsElement.TryGetProperty("name", out var n)
                ? (n.GetString() ?? "World")
                : "World";

            var result = new
            {
                // Common MCP shape for prompts: an array of messages with role and content parts
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = $"Please greet {name} in a friendly tone." }
                        }
                    }
                }
            };
            await WriteResultAsync(request.Id, result).ConfigureAwait(false);
        }

        private async ValueTask<ReadOnlyMemory<byte>?> ReadContentLengthFramedMessageAsync(CancellationToken ct)
        {
            // Read raw headers and body from the same underlying stream to avoid buffering issues.
            const int MaxHeaderBytes =64 *1024; // safety cap
            byte[] headerBuf = ArrayPool<byte>.Shared.Rent(4096);
            int headerCount =0;
            try
            {
                // Read until blank line (either CRLFCRLF or LFLF)
                while (true)
                {
                    if (headerCount == headerBuf.Length)
                    {
                        if (headerBuf.Length >= MaxHeaderBytes) throw new InvalidOperationException("Header too large");
                        var newBuf = ArrayPool<byte>.Shared.Rent(headerBuf.Length *2);
                        Buffer.BlockCopy(headerBuf,0, newBuf,0, headerCount);
                        ArrayPool<byte>.Shared.Return(headerBuf);
                        headerBuf = newBuf;
                    }

                    int n = await _stdin.ReadAsync(headerBuf.AsMemory(headerCount, headerBuf.Length - headerCount), ct).ConfigureAwait(false);
                    if (n ==0)
                    {
                        if (headerCount ==0) return null; // EOF before any header
                        throw new EndOfStreamException("Unexpected EOF while reading headers");
                    }
                    headerCount += n;

                    // Detect header end: try CRLFCRLF first, then LFLF
                    int headerEnd = FindHeaderEnd(headerBuf, headerCount);
                    if (headerEnd >=0)
                    {
                        // Parse headers
                        var headerText = Encoding.ASCII.GetString(headerBuf,0, headerEnd);
                        int? contentLength = null;
                        // Split on either CRLF or LF to be robust
                        foreach (var line in headerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            const string prefix = "Content-Length:";
                            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                var value = line.Substring(prefix.Length).Trim();
                                if (int.TryParse(value, out var len)) contentLength = len;
                            }
                        }

                        if (contentLength is null) return null; // treat as end/invalid

                        int bodyLength = contentLength.Value;
                        var body = ArrayPool<byte>.Shared.Rent(bodyLength);
                        int copied =0;

                        // Copy any bytes already read beyond header end
                        int bodyStart = headerEnd + HeaderDelimiterLength(headerBuf, headerEnd);
                        int preRead = headerCount - bodyStart;
                        if (preRead >0)
                        {
                            int toCopy = Math.Min(preRead, bodyLength);
                            Buffer.BlockCopy(headerBuf, bodyStart, body,0, toCopy);
                            copied += toCopy;
                        }

                        // Read remaining body bytes
                        while (copied < bodyLength)
                        {
                            int m = await _stdin.ReadAsync(body.AsMemory(copied, bodyLength - copied), ct).ConfigureAwait(false);
                            if (m ==0) throw new EndOfStreamException("Unexpected EOF while reading body");
                            copied += m;
                        }

                        // Return a tight array
                        var result = new byte[bodyLength];
                        Buffer.BlockCopy(body,0, result,0, bodyLength);
                        ArrayPool<byte>.Shared.Return(body);
                        return result;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuf);
            }

            static int FindHeaderEnd(byte[] buffer, int count)
            {
                // Try CRLFCRLF first
                for (int i = Math.Max(0, count -4); i <= count -4; i++)
                {
                    if (buffer[i] == (byte)'\r' && buffer[i +1] == (byte)'\n' && buffer[i +2] == (byte)'\r' && buffer[i +3] == (byte)'\n')
                    {
                        return i;
                    }
                }
                // Then try LFLF
                for (int i = Math.Max(0, count -2); i <= count -2; i++)
                {
                    if (buffer[i] == (byte)'\n' && buffer[i +1] == (byte)'\n')
                    {
                        return i;
                    }
                }
                return -1;
            }

            static int HeaderDelimiterLength(byte[] buffer, int headerEndIndex)
            {
                // Determine whether the delimiter was CRLFCRLF (length4) or LFLF (length2)
                return buffer[headerEndIndex] == (byte)'\r' ?4 :2;
            }
        }

        private async Task WriteResultAsync(JsonElement? id, object result)
        {
            var response = new JsonRpcResponse
            {
                Id = id,
                Result = result
            };
            await WriteMessageAsync(response).ConfigureAwait(false);
        }

        private async Task WriteErrorAsync(JsonElement? id, int code, string message, object? data = null)
        {
            var response = new JsonRpcResponse
            {
                Id = id,
                Error = new JsonRpcError { Code = code, Message = message, Data = data }
            };
            await WriteMessageAsync(response).ConfigureAwait(false);
        }

        private async Task WriteMessageAsync(object payload)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions);
            // Write headers + body (add Content-Type for host compatibility)
            var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\nContent-Type: application/json; charset=utf-8\r\n\r\n");
            await _stdout.WriteAsync(header,0, header.Length).ConfigureAwait(false);
            await _stdout.WriteAsync(bytes,0, bytes.Length).ConfigureAwait(false);
            await _stdout.FlushAsync().ConfigureAwait(false);
        }

        private async Task WriteStderrAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _stderr.WriteAsync(bytes,0, bytes.Length).ConfigureAwait(false);
            await _stderr.FlushAsync().ConfigureAwait(false);
        }
    }
}
