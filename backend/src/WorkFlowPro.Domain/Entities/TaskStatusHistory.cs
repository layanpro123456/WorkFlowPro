using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class TaskStatusHistory
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = string.Empty;
    public Guid ChangedById { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Comment { get; private set; }

    public TaskItem? Task { get; private set; }

    private TaskStatusHistory() { } // For EF Core

    public TaskStatusHistory(
        Guid taskId,
        string? fromStatus,
        string toStatus,
        Guid changedById,
        DateTime changedAtUtc,
        string? comment = null)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("TaskId cannot be empty.", nameof(taskId));
        if (changedById == Guid.Empty) throw new ArgumentException("ChangedById cannot be empty.", nameof(changedById));
        if (string.IsNullOrWhiteSpace(toStatus)) throw new DomainException("ToStatus is required.");

        Id = Guid.NewGuid();
        TaskId = taskId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedById = changedById;
        ChangedAtUtc = changedAtUtc;
        Comment = comment?.Trim();
    }
}
