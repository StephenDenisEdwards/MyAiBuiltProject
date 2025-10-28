using System.Collections.Concurrent;

namespace SampleMcpServer.Data;

internal enum TodoStatus
{
 Pending =0,
 Completed =1
}

internal sealed record TodoItem
{
 public int Id { get; init; }
 public required string Title { get; init; }
 public string? Notes { get; init; }
 public TodoStatus Status { get; init; } = TodoStatus.Pending;
 public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
 public DateTimeOffset? DueAt { get; init; }
}

/// <summary>
/// Thread-safe in-memory repository for Todo items.
/// </summary>
internal sealed class TodoRepository
{
 private readonly ConcurrentDictionary<int, TodoItem> _store = new();
 private int _nextId =0;

 public TodoItem Add(string title, string? notes = null, DateTimeOffset? dueAt = null)
 {
 if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));
 var id = Interlocked.Increment(ref _nextId);
 var item = new TodoItem
 {
 Id = id,
 Title = title.Trim(),
 Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
 DueAt = dueAt,
 Status = TodoStatus.Pending,
 CreatedAt = DateTimeOffset.UtcNow
 };
 _store[id] = item;
 return item;
 }

 public IReadOnlyList<TodoItem> GetAll(TodoStatus? status = null)
 {
 var values = _store.Values;
 if (status is null) return values.OrderBy(i => i.Id).ToArray();
 return values.Where(i => i.Status == status).OrderBy(i => i.Id).ToArray();
 }

 public TodoItem? Get(int id) => _store.TryGetValue(id, out var item) ? item : null;

 public TodoItem? Update(int id, string? title = null, string? notes = null, DateTimeOffset? dueAt = null)
 {
 return _store.AddOrUpdate(
 id,
 addValueFactory: _ => null!,
 updateValueFactory: (_, existing) => existing with
 {
 Title = string.IsNullOrWhiteSpace(title) ? existing.Title : title!.Trim(),
 Notes = notes is null ? existing.Notes : (string.IsNullOrWhiteSpace(notes) ? null : notes),
 DueAt = dueAt is null ? existing.DueAt : dueAt
 }
 );
 }

 public TodoItem? Complete(int id)
 {
 return _store.AddOrUpdate(
 id,
 addValueFactory: _ => null!,
 updateValueFactory: (_, existing) => existing with { Status = TodoStatus.Completed }
 );
 }

 public bool Delete(int id) => _store.TryRemove(id, out _);
}
