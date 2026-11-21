using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class TaskComment
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public TaskItem? Task { get; private set; }

    private TaskComment() { } // For EF Core

    public TaskComment(Guid taskId, Guid authorId, string body)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("TaskId cannot be empty.", nameof(taskId));
        if (authorId == Guid.Empty) throw new ArgumentException("AuthorId cannot be empty.", nameof(authorId));
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("Comment body is required.");

        Id = Guid.NewGuid();
        TaskId = taskId;
        AuthorId = authorId;
        Body = body.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}
