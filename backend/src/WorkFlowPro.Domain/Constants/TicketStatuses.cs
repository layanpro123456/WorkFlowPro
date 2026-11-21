namespace WorkFlowPro.Domain.Constants;

public static class TicketStatuses
{
    public const string New = "New";
    public const string Assigned = "Assigned";
    public const string InProgress = "InProgress";
    public const string WaitingForRequester = "WaitingForRequester";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> All = new[]
    {
        New,
        Assigned,
        InProgress,
        WaitingForRequester,
        Resolved,
        Closed
    };
}
