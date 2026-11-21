namespace WorkFlowPro.Domain.Constants;

public static class ApprovalStepStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Skipped = "Skipped";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending,
        Approved,
        Rejected,
        Skipped
    };

    public static bool IsValid(string? status) =>
        status != null && All.Contains(status, StringComparer.OrdinalIgnoreCase);
}
