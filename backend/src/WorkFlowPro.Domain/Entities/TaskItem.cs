using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class TaskItem : BaseEntity
{
    private readonly List<TaskComment> _comments = new();
    private readonly List<TaskStatusHistory> _statusHistory = new();
    private readonly List<TaskGitBranch> _gitBranches = new();
    private readonly List<TaskPullRequestLink> _pullRequestLinks = new();

    public Guid ProjectId { get; private set; }
    public int TaskNumber { get; private set; }
    public string TaskKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Status { get; private set; } = TaskStatuses.Backlog;
    public string Priority { get; private set; } = TaskPriorities.Normal;
    public Guid? AssigneeId { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTime? DueAtUtc { get; private set; }
    public int? EstimateMinutes { get; private set; }
    public int Version { get; private set; } = 1;

    public Project? Project { get; private set; }
    public IReadOnlyCollection<TaskComment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<TaskStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyCollection<TaskGitBranch> GitBranches => _gitBranches.AsReadOnly();
    public IReadOnlyCollection<TaskPullRequestLink> PullRequestLinks => _pullRequestLinks.AsReadOnly();

    private TaskItem() { } // For EF Core

    public TaskItem(
        Guid projectId,
        string title,
        string? description,
        Guid createdById,
        string priority = TaskPriorities.Normal,
        Guid? assigneeId = null,
        DateTime? dueAtUtc = null,
        int? estimateMinutes = null,
        string status = TaskStatuses.Backlog)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        if (createdById == Guid.Empty) throw new ArgumentException("CreatedById cannot be empty.", nameof(createdById));
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Task title is required.");

        ValidateTitle(title);
        ValidatePriority(priority);
        ValidateStatus(status);
        ValidateEstimate(estimateMinutes);

        Id = Guid.NewGuid();
        ProjectId = projectId;
        Title = title.Trim();
        Description = description?.Trim();
        CreatedById = createdById;
        Priority = priority;
        AssigneeId = assigneeId;
        DueAtUtc = dueAtUtc;
        EstimateMinutes = estimateMinutes;
        Status = status;
        Version = 1;
        CreatedAtUtc = DateTime.UtcNow;

        _statusHistory.Add(new TaskStatusHistory(
            taskId: Id,
            fromStatus: null,
            toStatus: Status,
            changedById: createdById,
            changedAtUtc: CreatedAtUtc,
            comment: "Task created."
        ));
    }

    public void UpdateDetails(
        string title,
        string? description,
        string priority,
        Guid? assigneeId,
        DateTime? dueAtUtc,
        int? estimateMinutes,
        int expectedVersion)
    {
        EnsureVersion(expectedVersion);

        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Task title is required.");
        ValidateTitle(title);
        ValidatePriority(priority);
        ValidateEstimate(estimateMinutes);

        Title = title.Trim();
        Description = description?.Trim();
        Priority = priority;
        AssigneeId = assigneeId;
        DueAtUtc = dueAtUtc;
        EstimateMinutes = estimateMinutes;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public TaskStatusHistory ChangeStatus(
        string newStatus,
        Guid changedById,
        int expectedVersion,
        string? comment = null)
    {
        EnsureVersion(expectedVersion);
        if (changedById == Guid.Empty) throw new ArgumentException("ChangedById cannot be empty.", nameof(changedById));

        TaskStatusTransitions.EnsureValidTransition(Status, newStatus);

        var previousStatus = Status;
        Status = newStatus;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;

        var history = new TaskStatusHistory(
            taskId: Id,
            fromStatus: previousStatus,
            toStatus: newStatus,
            changedById: changedById,
            changedAtUtc: UpdatedAtUtc.Value,
            comment: comment
        );
        _statusHistory.Add(history);
        return history;
    }

    public TaskComment AddComment(Guid authorId, string body)
    {
        if (authorId == Guid.Empty) throw new ArgumentException("AuthorId cannot be empty.", nameof(authorId));
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("Comment body is required.");

        var comment = new TaskComment(Id, authorId, body);
        _comments.Add(comment);
        return comment;
    }

    public void AssignTaskKey(int taskNumber, string projectCode)
    {
        if (taskNumber <= 0) throw new DomainException("Task number must be positive.");
        if (string.IsNullOrWhiteSpace(projectCode)) throw new DomainException("Project code is required to assign a task key.");

        if (TaskNumber == 0)
        {
            TaskNumber = taskNumber;
            TaskKey = $"{projectCode.Trim().ToUpperInvariant()}-{taskNumber}";
        }
    }

    /// <summary>
    /// Walks the shortest valid status transition path from the current status to targetStatus
    /// for automated Git/PR events, recording each step in StatusHistory.
    /// </summary>
    public IReadOnlyList<TaskStatusHistory> ApplyAutomationTransition(
        string targetStatus,
        Guid actorId,
        string comment)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        ValidateStatus(targetStatus);

        if (string.Equals(Status, targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<TaskStatusHistory>();
        }

        // Do not auto-reopen Done or Cancelled tasks unless explicitly moving forward
        var path = FindShortestTransitionPath(Status, targetStatus);
        if (path.Count == 0)
        {
            return Array.Empty<TaskStatusHistory>();
        }

        var recorded = new List<TaskStatusHistory>(path.Count);
        for (var i = 0; i < path.Count; i++)
        {
            var stepTarget = path[i];
            var stepComment = i == path.Count - 1
                ? comment
                : $"{comment} (auto-step)";
            var history = ChangeStatus(stepTarget, actorId, Version, stepComment);
            recorded.Add(history);
        }

        return recorded;
    }

    private static List<string> FindShortestTransitionPath(string currentStatus, string targetStatus)
    {
        var queue = new Queue<List<string>>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentStatus };
        queue.Enqueue(new List<string>());

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var head = path.Count == 0 ? currentStatus : path[^1];

            foreach (var next in TaskStatusTransitions.GetAllowedNextStatuses(head))
            {
                // Never route through Cancelled unless Cancelled is the target
                if (string.Equals(next, TaskStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(targetStatus, TaskStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!visited.Add(next))
                {
                    continue;
                }

                var nextPath = new List<string>(path) { next };
                if (string.Equals(next, targetStatus, StringComparison.OrdinalIgnoreCase))
                {
                    return nextPath;
                }

                queue.Enqueue(nextPath);
            }
        }

        return new List<string>();
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new ConcurrencyException($"Task version conflict. The task has been modified (current version: {Version}, provided: {expectedVersion}).");
        }
    }

    private static void ValidateTitle(string title)
    {
        if (title.Trim().Length > 200)
        {
            throw new DomainException("Task title cannot exceed 200 characters.");
        }
    }

    private static void ValidatePriority(string priority)
    {
        if (!TaskPriorities.All.Contains(priority, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Invalid priority '{priority}'. Allowed values: {string.Join(", ", TaskPriorities.All)}");
        }
    }

    private static void ValidateStatus(string status)
    {
        if (!TaskStatuses.All.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Invalid status '{status}'. Allowed values: {string.Join(", ", TaskStatuses.All)}");
        }
    }

    private static void ValidateEstimate(int? estimateMinutes)
    {
        if (estimateMinutes.HasValue && estimateMinutes.Value < 0)
        {
            throw new DomainException("Estimate minutes cannot be negative.");
        }
    }
}
