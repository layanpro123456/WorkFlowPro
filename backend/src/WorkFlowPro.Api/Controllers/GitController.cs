using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Features.Git.Dtos;
using WorkFlowPro.Application.Features.Git.Services;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class GitController : ControllerBase
{
    private readonly IGitIntegrationAppService _gitService;

    public GitController(IGitIntegrationAppService gitService)
    {
        _gitService = gitService;
    }

    [HttpGet("projects/{projectId:guid}/git/repositories")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectGitRepositoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectGitRepositoryDto>>> ListProjectRepositories(
        [FromRoute] Guid projectId,
        CancellationToken cancellationToken)
    {
        var repos = await _gitService.GetProjectRepositoriesAsync(projectId, cancellationToken);
        return Ok(repos);
    }

    [HttpPost("projects/{projectId:guid}/git/repositories")]
    [ProducesResponseType(typeof(ConnectGitRepositoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectGitRepositoryResponse>> ConnectRepository(
        [FromRoute] Guid projectId,
        [FromBody] ConnectGitRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _gitService.ConnectRepositoryAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(ListProjectRepositories), new { projectId }, response);
    }

    [HttpPut("projects/{projectId:guid}/git/repositories/{repositoryId:guid}")]
    [ProducesResponseType(typeof(ProjectGitRepositoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectGitRepositoryDto>> UpdateRepository(
        [FromRoute] Guid projectId,
        [FromRoute] Guid repositoryId,
        [FromBody] UpdateGitRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        var repo = await _gitService.UpdateRepositoryAsync(projectId, repositoryId, request, cancellationToken);
        return Ok(repo);
    }

    [HttpDelete("projects/{projectId:guid}/git/repositories/{repositoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisconnectRepository(
        [FromRoute] Guid projectId,
        [FromRoute] Guid repositoryId,
        CancellationToken cancellationToken)
    {
        await _gitService.DisconnectRepositoryAsync(projectId, repositoryId, cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{projectId:guid}/git/repositories/{repositoryId:guid}/test")]
    [ProducesResponseType(typeof(GitConnectionTestResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GitConnectionTestResultDto>> TestRepositoryConnection(
        [FromRoute] Guid projectId,
        [FromRoute] Guid repositoryId,
        CancellationToken cancellationToken)
    {
        var result = await _gitService.TestRepositoryConnectionAsync(projectId, repositoryId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("projects/{projectId:guid}/git/repositories/{repositoryId:guid}/sync")]
    [ProducesResponseType(typeof(GitSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GitSyncResultDto>> SyncPullRequests(
        [FromRoute] Guid projectId,
        [FromRoute] Guid repositoryId,
        CancellationToken cancellationToken)
    {
        var result = await _gitService.SyncRepositoryPullRequestsAsync(projectId, repositoryId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("projects/{projectId:guid}/git/pull-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectPullRequestSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectPullRequestSummaryDto>>> ListProjectPullRequests(
        [FromRoute] Guid projectId,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var prs = await _gitService.GetProjectPullRequestsAsync(projectId, state, cancellationToken);
        return Ok(prs);
    }

    [HttpGet("tasks/{taskId:guid}/git")]
    [ProducesResponseType(typeof(TaskGitDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskGitDetailDto>> GetTaskGitDetails(
        [FromRoute] Guid taskId,
        CancellationToken cancellationToken)
    {
        var detail = await _gitService.GetTaskGitDetailsAsync(taskId, cancellationToken);
        return Ok(detail);
    }

    [HttpPost("tasks/{taskId:guid}/git/branches")]
    [ProducesResponseType(typeof(TaskGitBranchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskGitBranchDto>> CreateTaskBranch(
        [FromRoute] Guid taskId,
        [FromBody] CreateTaskBranchRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _gitService.CreateTaskBranchAsync(taskId, request, cancellationToken);
        return CreatedAtAction(nameof(GetTaskGitDetails), new { taskId }, branch);
    }

    [HttpPost("tasks/{taskId:guid}/git/pull-requests/link")]
    [ProducesResponseType(typeof(TaskPullRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskPullRequestDto>> LinkPullRequest(
        [FromRoute] Guid taskId,
        [FromBody] LinkPullRequestRequest request,
        CancellationToken cancellationToken)
    {
        var pr = await _gitService.LinkPullRequestToTaskAsync(taskId, request, cancellationToken);
        return CreatedAtAction(nameof(GetTaskGitDetails), new { taskId }, pr);
    }

    [HttpDelete("tasks/{taskId:guid}/git/pull-requests/{pullRequestId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkPullRequest(
        [FromRoute] Guid taskId,
        [FromRoute] Guid pullRequestId,
        CancellationToken cancellationToken)
    {
        await _gitService.UnlinkPullRequestFromTaskAsync(taskId, pullRequestId, cancellationToken);
        return NoContent();
    }
}
