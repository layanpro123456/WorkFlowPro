using WorkFlowPro.Application.Features.Git.Dtos;

namespace WorkFlowPro.Application.Features.Git.Services;

public interface IGitIntegrationAppService
{
    Task<IReadOnlyList<ProjectGitRepositoryDto>> GetProjectRepositoriesAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<ConnectGitRepositoryResponse> ConnectRepositoryAsync(
        Guid projectId,
        ConnectGitRepositoryRequest request,
        CancellationToken cancellationToken);

    Task<ProjectGitRepositoryDto> UpdateRepositoryAsync(
        Guid projectId,
        Guid repositoryId,
        UpdateGitRepositoryRequest request,
        CancellationToken cancellationToken);

    Task DisconnectRepositoryAsync(
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken);

    Task<GitConnectionTestResultDto> TestRepositoryConnectionAsync(
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken);

    Task<GitSyncResultDto> SyncRepositoryPullRequestsAsync(
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectPullRequestSummaryDto>> GetProjectPullRequestsAsync(
        Guid projectId,
        string? state,
        CancellationToken cancellationToken);

    Task<TaskGitDetailDto> GetTaskGitDetailsAsync(
        Guid taskId,
        CancellationToken cancellationToken);

    Task<TaskGitBranchDto> CreateTaskBranchAsync(
        Guid taskId,
        CreateTaskBranchRequest request,
        CancellationToken cancellationToken);

    Task<TaskPullRequestDto> LinkPullRequestToTaskAsync(
        Guid taskId,
        LinkPullRequestRequest request,
        CancellationToken cancellationToken);

    Task UnlinkPullRequestFromTaskAsync(
        Guid taskId,
        Guid pullRequestId,
        CancellationToken cancellationToken);

    Task<GitWebhookProcessResultDto> ProcessWebhookAsync(
        string provider,
        Guid repositoryId,
        string? deliveryId,
        string? eventType,
        string? signatureHeader,
        string rawPayload,
        CancellationToken cancellationToken);
}
