using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Constants;

/// <summary>
/// Enforces business transition rules for tasks:
/// - Backlog    -> ToDo, Cancelled
/// - ToDo       -> InProgress, Backlog, Cancelled
/// - InProgress -> Blocked, Done, ToDo, Cancelled
/// - Blocked    -> InProgress, ToDo, Cancelled
/// - Done       -> InProgress, ToDo
/// - Cancelled  -> Backlog, ToDo
/// </summary>
public static class TaskStatusTransitions
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [TaskStatuses.Backlog] = new(StringComparer.OrdinalIgnoreCase) { TaskStatuses.ToDo, TaskStatuses.Cancelled },
        [TaskStatuses.ToDo] = new(StringComparer.OrdinalIgnoreCase) { TaskStatuses.InProgress, TaskStatuses.Backlog, TaskStatuses.Cancelled },
        [TaskStatuses.InProgress] = new(StringComparer.OrdinalIgnoreCase) { TaskStatuses.Blocked, TaskStatuses.Done, TaskStatuses.ToDo, TaskStatuses.Cancelled },
        [TaskStatuses.Blocked] = new(StringComparer.OrdinalIgnoreCase) { TaskStatuses.InProgress, TaskStatuses.ToDo, TaskStatuses.Cancelled },
        [TaskStatuses.Done] = new(StringComparer.OrdinalIgnoreCase) { TaskStatuses.InProgress, TaskStatuses.ToDo },
        [TaskStatuses.Cancelled] = new(StringComparer.OrdinalIgnoreCase) { TaskStatuses.Backlog, TaskStatuses.ToDo }
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
        if (!TaskStatuses.All.Contains(nextStatus, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Invalid target status '{nextStatus}'. Allowed statuses: {string.Join(", ", TaskStatuses.All)}");
        }

        if (string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"Task is already in status '{currentStatus}'.");
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
