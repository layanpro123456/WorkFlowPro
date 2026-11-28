using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class TaskGitBranch : BaseEntity
{
    public Guid TaskId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public string BranchName { get; private set; } = string.Empty;
    public string BaseBranch { get; private set; } = string.Empty;
    public string BranchUrl { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }

    public TaskItem? Task { get; private set; }
    public ProjectGitRepository? Repository { get; private set; }

    private TaskGitBranch() { } // For EF Core

    public TaskGitBranch(
        Guid taskId,
        Guid repositoryId,
        string branchName,
        string baseBranch,
        string branchUrl,
        Guid? createdByUserId = null)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("TaskId cannot be empty.", nameof(taskId));
        if (repositoryId == Guid.Empty) throw new ArgumentException("RepositoryId cannot be empty.", nameof(repositoryId));
        if (string.IsNullOrWhiteSpace(branchName)) throw new DomainException("Branch name is required.");
        if (string.IsNullOrWhiteSpace(baseBranch)) throw new DomainException("Base branch is required.");
        if (string.IsNullOrWhiteSpace(branchUrl)) throw new DomainException("Branch URL is required.");

        Id = Guid.NewGuid();
        TaskId = taskId;
        RepositoryId = repositoryId;
        BranchName = branchName.Trim();
        BaseBranch = baseBranch.Trim();
        BranchUrl = branchUrl.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
