using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Tickets.Dtos;
using WorkFlowPro.Application.Features.Tickets.Services;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketAppService _ticketService;

    public TicketsController(ITicketAppService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TicketSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<TicketSummaryDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? category,
        [FromQuery] Guid? assigneeId,
        [FromQuery] string? search,
        [FromQuery] bool myTicketsOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _ticketService.GetPagedListAsync(
            status, priority, category, assigneeId, search, myTicketsOnly, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TicketSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TicketSummaryDto>> Create(
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/assign")]
    [Authorize(Roles = $"{Roles.SupportAgent},{Roles.Manager},{Roles.Administrator}")]
    [ProducesResponseType(typeof(TicketSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketSummaryDto>> Assign(
        [FromRoute] Guid id,
        [FromBody] AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.AssignAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(TicketSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketSummaryDto>> ChangeStatus(
        [FromRoute] Guid id,
        [FromBody] ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.ChangeStatusAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(TicketMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketMessageDto>> AddMessage(
        [FromRoute] Guid id,
        [FromBody] AddTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.AddMessageAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetMessages), new { id }, result);
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketMessageDto>>> GetMessages(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.GetMessagesAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketStatusHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketStatusHistoryDto>>> GetHistory(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.GetHistoryAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("agents")]
    [Authorize(Roles = $"{Roles.SupportAgent},{Roles.Manager},{Roles.Administrator}")]
    [ProducesResponseType(typeof(IReadOnlyList<SupportAgentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SupportAgentDto>>> GetAgents(
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.GetEligibleAssigneesAsync(cancellationToken);
        return Ok(result);
    }
}
