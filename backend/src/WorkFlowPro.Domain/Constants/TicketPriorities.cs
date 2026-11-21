namespace WorkFlowPro.Domain.Constants;

public static class TicketPriorities
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string High = "High";
    public const string Urgent = "Urgent";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Low,
        Normal,
        High,
        Urgent
    };
}
