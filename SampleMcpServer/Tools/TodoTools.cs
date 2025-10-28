using System.ComponentModel;
using ModelContextProtocol.Server;
using SampleMcpServer.Data;

namespace SampleMcpServer.Tools;

/// <summary>
/// MCP Tools exposing Todo list operations backed by an in-memory repository.
/// </summary>
internal sealed class TodoTools
{
	private static readonly TodoRepository Repo = new();

	[McpServerTool]
	[Description("Create a new todo item.")]
	public object TodoCreate(
		[Description("Short title for the task")]
		string title,
		[Description("Optional notes/details")]
		string? notes = null,
		[Description("Optional due date (ISO8601, UTC recommended)")]
		DateTimeOffset? dueAt = null)
	{
		var item = Repo.Add(title, notes, dueAt);
		return new
		{
			content = new object[]
			{
				new { type = "text", text = $"Created todo #{item.Id}: {item.Title}" }
			},
			data = item
		};
	}

	[McpServerTool]
	[Description("List todo items, optionally filtered by status.")]
	public object TodoList(
		[Description("Filter by status: Pending or Completed")]
		string? status = null)
	{
		TodoStatus? filter = status is null ? null : Enum.TryParse<TodoStatus>(status, true, out var s) ? s : null;
		var list = Repo.GetAll(filter);
		var lines = list.Select(i => $"#{i.Id} [{i.Status}] {i.Title}").ToArray();
		return new
		{
			content = new object[]
				{ new { type = "text", text = (lines.Length == 0 ? "(no todos)" : string.Join("\n", lines)) } },
			data = list
		};
	}

	[McpServerTool]
	[Description("Get a single todo by id.")]
	public object TodoGet(
		[Description("Todo id")] int id)
	{
		var item = Repo.Get(id);
		if (item is null)
			return new { content = new object[] { new { type = "text", text = $"Todo #{id} not found" } } };
		return new
		{
			content = new object[] { new { type = "text", text = $"#{item.Id} [{item.Status}] {item.Title}" } },
			data = item
		};
	}

	[McpServerTool]
	[Description("Update fields of a todo item.")]
	public object TodoUpdate(
		[Description("Todo id")] int id,
		[Description("New title")] string? title = null,
		[Description("New notes")] string? notes = null,
		[Description("New due date (ISO8601)")]
		DateTimeOffset? dueAt = null)
	{
		var updated = Repo.Update(id, title, notes, dueAt);
		if (updated is null)
			return new { content = new object[] { new { type = "text", text = $"Todo #{id} not found" } } };
		return new { content = new object[] { new { type = "text", text = $"Updated todo #{id}" } }, data = updated };
	}

	[McpServerTool]
	[Description("Mark a todo as completed.")]
	public object TodoComplete(
		[Description("Todo id")] int id)
	{
		var done = Repo.Complete(id);
		if (done is null)
			return new { content = new object[] { new { type = "text", text = $"Todo #{id} not found" } } };
		return new { content = new object[] { new { type = "text", text = $"Completed todo #{id}" } }, data = done };
	}

	[McpServerTool]
	[Description("Delete a todo by id.")]
	public object TodoDelete(
		[Description("Todo id")] int id)
	{
		return Repo.Delete(id)
			? new { content = new object[] { new { type = "text", text = $"Deleted todo #{id}" } } }
			: new { content = new object[] { new { type = "text", text = $"Todo #{id} not found" } } };
	}
}
