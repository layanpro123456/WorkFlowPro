using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Application.Features.Approvals.Services;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Route("api/v1/approvals")]
[Authorize]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalAppService _approvalService;

    public ApprovalsController(IApprovalAppService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ApprovalRequestSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] bool? inboxOnly,
        [FromQuery] bool? myRequestsOnly,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _approvalService.ListRequestsAsync(
            status,
            inboxOnly,
            myRequestsOnly,
            search,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApprovalRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await _approvalService.GetRequestByIdAsync(id, cancellationToken);
        return Ok(detail);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApprovalRequestSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] SubmitApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var created = await _approvalService.SubmitRequestAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/decisions")]
    [ProducesResponseType(typeof(ApprovalRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordDecision(
        Guid id,
        [FromBody] RecordDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _approvalService.RecordDecisionAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApprovalRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _approvalService.CancelRequestAsync(id, request, cancellationToken);
        return Ok(updated);
    }
}
