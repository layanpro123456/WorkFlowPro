namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record TicketSummaryDto(
    Guid Id,
    long TicketNumber,
    string Subject,
    string Category,
    string Priority,
    string Status,
    Guid RequesterId,
    string RequesterName,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? RelatedProjectId,
    string? RelatedProjectCode,
    string? RelatedProjectName,
    int MessageCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc
);
