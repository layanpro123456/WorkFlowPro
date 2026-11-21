using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Application.Features.Tasks.Services;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class TasksController : ControllerBase
{
    private readonly ITaskAppService _taskService;

    public TasksController(ITaskAppService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet("projects/{projectId:guid}/tasks")]
    [ProducesResponseType(typeof(PagedResult<TaskSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> ListProjectTasks(
        [FromRoute] Guid projectId,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] Guid? assigneeId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _taskService.GetProjectTasksAsync(
            projectId, status, priority, assigneeId, search, page, pageSize, cancellationToken);

        return Ok(result);
    }

    [HttpPost("projects/{projectId:guid}/tasks")]
    [ProducesResponseType(typeof(TaskSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskSummaryDto>> Create(
        [FromRoute] Guid projectId,
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.CreateAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { taskId = result.Id }, result);
    }

    [HttpGet("tasks/{taskId:guid}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDetailDto>> GetById(
        [FromRoute] Guid taskId,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.GetByIdAsync(taskId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("tasks/{taskId:guid}")]
    [ProducesResponseType(typeof(TaskSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskSummaryDto>> Update(
        [FromRoute] Guid taskId,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.UpdateAsync(taskId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("tasks/{taskId:guid}/status")]
    [HttpPut("tasks/{taskId:guid}/status")]
    [ProducesResponseType(typeof(TaskSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskSummaryDto>> ChangeStatus(
        [FromRoute] Guid taskId,
        [FromBody] ChangeTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.ChangeStatusAsync(taskId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(TaskCommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskCommentDto>> AddComment(
        [FromRoute] Guid taskId,
        [FromBody] AddTaskCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.AddCommentAsync(taskId, request, cancellationToken);
        return CreatedAtAction(nameof(GetComments), new { taskId }, result);
    }

    [HttpGet("tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskCommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskCommentDto>>> GetComments(
        [FromRoute] Guid taskId,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.GetCommentsAsync(taskId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("tasks/{taskId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskStatusHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskStatusHistoryDto>>> GetHistory(
        [FromRoute] Guid taskId,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.GetHistoryAsync(taskId, cancellationToken);
        return Ok(result);
    }
}
