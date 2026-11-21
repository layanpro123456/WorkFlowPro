using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Application.Features.Tickets.Dtos;

namespace WorkFlowPro.Application.Features.Dashboard.Dtos;

public record DashboardSummaryDto(
    int AssignedTasksCount,
    int OverdueTasksCount,
    int OpenTicketsCount,
    int PendingApprovalsCount,
    int UnreadNotificationsCount,
    int? ManagedProjectsCount,
    IReadOnlyList<TaskSummaryDto> RecentTasks,
    IReadOnlyList<TicketSummaryDto> RecentTickets,
    IReadOnlyList<ApprovalRequestSummaryDto> RecentApprovals
);
