using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class GitPullRequest : BaseEntity
{
    private readonly List<TaskPullRequestLink> _taskLinks = new();

    public Guid RepositoryId { get; private set; }
    public long ExternalPrId { get; private set; }
    public int PrNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }
    public string State { get; private set; } = PullRequestStates.Open;
    public string SourceBranch { get; private set; } = string.Empty;
    public string TargetBranch { get; private set; } = string.Empty;
    public string AuthorLogin { get; private set; } = string.Empty;
    public string? AuthorAvatarUrl { get; private set; }
    public string HtmlUrl { get; private set; } = string.Empty;
    public string ReviewStatus { get; private set; } = PullRequestReviewStatuses.Pending;
    public string CheckStatus { get; private set; } = PullRequestCheckStatuses.None;
    public int CommitsCount { get; private set; }
    public int Additions { get; private set; }
    public int Deletions { get; private set; }
    public DateTime OpenedAtUtc { get; private set; }
    public DateTime? MergedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public DateTime LastSyncedAtUtc { get; private set; }

    public ProjectGitRepository? Repository { get; private set; }
    public IReadOnlyCollection<TaskPullRequestLink> TaskLinks => _taskLinks.AsReadOnly();

    private GitPullRequest() { } // For EF Core

    public GitPullRequest(
        Guid repositoryId,
        long externalPrId,
        int prNumber,
        string title,
        string? body,
        string state,
        string sourceBranch,
        string targetBranch,
        string authorLogin,
        string htmlUrl,
        DateTime openedAtUtc,
        string? authorAvatarUrl = null,
        string reviewStatus = PullRequestReviewStatuses.Pending,
        string checkStatus = PullRequestCheckStatuses.None,
        int commitsCount = 0,
        int additions = 0,
        int deletions = 0,
        DateTime? mergedAtUtc = null,
        DateTime? closedAtUtc = null)
    {
        if (repositoryId == Guid.Empty) throw new ArgumentException("RepositoryId cannot be empty.", nameof(repositoryId));
        if (prNumber <= 0) throw new DomainException("Pull request number must be positive.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Pull request title is required.");
        if (string.IsNullOrWhiteSpace(sourceBranch)) throw new DomainException("Source branch is required.");
        if (string.IsNullOrWhiteSpace(targetBranch)) throw new DomainException("Target branch is required.");

        Id = Guid.NewGuid();
        RepositoryId = repositoryId;
        ExternalPrId = externalPrId;
        PrNumber = prNumber;
        Title = title.Trim();
        Body = body;
        State = state;
        SourceBranch = sourceBranch.Trim();
        TargetBranch = targetBranch.Trim();
        AuthorLogin = string.IsNullOrWhiteSpace(authorLogin) ? "unknown" : authorLogin.Trim();
        AuthorAvatarUrl = authorAvatarUrl;
        HtmlUrl = htmlUrl.Trim();
        ReviewStatus = reviewStatus;
        CheckStatus = checkStatus;
        CommitsCount = commitsCount;
        Additions = additions;
        Deletions = deletions;
        OpenedAtUtc = openedAtUtc;
        MergedAtUtc = mergedAtUtc;
        ClosedAtUtc = closedAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
        LastSyncedAtUtc = CreatedAtUtc;
    }

    public (bool StateChanged, bool ReviewChanged, bool CheckChanged) UpdateFromRemote(
        long externalPrId,
        string title,
        string? body,
        string state,
        string sourceBranch,
        string targetBranch,
        string authorLogin,
        string htmlUrl,
        string? authorAvatarUrl,
        string? reviewStatus,
        string? checkStatus,
        int commitsCount,
        int additions,
        int deletions,
        DateTime? mergedAtUtc,
        DateTime? closedAtUtc)
    {
        var stateChanged = !string.Equals(State, state, StringComparison.OrdinalIgnoreCase);
        var reviewChanged = reviewStatus != null && !string.Equals(ReviewStatus, reviewStatus, StringComparison.OrdinalIgnoreCase);
        var checkChanged = checkStatus != null && !string.Equals(CheckStatus, checkStatus, StringComparison.OrdinalIgnoreCase);

        if (externalPrId > 0) ExternalPrId = externalPrId;
        if (!string.IsNullOrWhiteSpace(title)) Title = title.Trim();
        Body = body;
        State = state;
        if (!string.IsNullOrWhiteSpace(sourceBranch)) SourceBranch = sourceBranch.Trim();
        if (!string.IsNullOrWhiteSpace(targetBranch)) TargetBranch = targetBranch.Trim();
        if (!string.IsNullOrWhiteSpace(authorLogin)) AuthorLogin = authorLogin.Trim();
        if (!string.IsNullOrWhiteSpace(htmlUrl)) HtmlUrl = htmlUrl.Trim();
        if (!string.IsNullOrWhiteSpace(authorAvatarUrl)) AuthorAvatarUrl = authorAvatarUrl;
        if (!string.IsNullOrWhiteSpace(reviewStatus)) ReviewStatus = reviewStatus;
        if (!string.IsNullOrWhiteSpace(checkStatus)) CheckStatus = checkStatus;

        if (commitsCount > 0) CommitsCount = commitsCount;
        if (additions >= 0) Additions = additions;
        if (deletions >= 0) Deletions = deletions;

        MergedAtUtc = mergedAtUtc ?? MergedAtUtc;
        ClosedAtUtc = closedAtUtc ?? ClosedAtUtc;
        LastSyncedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = LastSyncedAtUtc;

        return (stateChanged, reviewChanged, checkChanged);
    }

    public void UpdateReviewStatus(string reviewStatus)
    {
        ReviewStatus = reviewStatus;
        LastSyncedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = LastSyncedAtUtc;
    }

    public void UpdateCheckStatus(string checkStatus)
    {
        CheckStatus = checkStatus;
        LastSyncedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = LastSyncedAtUtc;
    }
}
