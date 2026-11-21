using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Application.Features.Approvals.Services;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Route("api/v1/workflow-definitions")]
[Authorize]
public class WorkflowDefinitionsController : ControllerBase
{
    private readonly IApprovalAppService _approvalService;

    public WorkflowDefinitionsController(IApprovalAppService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowDefinitionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var definitions = await _approvalService.ListDefinitionsAsync(includeInactive, cancellationToken);
        return Ok(definitions);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowDefinitionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await _approvalService.GetDefinitionByIdAsync(id, cancellationToken);
        return Ok(definition);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    [ProducesResponseType(typeof(WorkflowDefinitionDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var created = await _approvalService.CreateDefinitionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    [ProducesResponseType(typeof(WorkflowDefinitionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var updated = await _approvalService.UpdateDefinitionAsync(id, request, cancellationToken);
        return Ok(updated);
    }
}
