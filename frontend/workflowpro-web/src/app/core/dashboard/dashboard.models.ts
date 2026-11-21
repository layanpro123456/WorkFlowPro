import { ApprovalRequestSummary } from '../approvals/approval.models';
import { TaskSummary } from '../tasks/task.models';
import { TicketSummary } from '../tickets/ticket.models';

export interface DashboardSummary {
  assignedTasksCount: number;
  overdueTasksCount: number;
  openTicketsCount: number;
  pendingApprovalsCount: number;
  unreadNotificationsCount: number;
  managedProjectsCount?: number | null;
  recentTasks: TaskSummary[];
  recentTickets: TicketSummary[];
  recentApprovals: ApprovalRequestSummary[];
}
