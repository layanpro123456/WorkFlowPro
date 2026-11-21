namespace WorkFlowPro.Domain.Constants;

public static class ApprovalRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    };

    public static bool IsValid(string? status) =>
        status != null && All.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static bool IsTerminal(string? status) =>
        status == Approved || status == Rejected || status == Cancelled;
}
