namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record TicketMessageDto(
    Guid Id,
    Guid TicketId,
    Guid AuthorId,
    string AuthorName,
    string Body,
    bool IsInternalNote,
    DateTime CreatedAtUtc
);
