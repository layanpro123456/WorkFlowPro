namespace WorkFlowPro.Application.Common.Interfaces;

public record GitProviderContext(
    string ApiBaseUrl,
    string WebBaseUrl,
    string Owner,
    string RepoName,
    string? AccessToken
);

public record GitConnectionTestResult(
    bool Success,
    string Message,
    string? DefaultBranch,
    bool IsPrivate
);

public record GitRemoteBranchResult(
    string BranchName,
    string BaseBranch,
    string BranchUrl,
    string CommitSha
);

public record GitRemotePullRequest(
    long ExternalPrId,
    int PrNumber,
    string Title,
    string? Body,
    string State,
    string SourceBranch,
    string TargetBranch,
    string AuthorLogin,
    string? AuthorAvatarUrl,
    string HtmlUrl,
    string ReviewStatus,
    string CheckStatus,
    int CommitsCount,
    int Additions,
    int Deletions,
    DateTime OpenedAtUtc,
    DateTime? MergedAtUtc,
    DateTime? ClosedAtUtc
);

public record GitWebhookEvent(
    string EventType,
    string Action,
    string? RepositoryFullName,
    string? BranchName,
    string? BaseBranch,
    string? BranchUrl,
    GitRemotePullRequest? PullRequest,
    int? RelatedPrNumber,
    string? ReviewState,
    string? CheckConclusion,
    string? HeadBranch,
    string? SenderLogin
);

public interface IGitProviderClient
{
    string Provider { get; }

    Task<GitConnectionTestResult> TestConnectionAsync(
        GitProviderContext context,
        CancellationToken cancellationToken = default);

    Task<GitRemoteBranchResult> CreateBranchAsync(
        GitProviderContext context,
        string branchName,
        string baseBranch,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitRemotePullRequest>> ListPullRequestsAsync(
        GitProviderContext context,
        string state = "all",
        int maxResults = 30,
        CancellationToken cancellationToken = default);

    Task<GitRemotePullRequest?> GetPullRequestAsync(
        GitProviderContext context,
        int prNumber,
        CancellationToken cancellationToken = default);

    bool VerifyWebhookSignature(
        string payload,
        string? signatureHeader,
        string secret);

    GitWebhookEvent? ParseWebhookEvent(
        string eventType,
        string payload);
}
