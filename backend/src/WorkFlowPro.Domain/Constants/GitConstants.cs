namespace WorkFlowPro.Domain.Constants;

public static class GitProviders
{
    public const string GitHub = "GitHub";

    public static readonly IReadOnlyList<string> All = new[]
    {
        GitHub
    };

    public static bool IsSupported(string? provider) =>
        provider != null && All.Contains(provider, StringComparer.OrdinalIgnoreCase);
}

public static class PullRequestStates
{
    public const string Open = "Open";
    public const string Draft = "Draft";
    public const string Merged = "Merged";
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Open,
        Draft,
        Merged,
        Closed
    };
}

public static class PullRequestReviewStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string ChangesRequested = "ChangesRequested";
    public const string Commented = "Commented";
}

public static class PullRequestCheckStatuses
{
    public const string None = "None";
    public const string Pending = "Pending";
    public const string Success = "Success";
    public const string Failure = "Failure";
}

public static class TaskPullRequestLinkSources
{
    public const string AutoBranch = "AutoBranch";
    public const string AutoTitle = "AutoTitle";
    public const string AutoBody = "AutoBody";
    public const string Manual = "Manual";
}

public static class GitWebhookDeliveryStatuses
{
    public const string Processed = "Processed";
    public const string Ignored = "Ignored";
    public const string Failed = "Failed";
}
