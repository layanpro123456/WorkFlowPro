using WorkFlowPro.Domain.Common;

namespace WorkFlowPro.Domain.Entities;

public class TaskPullRequestLink : BaseEntity
{
    public Guid TaskId { get; private set; }
    public Guid PullRequestId { get; private set; }
    public string LinkSource { get; private set; } = string.Empty;
    public Guid? LinkedByUserId { get; private set; }
    public DateTime LinkedAtUtc { get; private set; }

    public TaskItem? Task { get; private set; }
    public GitPullRequest? PullRequest { get; private set; }

    private TaskPullRequestLink() { } // For EF Core

    public TaskPullRequestLink(
        Guid taskId,
        Guid pullRequestId,
        string linkSource,
        Guid? linkedByUserId = null)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("TaskId cannot be empty.", nameof(taskId));
        if (pullRequestId == Guid.Empty) throw new ArgumentException("PullRequestId cannot be empty.", nameof(pullRequestId));

        Id = Guid.NewGuid();
        TaskId = taskId;
        PullRequestId = pullRequestId;
        LinkSource = linkSource;
        LinkedByUserId = linkedByUserId;
        LinkedAtUtc = DateTime.UtcNow;
        CreatedAtUtc = LinkedAtUtc;
    }
}
