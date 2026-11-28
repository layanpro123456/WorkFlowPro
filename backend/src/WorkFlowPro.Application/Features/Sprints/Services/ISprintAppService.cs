using WorkFlowPro.Application.Features.Sprints.Dtos;

namespace WorkFlowPro.Application.Features.Sprints.Services;

public interface ISprintAppService
{
    Task<IReadOnlyList<SprintDto>> GetProjectSprintsAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<SprintDto> CreateSprintAsync(
        Guid projectId,
        CreateSprintRequest request,
        CancellationToken cancellationToken);

    Task<SprintDto> UpdateSprintAsync(
        Guid sprintId,
        UpdateSprintRequest request,
        CancellationToken cancellationToken);

    Task<SprintDto> StartSprintAsync(
        Guid sprintId,
        StartSprintRequest request,
        CancellationToken cancellationToken);

    Task<CompleteSprintResultDto> CompleteSprintAsync(
        Guid sprintId,
        CompleteSprintRequest request,
        CancellationToken cancellationToken);

    Task<SprintBurndownDto> GetSprintBurndownAsync(
        Guid sprintId,
        CancellationToken cancellationToken);

    Task<VelocityChartDto> GetProjectVelocityAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BoardColumnConfigDto>> GetBoardConfigsAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BoardColumnConfigDto>> UpdateBoardConfigsAsync(
        Guid projectId,
        UpdateBoardColumnConfigsRequest request,
        CancellationToken cancellationToken);

    Task<IssueLinkDto> CreateIssueLinkAsync(
        Guid sourceTaskId,
        CreateIssueLinkRequest request,
        CancellationToken cancellationToken);

    Task DeleteIssueLinkAsync(
        Guid taskId,
        Guid linkId,
        CancellationToken cancellationToken);

    Task RecordActiveSprintSnapshotAsync(
        Guid sprintId,
        CancellationToken cancellationToken);
}
