namespace WorkFlowPro.Domain.Constants;

public static class ProjectStatuses
{
    public const string Active = "Active";
    public const string OnHold = "OnHold";
    public const string Completed = "Completed";
    public const string Archived = "Archived";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Active,
        OnHold,
        Completed,
        Archived
    };
}
