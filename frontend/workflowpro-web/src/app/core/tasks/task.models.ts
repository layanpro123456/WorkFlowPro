export type TaskStatus = 'Backlog' | 'ToDo' | 'InProgress' | 'Blocked' | 'Done' | 'Cancelled';
export type TaskPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export const TASK_STATUSES: TaskStatus[] = [
  'Backlog',
  'ToDo',
  'InProgress',
  'Blocked',
  'Done',
  'Cancelled'
];

export const TASK_PRIORITIES: TaskPriority[] = [
  'Low',
  'Normal',
  'High',
  'Urgent'
];

export const ALLOWED_TASK_TRANSITIONS: Record<TaskStatus, TaskStatus[]> = {
  Backlog: ['ToDo', 'Cancelled'],
  ToDo: ['InProgress', 'Backlog', 'Cancelled'],
  InProgress: ['Blocked', 'Done', 'ToDo', 'Cancelled'],
  Blocked: ['InProgress', 'ToDo', 'Cancelled'],
  Done: ['InProgress', 'ToDo'],
  Cancelled: ['Backlog', 'ToDo']
};

export interface TaskSummary {
  id: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  title: string;
  status: TaskStatus;
  priority: TaskPriority;
  assigneeId?: string | null;
  assigneeName?: string | null;
  createdById: string;
  createdByName: string;
  dueAtUtc?: string | null;
  estimateMinutes?: number | null;
  version: number;
  commentCount: number;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface TaskComment {
  id: string;
  taskId: string;
  authorId: string;
  authorName: string;
  body: string;
  createdAtUtc: string;
}

export interface TaskStatusHistory {
  id: string;
  taskId: string;
  fromStatus?: TaskStatus | null;
  toStatus: TaskStatus;
  changedById: string;
  changedByName: string;
  changedAtUtc: string;
  comment?: string | null;
}

export interface TaskDetail {
  id: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  title: string;
  description?: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  assigneeId?: string | null;
  assigneeName?: string | null;
  createdById: string;
  createdByName: string;
  dueAtUtc?: string | null;
  estimateMinutes?: number | null;
  version: number;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  comments: TaskComment[];
  history: TaskStatusHistory[];
}

export interface CreateTaskRequest {
  title: string;
  description?: string | null;
  priority: TaskPriority;
  assigneeId?: string | null;
  dueAtUtc?: string | null;
  estimateMinutes?: number | null;
  status?: TaskStatus | null;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string | null;
  priority: TaskPriority;
  assigneeId?: string | null;
  dueAtUtc?: string | null;
  estimateMinutes?: number | null;
  version: number;
}

export interface ChangeTaskStatusRequest {
  status: TaskStatus;
  version: number;
  comment?: string | null;
}

export interface AddTaskCommentRequest {
  body: string;
}
