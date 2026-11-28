namespace WorkFlowPro.Domain.Constants;

public static class IssueTypes
{
    public const string Epic = "Epic";
    public const string Story = "Story";
    public const string Task = "Task";
    public const string Bug = "Bug";
    public const string Subtask = "Subtask";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Epic,
        Story,
        Task,
        Bug,
        Subtask
    };

    public static bool IsValid(string? issueType) =>
        issueType != null && All.Contains(issueType, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? issueType) =>
        All.FirstOrDefault(t => string.Equals(t, issueType?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? Task;
}

public static class IssueLinkTypes
{
    public const string Blocks = "Blocks";
    public const string RelatesTo = "RelatesTo";
    public const string Duplicates = "Duplicates";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Blocks,
        RelatesTo,
        Duplicates
    };

    public static bool IsValid(string? linkType) =>
        linkType != null && All.Contains(linkType, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? linkType) =>
        All.FirstOrDefault(t => string.Equals(t, linkType?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? RelatesTo;

    public static string GetInverseLabel(string linkType) =>
        linkType switch
        {
            Blocks => "is blocked by",
            Duplicates => "is duplicated by",
            _ => "relates to"
        };
}

public static class SprintStatuses
{
    public const string Planned = "Planned";
    public const string Active = "Active";
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Planned,
        Active,
        Closed
    };

    public static bool IsValid(string? status) =>
        status != null && All.Contains(status, StringComparer.OrdinalIgnoreCase);
}
