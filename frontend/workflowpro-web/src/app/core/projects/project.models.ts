export interface ProjectSummary {
  id: string;
  projectCode: string;
  name: string;
  status: string;
  startDate?: string;
  targetDate?: string;
  memberCount: number;
  createdAtUtc: string;
}

export interface ProjectMember {
  userId: string;
  displayName: string;
  email: string;
  projectRole: string;
  joinedAtUtc: string;
}

export interface ProjectDetail {
  id: string;
  projectCode: string;
  name: string;
  description?: string;
  status: string;
  startDate?: string;
  targetDate?: string;
  createdById: string;
  createdByName: string;
  createdAtUtc: string;
  archivedAtUtc?: string;
  members: ProjectMember[];
}

export interface CreateProjectPayload {
  projectCode: string;
  name: string;
  description?: string;
  startDate?: string;
  targetDate?: string;
}

export interface UpdateProjectPayload {
  name: string;
  description?: string;
  status?: string;
  startDate?: string;
  targetDate?: string;
}

export interface AddMemberPayload {
  userId: string;
  projectRole: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
