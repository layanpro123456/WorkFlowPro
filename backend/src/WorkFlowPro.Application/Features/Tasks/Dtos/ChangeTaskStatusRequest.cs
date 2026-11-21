namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record ChangeTaskStatusRequest(
    string Status,
    int Version,
    string? Comment = null
);
