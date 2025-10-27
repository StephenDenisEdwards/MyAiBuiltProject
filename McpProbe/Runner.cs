using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpProbe;

internal static class Program
{
	private static async Task<int> Main(string[] args)
	{

		Console.WriteLine("Press any key to start...");
		Console.ReadKey(intercept: true);

		var serverPath = ResolveServerPath(args.FirstOrDefault());
		if (serverPath is null)
		{
			Console.Error.WriteLine(
				"Could not locate server binary. Build/publish MyAiBuiltProject or pass a path as the first argument.");
			Console.Error.WriteLine(
				"Examples:\n dotnet publish ..\\MyAiBuiltProject -c Release\n McpProbe <path-to-server-exe-or-dll>");
			return 2;
		}

		Console.WriteLine($"Launching server: {serverPath}");
		var psi = new ProcessStartInfo
		{
			FileName = NeedsDotNetHost(serverPath) ? "dotnet" : serverPath,
			Arguments = NeedsDotNetHost(serverPath) ? Quote(serverPath) : string.Empty,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start server process");
		_ = Task.Run(async () =>
		{
			while (!proc.HasExited)
			{
				var line = await proc.StandardError.ReadLineAsync();
				if (line is null) break;
				Console.Error.WriteLine($"[server] {line}");
			}

			Console.WriteLine("Process Exited");
		});

		var transport = new JsonRpcTransport(proc);

		try
		{
			//1) initialize
			await transport.SendAsync(new JsonRpcRequest
				{ Method = "initialize", Id = JsonSerializer.SerializeToElement(1) });
			Dump("initialize", await transport.ReadAsync());

			//2) ping
			await transport.SendAsync(new JsonRpcRequest
				{ Method = "ping", Id = JsonSerializer.SerializeToElement(2) });
			Dump("ping", await transport.ReadAsync());

			//3) tools/list
			await transport.SendAsync(new JsonRpcRequest
				{ Method = "tools/list", Id = JsonSerializer.SerializeToElement(3) });
			Dump("tools/list", await transport.ReadAsync());

			//4) tools/call echo
			var echoParams = JsonSerializer.SerializeToElement(new
				{ name = "echo", arguments = new { message = "hello from probe" } });
			await transport.SendAsync(new JsonRpcRequest
				{ Method = "tools/call", Id = JsonSerializer.SerializeToElement(4), Params = echoParams });
			Dump("tools/call", await transport.ReadAsync());

			//5) resources/list
			await transport.SendAsync(new JsonRpcRequest
				{ Method = "resources/list", Id = JsonSerializer.SerializeToElement(5) });
			Dump("resources/list", await transport.ReadAsync());

			//6) resources/read
			var readParams = JsonSerializer.SerializeToElement(new { uri = "resource://hello.txt" });
			await transport.SendAsync(new JsonRpcRequest
				{ Method = "resources/read", Id = JsonSerializer.SerializeToElement(6), Params = readParams });
			Dump("resources/read", await transport.ReadAsync());

			//7) prompts/list
			await transport.SendAsync(new JsonRpcRequest
				{ Method = "prompts/list", Id = JsonSerializer.SerializeToElement(7) });
			Dump("prompts/list", await transport.ReadAsync());

			//8) prompts/get greet
			var getParams =
				JsonSerializer.SerializeToElement(new { name = "greet", arguments = new { name = "World" } });
			await transport.SendAsync(new JsonRpcRequest
				{ Method = "prompts/get", Id = JsonSerializer.SerializeToElement(8), Params = getParams });
			Dump("prompts/get", await transport.ReadAsync());
		}
		finally
		{
			try
			{
				proc.StandardInput.Close();
			}
			catch
			{
			}

			if (!proc.HasExited)
			{
				try
				{
					proc.Kill(true);
				}
				catch
				{
				}
			}
		}

		return 0;
	}

	private static void Dump(string label, JsonDocument resp)
	{
		Console.WriteLine($"\n=== {label} ===\n{Pretty(resp)}");
	}

	private static string Pretty(JsonDocument doc)
	{
		var opts = new JsonSerializerOptions { WriteIndented = true };
		return JsonSerializer.Serialize(doc.RootElement, opts);
	}

	private static bool NeedsDotNetHost(string path) =>
		Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase);

	private static string? ResolveServerPath(string? arg)
	{
		if (!string.IsNullOrWhiteSpace(arg))
		{
			var p = Path.GetFullPath(arg);
			if (File.Exists(p)) return p;
		}

		// Try default publish location used by .mcp.json
		var publishDll = Path.GetFullPath("..\\MyAiBuiltProject\\bin\\Release\\net9.0\\publish\\MyAiBuiltProject.dll");
		if (File.Exists(publishDll)) return publishDll;

		// Try Debug build exe (self-contained or framework dependent)
		var debugExe = Path.GetFullPath("..\\MyAiBuiltProject\\bin\\Debug\\net9.0\\MyAiBuiltProject.exe");
		if (File.Exists(debugExe)) return debugExe;
		var debugDll = Path.GetFullPath("..\\MyAiBuiltProject\\bin\\Debug\\net9.0\\MyAiBuiltProject.dll");
		if (File.Exists(debugDll)) return debugDll;

		return null;
	}

	private static string Quote(string s) => s.Contains(' ') ? $"\"{s}\"" : s;
}

internal sealed class JsonRpcTransport
{
	private readonly Process _process;

	private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public JsonRpcTransport(Process process)
	{
		_process = process;
	}

	public async Task SendAsync(JsonRpcRequest request, CancellationToken ct = default)
	{
		var bytes = JsonSerializer.SerializeToUtf8Bytes(request, _jsonOptions);
		var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
		await _process.StandardInput.BaseStream.WriteAsync(header.AsMemory(0, header.Length), ct);
		await _process.StandardInput.BaseStream.WriteAsync(bytes.AsMemory(0, bytes.Length), ct);
		await _process.StandardInput.BaseStream.FlushAsync(ct);
	}

	public async Task<JsonDocument> ReadAsync(CancellationToken ct = default)
	{
		// Read headers until empty line
		string? line;
		int? contentLength = null;
		while ((line = await _process.StandardOutput.ReadLineAsync().WaitAsync(ct)) != null)
		{
			if (line.Length == 0) break;
			const string prefix = "Content-Length:";
			if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				var value = line.Substring(prefix.Length).Trim();
				if (int.TryParse(value, out var len)) contentLength = len;
			}
		}

		if (contentLength is null) throw new InvalidOperationException("Missing Content-Length header");

		var buffer = ArrayPool<byte>.Shared.Rent(contentLength.Value);
		try
		{
			int read = 0;
			while (read < contentLength.Value)
			{
				int n = await _process.StandardOutput.BaseStream.ReadAsync(
					buffer.AsMemory(read, contentLength.Value - read), ct);
				if (n == 0) throw new EndOfStreamException();
				read += n;
			}

			return JsonDocument.Parse(new ReadOnlyMemory<byte>(buffer, 0, contentLength.Value));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}

internal sealed class JsonRpcRequest
{
	[JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
	[JsonPropertyName("id")] public JsonElement? Id { get; set; }
	[JsonPropertyName("method")] public string Method { get; set; } = string.Empty;
	[JsonPropertyName("params")] public JsonElement? Params { get; set; }
}
