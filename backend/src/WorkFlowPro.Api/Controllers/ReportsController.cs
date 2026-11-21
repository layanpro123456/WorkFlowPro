using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlowPro.Application.Features.Reports.Dtos;
using WorkFlowPro.Application.Features.Reports.Services;

namespace WorkFlowPro.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportAppService _reportService;

    public ReportsController(IReportAppService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("tasks")]
    [ProducesResponseType(typeof(TaskReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaskReport(
        [FromQuery] Guid? projectId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        var request = new TaskReportRequest(projectId, startDate, endDate, status, page, pageSize);

        if (exportCsv)
        {
            var csv = await _reportService.ExportTaskReportCsvAsync(request, cancellationToken);
            return File(csv.Content, csv.ContentType, csv.FileName);
        }

        var report = await _reportService.GetTaskReportAsync(request, cancellationToken);
        return Ok(report);
    }

    [HttpGet("tickets")]
    [ProducesResponseType(typeof(TicketReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTicketReport(
        [FromQuery] string? category,
        [FromQuery] string? priority,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        var request = new TicketReportRequest(category, priority, startDate, endDate, page, pageSize);

        if (exportCsv)
        {
            var csv = await _reportService.ExportTicketReportCsvAsync(request, cancellationToken);
            return File(csv.Content, csv.ContentType, csv.FileName);
        }

        var report = await _reportService.GetTicketReportAsync(request, cancellationToken);
        return Ok(report);
    }

    [HttpGet("projects/{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectPerformanceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectPerformance(
        Guid projectId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var report = await _reportService.GetProjectPerformanceAsync(projectId, startDate, endDate, cancellationToken);
        return Ok(report);
    }
}
