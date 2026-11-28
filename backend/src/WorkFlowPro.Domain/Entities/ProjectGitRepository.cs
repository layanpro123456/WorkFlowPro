using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class ProjectGitRepository : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public string Provider { get; private set; } = GitProviders.GitHub;
    public string RepositoryOwner { get; private set; } = string.Empty;
    public string RepositoryName { get; private set; } = string.Empty;
    public string RepositoryFullName { get; private set; } = string.Empty;
    public string ApiBaseUrl { get; private set; } = "https://api.github.com";
    public string WebBaseUrl { get; private set; } = "https://github.com";
    public string DefaultBranch { get; private set; } = "main";

    public string? EncryptedAccessToken { get; private set; }
    public string? EncryptedWebhookSecret { get; private set; }

    public bool IsDefault { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public bool AutoLinkEnabled { get; private set; } = true;
    public bool AutoTransitionEnabled { get; private set; } = true;
    public bool UseInReviewColumn { get; private set; } = true;

    public DateTime? LastSyncedAtUtc { get; private set; }
    public string? LastSyncError { get; private set; }
    public Guid CreatedById { get; private set; }

    public Project? Project { get; private set; }

    public bool HasAccessToken => !string.IsNullOrWhiteSpace(EncryptedAccessToken);
    public bool HasWebhookSecret => !string.IsNullOrWhiteSpace(EncryptedWebhookSecret);

    private ProjectGitRepository() { } // For EF Core

    public ProjectGitRepository(
        Guid projectId,
        string provider,
        string repositoryOwner,
        string repositoryName,
        string defaultBranch,
        Guid createdById,
        string? apiBaseUrl = null,
        string? webBaseUrl = null,
        bool isDefault = true,
        bool autoLinkEnabled = true,
        bool autoTransitionEnabled = true,
        bool useInReviewColumn = true)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        if (createdById == Guid.Empty) throw new ArgumentException("CreatedById cannot be empty.", nameof(createdById));
        if (!GitProviders.IsSupported(provider))
        {
            throw new DomainException($"Unsupported Git provider '{provider}'. Supported: {string.Join(", ", GitProviders.All)}");
        }
        if (string.IsNullOrWhiteSpace(repositoryOwner)) throw new DomainException("Repository owner is required.");
        if (string.IsNullOrWhiteSpace(repositoryName)) throw new DomainException("Repository name is required.");
        if (string.IsNullOrWhiteSpace(defaultBranch)) throw new DomainException("Default branch is required.");

        Id = Guid.NewGuid();
        ProjectId = projectId;
        Provider = GitProviders.GitHub;
        RepositoryOwner = repositoryOwner.Trim();
        RepositoryName = repositoryName.Trim();
        RepositoryFullName = $"{RepositoryOwner}/{RepositoryName}";
        DefaultBranch = defaultBranch.Trim();
        ApiBaseUrl = NormalizeBaseUrl(apiBaseUrl, "https://api.github.com");
        WebBaseUrl = NormalizeBaseUrl(webBaseUrl, "https://github.com");
        IsDefault = isDefault;
        IsActive = true;
        AutoLinkEnabled = autoLinkEnabled;
        AutoTransitionEnabled = autoTransitionEnabled;
        UseInReviewColumn = useInReviewColumn;
        CreatedById = createdById;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateConfiguration(
        string defaultBranch,
        string? apiBaseUrl,
        string? webBaseUrl,
        bool isDefault,
        bool isActive,
        bool autoLinkEnabled,
        bool autoTransitionEnabled,
        bool useInReviewColumn)
    {
        if (string.IsNullOrWhiteSpace(defaultBranch)) throw new DomainException("Default branch is required.");

        DefaultBranch = defaultBranch.Trim();
        ApiBaseUrl = NormalizeBaseUrl(apiBaseUrl, ApiBaseUrl);
        WebBaseUrl = NormalizeBaseUrl(webBaseUrl, WebBaseUrl);
        IsDefault = isDefault;
        IsActive = isActive;
        AutoLinkEnabled = autoLinkEnabled;
        AutoTransitionEnabled = autoTransitionEnabled;
        UseInReviewColumn = useInReviewColumn;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetCredentials(string? encryptedAccessToken, string? encryptedWebhookSecret)
    {
        if (encryptedAccessToken != null)
        {
            EncryptedAccessToken = encryptedAccessToken;
        }
        if (encryptedWebhookSecret != null)
        {
            EncryptedWebhookSecret = encryptedWebhookSecret;
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearAccessToken()
    {
        EncryptedAccessToken = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordSyncSuccess(DateTime utcNow)
    {
        LastSyncedAtUtc = utcNow;
        LastSyncError = null;
        UpdatedAtUtc = utcNow;
    }

    public void RecordSyncFailure(string errorMessage, DateTime utcNow)
    {
        LastSyncError = errorMessage.Length > 1000 ? errorMessage[..1000] : errorMessage;
        UpdatedAtUtc = utcNow;
    }

    public string BuildBranchWebUrl(string branchName) =>
        $"{WebBaseUrl}/{RepositoryFullName}/tree/{Uri.EscapeDataString(branchName).Replace("%2F", "/")}";

    private static string NormalizeBaseUrl(string? url, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(url) ? fallback : url.Trim();
        return candidate.TrimEnd('/');
    }
}
