using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class SupportTicket : BaseEntity
{
    private readonly List<TicketMessage> _messages = new();
    private readonly List<TicketStatusHistory> _statusHistory = new();

    public long TicketNumber { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public Guid? RelatedProjectId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = TicketCategories.General;
    public string Priority { get; private set; } = TicketPriorities.Normal;
    public string Status { get; private set; } = TicketStatuses.New;
    public DateTime? ResolvedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    public Project? RelatedProject { get; private set; }
    public IReadOnlyCollection<TicketMessage> Messages => _messages.AsReadOnly();
    public IReadOnlyCollection<TicketStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    private SupportTicket() { } // For EF Core

    public SupportTicket(
        Guid requesterId,
        string subject,
        string description,
        string category,
        string priority = TicketPriorities.Normal,
        Guid? relatedProjectId = null)
    {
        if (requesterId == Guid.Empty) throw new ArgumentException("RequesterId cannot be empty.", nameof(requesterId));
        if (string.IsNullOrWhiteSpace(subject)) throw new DomainException("Ticket subject is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Ticket description is required.");

        ValidateSubject(subject);
        ValidateCategory(category);
        ValidatePriority(priority);

        Id = Guid.NewGuid();
        RequesterId = requesterId;
        Subject = subject.Trim();
        Description = description.Trim();
        Category = category;
        Priority = priority;
        RelatedProjectId = relatedProjectId;
        Status = TicketStatuses.New;
        CreatedAtUtc = DateTime.UtcNow;

        _statusHistory.Add(new TicketStatusHistory(
            ticketId: Id,
            fromStatus: null,
            toStatus: Status,
            changedById: requesterId,
            changedAtUtc: CreatedAtUtc,
            comment: "Ticket created."
        ));
    }

    public TicketStatusHistory? Assign(Guid assigneeId, Guid assignedById, string? comment = null)
    {
        if (assigneeId == Guid.Empty) throw new ArgumentException("AssigneeId cannot be empty.", nameof(assigneeId));
        if (assignedById == Guid.Empty) throw new ArgumentException("AssignedById cannot be empty.", nameof(assignedById));

        AssigneeId = assigneeId;
        UpdatedAtUtc = DateTime.UtcNow;

        TicketStatusHistory? history = null;
        if (Status == TicketStatuses.New)
        {
            var previousStatus = Status;
            Status = TicketStatuses.Assigned;
            history = new TicketStatusHistory(
                ticketId: Id,
                fromStatus: previousStatus,
                toStatus: Status,
                changedById: assignedById,
                changedAtUtc: UpdatedAtUtc.Value,
                comment: comment ?? "Ticket assigned."
            );
            _statusHistory.Add(history);
        }

        return history;
    }

    public TicketStatusHistory ChangeStatus(string newStatus, Guid changedById, string? comment = null)
    {
        if (changedById == Guid.Empty) throw new ArgumentException("ChangedById cannot be empty.", nameof(changedById));

        TicketStatusTransitions.EnsureValidTransition(Status, newStatus);

        var previousStatus = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        if (newStatus == TicketStatuses.Resolved)
        {
            ResolvedAtUtc = UpdatedAtUtc;
        }
        else if (newStatus == TicketStatuses.Closed)
        {
            ClosedAtUtc = UpdatedAtUtc;
            if (!ResolvedAtUtc.HasValue)
            {
                ResolvedAtUtc = UpdatedAtUtc;
            }
        }
        else if (newStatus == TicketStatuses.InProgress)
        {
            // If reopened from Resolved or Closed
            ResolvedAtUtc = null;
            ClosedAtUtc = null;
        }

        var history = new TicketStatusHistory(
            ticketId: Id,
            fromStatus: previousStatus,
            toStatus: newStatus,
            changedById: changedById,
            changedAtUtc: UpdatedAtUtc.Value,
            comment: comment
        );
        _statusHistory.Add(history);
        return history;
    }

    public TicketMessage AddMessage(Guid authorId, string body, bool isInternalNote)
    {
        if (authorId == Guid.Empty) throw new ArgumentException("AuthorId cannot be empty.", nameof(authorId));
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("Message body is required.");

        var message = new TicketMessage(Id, authorId, body, isInternalNote);
        _messages.Add(message);
        UpdatedAtUtc = DateTime.UtcNow;
        return message;
    }

    private static void ValidateSubject(string subject)
    {
        if (subject.Trim().Length > 200)
        {
            throw new DomainException("Ticket subject cannot exceed 200 characters.");
        }
    }

    private static void ValidateCategory(string category)
    {
        if (!TicketCategories.All.Contains(category, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Invalid category '{category}'. Allowed categories: {string.Join(", ", TicketCategories.All)}");
        }
    }

    private static void ValidatePriority(string priority)
    {
        if (!TicketPriorities.All.Contains(priority, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Invalid priority '{priority}'. Allowed values: {string.Join(", ", TicketPriorities.All)}");
        }
    }
}
