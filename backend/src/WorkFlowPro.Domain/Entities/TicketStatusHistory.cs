using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class TicketStatusHistory
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = string.Empty;
    public Guid ChangedById { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Comment { get; private set; }

    public SupportTicket? Ticket { get; private set; }

    private TicketStatusHistory() { } // For EF Core

    public TicketStatusHistory(
        Guid ticketId,
        string? fromStatus,
        string toStatus,
        Guid changedById,
        DateTime changedAtUtc,
        string? comment = null)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("TicketId cannot be empty.", nameof(ticketId));
        if (changedById == Guid.Empty) throw new ArgumentException("ChangedById cannot be empty.", nameof(changedById));
        if (string.IsNullOrWhiteSpace(toStatus)) throw new DomainException("ToStatus is required.");

        Id = Guid.NewGuid();
        TicketId = ticketId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedById = changedById;
        ChangedAtUtc = changedAtUtc;
        Comment = comment?.Trim();
    }
}
