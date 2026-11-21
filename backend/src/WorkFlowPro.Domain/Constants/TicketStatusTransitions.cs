using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Constants;

/// <summary>
/// Enforces business transition rules for service desk support tickets:
/// - New                 -> Assigned, InProgress, Closed
/// - Assigned            -> InProgress, WaitingForRequester, Resolved, Closed
/// - InProgress          -> WaitingForRequester, Resolved, Closed
/// - WaitingForRequester -> InProgress, Resolved, Closed
/// - Resolved            -> Closed, InProgress
/// - Closed              -> InProgress
/// </summary>
public static class TicketStatusTransitions
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [TicketStatuses.New] = new(StringComparer.OrdinalIgnoreCase)
        {
            TicketStatuses.Assigned,
            TicketStatuses.InProgress,
            TicketStatuses.Closed
        },
        [TicketStatuses.Assigned] = new(StringComparer.OrdinalIgnoreCase)
        {
            TicketStatuses.InProgress,
            TicketStatuses.WaitingForRequester,
            TicketStatuses.Resolved,
            TicketStatuses.Closed
        },
        [TicketStatuses.InProgress] = new(StringComparer.OrdinalIgnoreCase)
        {
            TicketStatuses.WaitingForRequester,
            TicketStatuses.Resolved,
            TicketStatuses.Closed
        },
        [TicketStatuses.WaitingForRequester] = new(StringComparer.OrdinalIgnoreCase)
        {
            TicketStatuses.InProgress,
            TicketStatuses.Resolved,
            TicketStatuses.Closed
        },
        [TicketStatuses.Resolved] = new(StringComparer.OrdinalIgnoreCase)
        {
            TicketStatuses.Closed,
            TicketStatuses.InProgress
        },
        [TicketStatuses.Closed] = new(StringComparer.OrdinalIgnoreCase)
        {
            TicketStatuses.InProgress
        }
    };

    public static bool IsValidTransition(string currentStatus, string nextStatus)
    {
        if (string.IsNullOrWhiteSpace(currentStatus) || string.IsNullOrWhiteSpace(nextStatus))
        {
            return false;
        }

        if (string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (AllowedTransitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(nextStatus))
        {
            return true;
        }

        return false;
    }

    public static void EnsureValidTransition(string currentStatus, string nextStatus)
    {
        if (!TicketStatuses.All.Contains(nextStatus, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Invalid target status '{nextStatus}'. Allowed statuses: {string.Join(", ", TicketStatuses.All)}");
        }

        if (string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"Ticket is already in status '{currentStatus}'.");
        }

        if (!IsValidTransition(currentStatus, nextStatus))
        {
            throw new DomainException($"Invalid status transition from '{currentStatus}' to '{nextStatus}'.");
        }
    }

    public static IReadOnlyCollection<string> GetAllowedNextStatuses(string currentStatus)
    {
        if (AllowedTransitions.TryGetValue(currentStatus, out var allowed))
        {
            return allowed;
        }

        return Array.Empty<string>();
    }
}
