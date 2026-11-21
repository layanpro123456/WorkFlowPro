namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record TaskCommentDto(
    Guid Id,
    Guid TaskId,
    Guid AuthorId,
    string AuthorName,
    string Body,
    DateTime CreatedAtUtc
);
