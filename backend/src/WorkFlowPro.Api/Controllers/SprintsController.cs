using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Features.Sprints.Dtos;
using WorkFlowPro.Application.Features.Sprints.Services;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class SprintsController : ControllerBase
{
    private readonly ISprintAppService _sprintService;

    public SprintsController(ISprintAppService sprintService)
    {
        _sprintService = sprintService;
    }

    [HttpGet("projects/{projectId:guid}/sprints")]
    [ProducesResponseType(typeof(IReadOnlyList<SprintDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SprintDto>>> ListProjectSprints(
        [FromRoute] Guid projectId,
        CancellationToken cancellationToken)
    {
        var sprints = await _sprintService.GetProjectSprintsAsync(projectId, cancellationToken);
        return Ok(sprints);
    }

    [HttpPost("projects/{projectId:guid}/sprints")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintDto>> CreateSprint(
        [FromRoute] Guid projectId,
        [FromBody] CreateSprintRequest request,
        CancellationToken cancellationToken)
    {
        var sprint = await _sprintService.CreateSprintAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(ListProjectSprints), new { projectId }, sprint);
    }

    [HttpPut("sprints/{sprintId:guid}")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintDto>> UpdateSprint(
        [FromRoute] Guid sprintId,
        [FromBody] UpdateSprintRequest request,
        CancellationToken cancellationToken)
    {
        var sprint = await _sprintService.UpdateSprintAsync(sprintId, request, cancellationToken);
        return Ok(sprint);
    }

    [HttpPost("sprints/{sprintId:guid}/start")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintDto>> StartSprint(
        [FromRoute] Guid sprintId,
        [FromBody] StartSprintRequest request,
        CancellationToken cancellationToken)
    {
        var sprint = await _sprintService.StartSprintAsync(sprintId, request, cancellationToken);
        return Ok(sprint);
    }

    [HttpPost("sprints/{sprintId:guid}/complete")]
    [ProducesResponseType(typeof(CompleteSprintResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompleteSprintResultDto>> CompleteSprint(
        [FromRoute] Guid sprintId,
        [FromBody] CompleteSprintRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sprintService.CompleteSprintAsync(sprintId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("sprints/{sprintId:guid}/burndown")]
    [ProducesResponseType(typeof(SprintBurndownDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintBurndownDto>> GetBurndown(
        [FromRoute] Guid sprintId,
        CancellationToken cancellationToken)
    {
        var burndown = await _sprintService.GetSprintBurndownAsync(sprintId, cancellationToken);
        return Ok(burndown);
    }

    [HttpGet("projects/{projectId:guid}/velocity")]
    [ProducesResponseType(typeof(VelocityChartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VelocityChartDto>> GetVelocity(
        [FromRoute] Guid projectId,
        CancellationToken cancellationToken)
    {
        var velocity = await _sprintService.GetProjectVelocityAsync(projectId, cancellationToken);
        return Ok(velocity);
    }

    [HttpGet("projects/{projectId:guid}/board-config")]
    [ProducesResponseType(typeof(IReadOnlyList<BoardColumnConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BoardColumnConfigDto>>> GetBoardConfigs(
        [FromRoute] Guid projectId,
        CancellationToken cancellationToken)
    {
        var configs = await _sprintService.GetBoardConfigsAsync(projectId, cancellationToken);
        return Ok(configs);
    }

    [HttpPut("projects/{projectId:guid}/board-config")]
    [ProducesResponseType(typeof(IReadOnlyList<BoardColumnConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BoardColumnConfigDto>>> UpdateBoardConfigs(
        [FromRoute] Guid projectId,
        [FromBody] UpdateBoardColumnConfigsRequest request,
        CancellationToken cancellationToken)
    {
        var configs = await _sprintService.UpdateBoardConfigsAsync(projectId, request, cancellationToken);
        return Ok(configs);
    }
}
