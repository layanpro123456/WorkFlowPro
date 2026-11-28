namespace WorkFlowPro.Domain.Constants;

public static class TaskStatuses
{
    public const string Backlog = "Backlog";
    public const string ToDo = "ToDo";
    public const string InProgress = "InProgress";
    public const string InReview = "InReview";
    public const string Blocked = "Blocked";
    public const string Done = "Done";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Backlog,
        ToDo,
        InProgress,
        InReview,
        Blocked,
        Done,
        Cancelled
    };

    public static bool IsValid(string? status) =>
        status != null && All.Contains(status, StringComparer.OrdinalIgnoreCase);
}
