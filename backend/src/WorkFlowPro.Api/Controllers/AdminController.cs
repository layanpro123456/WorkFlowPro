using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Admin.Dtos;
using WorkFlowPro.Application.Features.Admin.Services;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = Roles.Administrator)]
public class AdminController : ControllerBase
{
    private readonly IAdminAppService _adminAppService;

    public AdminController(IAdminAppService adminAppService)
    {
        _adminAppService = adminAppService;
    }

    [HttpGet("users")]
    [ProducesResponseType(typeof(PagedResult<AdminUserSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminUserSummaryDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminAppService.GetUsersAsync(search, role, isActive, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPost("users")]
    [ProducesResponseType(typeof(AdminUserSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminUserSummaryDto>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminAppService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUsers), new { search = result.Email }, result);
    }

    [HttpPatch("users/{id:guid}/status")]
    [ProducesResponseType(typeof(AdminUserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserSummaryDto>> UpdateUserStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminAppService.UpdateUserStatusAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("users/{id:guid}/roles")]
    [ProducesResponseType(typeof(AdminUserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserSummaryDto>> UpdateUserRoles(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminAppService.UpdateUserRolesAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _adminAppService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] Guid? actorId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filters = new AuditLogFilterParams(
            Action: action,
            EntityType: entityType,
            ActorId: actorId,
            StartDate: startDate,
            EndDate: endDate,
            Page: page,
            PageSize: pageSize
        );

        var result = await _adminAppService.GetAuditLogsAsync(filters, cancellationToken);
        return Ok(result);
    }
}
