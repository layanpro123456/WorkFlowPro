export type IssueType = 'Epic' | 'Story' | 'Task' | 'Bug' | 'Subtask';
export type IssueLinkType = 'Blocks' | 'RelatesTo' | 'Duplicates';
export type SprintStatus = 'Planned' | 'Active' | 'Closed';

export const ISSUE_TYPES: IssueType[] = ['Epic', 'Story', 'Task', 'Bug', 'Subtask'];
export const ISSUE_LINK_TYPES: IssueLinkType[] = ['Blocks', 'RelatesTo', 'Duplicates'];

export interface Sprint {
  id: string;
  projectId: string;
  name: string;
  goal?: string | null;
  status: SprintStatus;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  totalTasks: number;
  completedTasks: number;
  totalPoints: number;
  completedPoints: number;
  createdAtUtc: string;
}

export interface CreateSprintRequest {
  name: string;
  goal?: string | null;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
}

export interface UpdateSprintRequest {
  name: string;
  goal?: string | null;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
}

export interface StartSprintRequest {
  startDateUtc: string;
  endDateUtc: string;
}

export interface CompleteSprintRequest {
  moveIncompleteToSprintId?: string | null;
}

export interface CompleteSprintResult {
  completedSprint: Sprint;
  completedTasksCount: number;
  rolledOverTasksCount: number;
  rolledOverToSprintId?: string | null;
}

export interface SprintBurndownPoint {
  date: string;
  idealRemainingPoints: number;
  actualRemainingPoints: number;
  completedPoints: number;
  remainingTaskCount: number;
}

export interface SprintBurndown {
  sprintId: string;
  sprintName: string;
  status: SprintStatus;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
  totalPoints: number;
  points: SprintBurndownPoint[];
}

export interface VelocitySprintBar {
  sprintId: string;
  sprintName: string;
  committedPoints: number;
  completedPoints: number;
  completedTasks: number;
  completedAtUtc?: string | null;
}

export interface VelocityChart {
  projectId: string;
  averageCompletedPoints: number;
  sprints: VelocitySprintBar[];
}

export interface BoardColumnConfig {
  status: string;
  displayOrder: number;
  wipLimit?: number | null;
  isStrictWip: boolean;
  isVisible: boolean;
}

export interface MoveTaskOnBoardRequest {
  targetStatus: string;
  boardRank: number;
  version: number;
  comment?: string | null;
}

export interface AssignTaskSprintRequest {
  sprintId?: string | null;
  boardRank: number;
}

export interface IssueLink {
  id: string;
  linkType: IssueLinkType;
  displayRelation: string;
  relatedTaskId: string;
  relatedTaskKey: string;
  relatedTaskTitle: string;
  relatedTaskStatus: string;
  relatedTaskIssueType: IssueType;
  createdAtUtc: string;
}

export interface CreateIssueLinkRequest {
  targetTaskId: string;
  linkType: IssueLinkType;
}
