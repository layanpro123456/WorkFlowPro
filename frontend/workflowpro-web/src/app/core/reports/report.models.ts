import { PagedResult } from '../projects/project.models';

export interface TaskReportItem {
  taskId: string;
  projectCode: string;
  projectName: string;
  title: string;
  status: string;
  priority: string;
  assigneeName?: string | null;
  dueDateUtc?: string | null;
  createdAtUtc: string;
}

export interface TaskReport {
  totalTasks: number;
  completedTasks: number;
  completionRate: number;
  overdueTasks: number;
  statusDistribution: Record<string, number>;
  priorityDistribution: Record<string, number>;
  tasks: PagedResult<TaskReportItem>;
}

export interface TicketReportItem {
  ticketNumber: number;
  subject: string;
  category: string;
  priority: string;
  status: string;
  requesterName: string;
  assigneeName?: string | null;
  createdAtUtc: string;
  resolvedAtUtc?: string | null;
}

export interface TicketReport {
  totalTickets: number;
  resolvedTickets: number;
  resolutionRate: number;
  categoryDistribution: Record<string, number>;
  priorityDistribution: Record<string, number>;
  statusDistribution: Record<string, number>;
  tickets: PagedResult<TicketReportItem>;
}

export interface ProjectPerformanceReport {
  projectId: string;
  projectCode: string;
  projectName: string;
  status: string;
  startDate?: string | null;
  targetDate?: string | null;
  totalTasks: number;
  completedTasks: number;
  overdueTasks: number;
  completionRate: number;
  memberCount: number;
}

export interface TaskReportFilterParams {
  projectId?: string;
  startDate?: string;
  endDate?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface TicketReportFilterParams {
  category?: string;
  priority?: string;
  startDate?: string;
  endDate?: string;
  page?: number;
  pageSize?: number;
}
