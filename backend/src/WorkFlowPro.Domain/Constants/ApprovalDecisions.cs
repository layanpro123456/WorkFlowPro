namespace WorkFlowPro.Domain.Constants;

public static class ApprovalDecisions
{
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Approved,
        Rejected
    };

    public static bool IsValid(string? decision) =>
        decision != null && All.Contains(decision, StringComparer.OrdinalIgnoreCase);
}
