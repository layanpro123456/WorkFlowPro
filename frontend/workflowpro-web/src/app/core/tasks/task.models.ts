import { TaskGitBranch, TaskGitSummary, TaskPullRequest } from '../git/git.models';
import { IssueLink, IssueType } from '../sprints/sprint.models';

export type TaskStatus = 'Backlog' | 'ToDo' | 'InProgress' | 'InReview' | 'Blocked' | 'Done' | 'Cancelled';
export type TaskPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export const TASK_STATUSES: TaskStatus[] = [
  'Backlog',
  'ToDo',
  'InProgress',
  'InReview',
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
  InProgress: ['InReview', 'Blocked', 'Done', 'ToDo', 'Cancelled'],
  InReview: ['Done', 'InProgress', 'Blocked', 'Cancelled'],
  Blocked: ['InProgress', 'ToDo', 'Cancelled'],
  Done: ['InProgress', 'ToDo'],
  Cancelled: ['Backlog', 'ToDo']
};

export interface TaskSummary {
  id: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  taskNumber?: number;
  taskKey?: string;
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
  gitSummary?: TaskGitSummary | null;
  issueType?: IssueType;
  storyPoints?: number | null;
  epicId?: string | null;
  epicKey?: string | null;
  epicTitle?: string | null;
  parentTaskId?: string | null;
  parentTaskKey?: string | null;
  sprintId?: string | null;
  sprintName?: string | null;
  boardRank?: number;
  subtaskCount?: number;
  completedSubtaskCount?: number;
  isBlockedByOpenIssue?: boolean;
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
  taskNumber?: number;
  taskKey?: string;
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
  branches?: TaskGitBranch[];
  pullRequests?: TaskPullRequest[];
  issueType?: IssueType;
  storyPoints?: number | null;
  epicId?: string | null;
  epicKey?: string | null;
  epicTitle?: string | null;
  parentTaskId?: string | null;
  parentTaskKey?: string | null;
  sprintId?: string | null;
  sprintName?: string | null;
  boardRank?: number;
  subtasks?: TaskSummary[];
  links?: IssueLink[];
}

export interface CreateTaskRequest {
  title: string;
  description?: string | null;
  priority: TaskPriority;
  assigneeId?: string | null;
  dueAtUtc?: string | null;
  estimateMinutes?: number | null;
  status?: TaskStatus | null;
  issueType?: IssueType;
  storyPoints?: number | null;
  epicId?: string | null;
  parentTaskId?: string | null;
  sprintId?: string | null;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string | null;
  priority: TaskPriority;
  assigneeId?: string | null;
  dueAtUtc?: string | null;
  estimateMinutes?: number | null;
  version: number;
  issueType?: IssueType | null;
  storyPoints?: number | null;
  epicId?: string | null;
  parentTaskId?: string | null;
  sprintId?: string | null;
}

export interface ChangeTaskStatusRequest {
  status: TaskStatus;
  version: number;
  comment?: string | null;
}

export interface AddTaskCommentRequest {
  body: string;
}
