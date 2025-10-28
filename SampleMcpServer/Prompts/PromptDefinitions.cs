using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SampleMcpServer.Prompts;

/// <summary>
/// Sample MCP prompts exposed by the server.
/// </summary>
internal class PromptDefinitions
{
	[McpServerPrompt]
	[Description("Create a friendly greeting for the specified name.")]
	public string Greet(
		[Description("Person to greet")] string name = "World")
	{
		// Returning a single string is treated as a simple user message by the SDK.
		return $"Please greet {name} in a friendly tone.";
	}

	[McpServerPrompt]
	[Description("Write a concise haiku about a topic.")]
	public string Haiku(
		[Description("Topic for the haiku")] string topic = "coding")
	{
		return $"Write a three-line haiku about {topic}. Use a5-7-5 syllable structure.";
	}



	[McpServerPrompt]
	[Description("Exercise all Todo tools end-to-end and report a concise PASS/FAIL log.")]
	public string TestTodos()
	{
		return """
		       You are testing the MCP Todo tools exposed by this server. Execute the MCP tools directly; do not simulate outputs. Use the `data` payloads to make assertions and produce a compact test report.

		       Tools under test:
		       - TodoCreate(title, notes?, dueAt?)
		       - TodoList(status?)
		       - TodoGet(id)
		       - TodoUpdate(id, title?, notes?, dueAt?)
		       - TodoComplete(id)
		       - TodoDelete(id)

		       Test plan:
		       1) Create two todos:
		          - A: title="Write tests", notes="end-to-end", dueAt=now+1d (ISO 8601, UTC 'Z')
		          - B: title="Ship feature"
		          Capture their ids.
		       2) List all: expect at least A and B present; both Status=Pending.
		       3) Get A by id: verify fields match; Status=Pending.
		       4) Update A: title="Write E2E tests", notes="" (to clear notes), dueAt=now+2d.
		          Verify Title updated, Notes is null, and DueAt changed.
		       5) Complete B: verify Status=Completed.
		       6) List Pending: contains A, not B.
		       7) List Completed: contains B.
		       8) Delete A: succeeds.
		       9) List all: contains B only.
		       10) Negative cases (use a non-existent id like 999999):
		           - Get, Update, Complete, Delete each should return a "not found" result/message.

		       Output:
		       - Print a compact PASS/FAIL line for each step with any assertion details.
		       - End with a one-line summary like: "Todos E2E: X passed, Y failed".

		       Important:
		       - Use actual MCP tool results for checks, not assumptions.
		       - Prefer the `data` object from each tool result for machine checks (ids, titles, status) over formatted text.
		       - Status values are "Pending" or "Completed" (case-insensitive).
		       """;
	}

}
