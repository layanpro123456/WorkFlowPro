namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record TaskStatusHistoryDto(
    Guid Id,
    Guid TaskId,
    string? FromStatus,
    string ToStatus,
    Guid ChangedById,
    string ChangedByName,
    DateTime ChangedAtUtc,
    string? Comment
);
