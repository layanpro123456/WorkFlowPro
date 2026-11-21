namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record TicketDetailDto(
    Guid Id,
    long TicketNumber,
    string Subject,
    string Description,
    string Category,
    string Priority,
    string Status,
    Guid RequesterId,
    string RequesterName,
    string RequesterEmail,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? RelatedProjectId,
    string? RelatedProjectCode,
    string? RelatedProjectName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc,
    IReadOnlyList<TicketMessageDto> Messages,
    IReadOnlyList<TicketStatusHistoryDto> History
);
