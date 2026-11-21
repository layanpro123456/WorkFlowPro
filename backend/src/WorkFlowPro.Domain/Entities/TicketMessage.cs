using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class TicketMessage
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsInternalNote { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public SupportTicket? Ticket { get; private set; }

    private TicketMessage() { } // For EF Core

    public TicketMessage(Guid ticketId, Guid authorId, string body, bool isInternalNote)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("TicketId cannot be empty.", nameof(ticketId));
        if (authorId == Guid.Empty) throw new ArgumentException("AuthorId cannot be empty.", nameof(authorId));
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("Message body is required.");

        Id = Guid.NewGuid();
        TicketId = ticketId;
        AuthorId = authorId;
        Body = body.Trim();
        IsInternalNote = isInternalNote;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
