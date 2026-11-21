using WorkFlowPro.Application.Common.Models;

namespace WorkFlowPro.Application.Features.Reports.Dtos;

public record TaskReportRequest(
    Guid? ProjectId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 50
);

public record TaskReportItemDto(
    Guid TaskId,
    string ProjectCode,
    string ProjectName,
    string Title,
    string Status,
    string Priority,
    string? AssigneeName,
    DateTime? DueDateUtc,
    DateTime CreatedAtUtc
);

public record TaskReportDto(
    int TotalTasks,
    int CompletedTasks,
    double CompletionRate,
    int OverdueTasks,
    IDictionary<string, int> StatusDistribution,
    IDictionary<string, int> PriorityDistribution,
    PagedResult<TaskReportItemDto> Tasks
);

public record TicketReportRequest(
    string? Category = null,
    string? Priority = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Page = 1,
    int PageSize = 50
);

public record TicketReportItemDto(
    long TicketNumber,
    string Subject,
    string Category,
    string Priority,
    string Status,
    string RequesterName,
    string? AssigneeName,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc
);

public record TicketReportDto(
    int TotalTickets,
    int ResolvedTickets,
    double ResolutionRate,
    IDictionary<string, int> CategoryDistribution,
    IDictionary<string, int> PriorityDistribution,
    IDictionary<string, int> StatusDistribution,
    PagedResult<TicketReportItemDto> Tickets
);

public record ProjectPerformanceReportDto(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string Status,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    int TotalTasks,
    int CompletedTasks,
    int OverdueTasks,
    double CompletionRate,
    int MemberCount
);

public record CsvExportResult(
    byte[] Content,
    string ContentType,
    string FileName
);
