namespace WorkFlowPro.Domain.Constants;

public static class TicketCategories
{
    public const string Technical = "Technical";
    public const string Billing = "Billing";
    public const string General = "General";
    public const string Hardware = "Hardware";
    public const string Access = "Access";
    public const string BugReport = "BugReport";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Technical,
        Billing,
        General,
        Hardware,
        Access,
        BugReport
    };

    public static bool IsValid(string? category) =>
        category != null && All.Contains(category, StringComparer.OrdinalIgnoreCase);
}
