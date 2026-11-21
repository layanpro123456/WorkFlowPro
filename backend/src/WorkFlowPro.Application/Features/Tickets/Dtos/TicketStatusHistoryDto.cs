namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record TicketStatusHistoryDto(
    Guid Id,
    Guid TicketId,
    string? FromStatus,
    string ToStatus,
    Guid ChangedById,
    string ChangedByName,
    DateTime ChangedAtUtc,
    string? Comment
);
