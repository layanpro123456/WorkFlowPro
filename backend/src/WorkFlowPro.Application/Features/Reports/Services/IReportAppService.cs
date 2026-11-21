using WorkFlowPro.Application.Features.Reports.Dtos;

namespace WorkFlowPro.Application.Features.Reports.Services;

public interface IReportAppService
{
    Task<TaskReportDto> GetTaskReportAsync(TaskReportRequest request, CancellationToken cancellationToken);
    Task<CsvExportResult> ExportTaskReportCsvAsync(TaskReportRequest request, CancellationToken cancellationToken);
    Task<TicketReportDto> GetTicketReportAsync(TicketReportRequest request, CancellationToken cancellationToken);
    Task<CsvExportResult> ExportTicketReportCsvAsync(TicketReportRequest request, CancellationToken cancellationToken);
    Task<ProjectPerformanceReportDto> GetProjectPerformanceAsync(Guid projectId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);
}
