using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Application.Features.Projects.Services;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectAppService _projectService;

    public ProjectsController(IProjectAppService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectSummaryDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectService.GetPagedListAsync(status, search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    [ProducesResponseType(typeof(ProjectSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectSummaryDto>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { projectId = result.Id }, result);
    }

    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailDto>> GetById(
        [FromRoute] Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.GetByIdAsync(projectId, cancellationToken);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Project Not Found",
                Detail = $"Project with ID '{projectId}' was not found or is inaccessible."
            });
        }
        return Ok(result);
    }

    [HttpPut("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailDto>> Update(
        [FromRoute] Guid projectId,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.UpdateAsync(projectId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{projectId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(
        [FromRoute] Guid projectId,
        CancellationToken cancellationToken)
    {
        await _projectService.ArchiveAsync(projectId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> GetMembers(
        [FromRoute] Guid projectId,
        CancellationToken cancellationToken)
    {
        var members = await _projectService.GetMembersAsync(projectId, cancellationToken);
        return Ok(members);
    }

    [HttpPost("{projectId:guid}/members")]
    [ProducesResponseType(typeof(ProjectMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectMemberDto>> AddMember(
        [FromRoute] Guid projectId,
        [FromBody] AddProjectMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = await _projectService.AddMemberAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(GetMembers), new { projectId }, member);
    }

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(
        [FromRoute] Guid projectId,
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        await _projectService.RemoveMemberAsync(projectId, userId, cancellationToken);
        return NoContent();
    }
}
