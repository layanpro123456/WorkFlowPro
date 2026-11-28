namespace WorkFlowPro.Application.Features.Git.Dtos;

public record ProjectGitRepositoryDto(
    Guid Id,
    Guid ProjectId,
    string Provider,
    string RepositoryOwner,
    string RepositoryName,
    string RepositoryFullName,
    string ApiBaseUrl,
    string WebBaseUrl,
    string DefaultBranch,
    bool HasAccessToken,
    bool HasWebhookSecret,
    string WebhookPath,
    bool IsDefault,
    bool IsActive,
    bool AutoLinkEnabled,
    bool AutoTransitionEnabled,
    bool UseInReviewColumn,
    DateTime? LastSyncedAtUtc,
    string? LastSyncError,
    DateTime CreatedAtUtc
);

public record ConnectGitRepositoryRequest(
    string Provider,
    string RepositoryOwner,
    string RepositoryName,
    string? DefaultBranch,
    string? ApiBaseUrl,
    string? WebBaseUrl,
    string? AccessToken,
    string? WebhookSecret,
    bool IsDefault = true,
    bool AutoLinkEnabled = true,
    bool AutoTransitionEnabled = true,
    bool UseInReviewColumn = true
);

public record UpdateGitRepositoryRequest(
    string DefaultBranch,
    string? ApiBaseUrl,
    string? WebBaseUrl,
    string? AccessToken,
    string? WebhookSecret,
    bool IsDefault,
    bool IsActive,
    bool AutoLinkEnabled,
    bool AutoTransitionEnabled,
    bool UseInReviewColumn
);

public record ConnectGitRepositoryResponse(
    ProjectGitRepositoryDto Repository,
    string? GeneratedWebhookSecret
);

public record GitConnectionTestResultDto(
    bool Success,
    string Message,
    string? DetectedDefaultBranch,
    bool IsPrivate
);

public record GitSyncResultDto(
    Guid RepositoryId,
    int PullRequestsSynced,
    int TasksLinked,
    int TasksTransitioned,
    DateTime SyncedAtUtc
);

public record TaskGitBranchDto(
    Guid Id,
    Guid TaskId,
    Guid RepositoryId,
    string RepositoryFullName,
    string BranchName,
    string BaseBranch,
    string BranchUrl,
    Guid? CreatedByUserId,
    DateTime CreatedAtUtc
);

public record CreateTaskBranchRequest(
    Guid? RepositoryId = null,
    string? BranchName = null,
    string? BaseBranch = null,
    string? BranchPrefix = "feat"
);

public record TaskPullRequestDto(
    Guid Id,
    Guid RepositoryId,
    string RepositoryFullName,
    int PrNumber,
    string Title,
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
    string LinkSource,
    DateTime OpenedAtUtc,
    DateTime? MergedAtUtc,
    DateTime? ClosedAtUtc,
    DateTime LastSyncedAtUtc
);

public record ProjectPullRequestSummaryDto(
    Guid Id,
    Guid RepositoryId,
    string RepositoryFullName,
    int PrNumber,
    string Title,
    string State,
    string SourceBranch,
    string TargetBranch,
    string AuthorLogin,
    string HtmlUrl,
    string ReviewStatus,
    string CheckStatus,
    IReadOnlyList<string> LinkedTaskKeys,
    DateTime OpenedAtUtc,
    DateTime? MergedAtUtc,
    DateTime LastSyncedAtUtc
);

public record LinkPullRequestRequest(
    Guid RepositoryId,
    int PrNumber
);

public record TaskGitDetailDto(
    Guid TaskId,
    string TaskKey,
    IReadOnlyList<TaskGitBranchDto> Branches,
    IReadOnlyList<TaskPullRequestDto> PullRequests
);

public record TaskGitSummaryDto(
    int BranchCount,
    int OpenPrCount,
    int MergedPrCount,
    int? PrimaryPrNumber,
    string? PrimaryPrState,
    string? PrimaryPrReviewStatus,
    string? PrimaryPrCheckStatus,
    string? PrimaryPrUrl,
    string? PrimaryBranchName,
    string? PrimaryBranchUrl
);

public record GitWebhookProcessResultDto(
    bool Accepted,
    string Status,
    string Message,
    int TasksLinked = 0,
    int TasksTransitioned = 0
);
