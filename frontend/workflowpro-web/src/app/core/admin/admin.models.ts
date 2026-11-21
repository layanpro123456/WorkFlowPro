import { PagedResult } from '../projects/project.models';

export interface AdminUserSummary {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface CreateUserRequest {
  email: string;
  displayName: string;
  password?: string;
  roles: string[];
}

export interface UpdateUserStatusRequest {
  isActive: boolean;
}

export interface UpdateUserRolesRequest {
  roles: string[];
}

export interface AuditLog {
  id: string;
  actorId?: string | null;
  actorDisplayName?: string | null;
  actorEmail?: string | null;
  action: string;
  entityType: string;
  entityId?: string | null;
  occurredAtUtc: string;
  correlationId?: string | null;
  metadataJson?: string | null;
}

export interface AuditLogFilterParams {
  action?: string;
  entityType?: string;
  actorId?: string;
  startDate?: string;
  endDate?: string;
  page?: number;
  pageSize?: number;
}
