import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult } from '../projects/project.models';
import {
  AdminUserSummary,
  AuditLog,
  AuditLogFilterParams,
  CreateUserRequest,
  UpdateUserRolesRequest,
  UpdateUserStatusRequest
} from './admin.models';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/admin';

  listUsers(
    search?: string,
    role?: string,
    isActive?: boolean,
    page = 1,
    pageSize = 20
  ): Observable<PagedResult<AdminUserSummary>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (search && search.trim()) {
      params = params.set('search', search.trim());
    }

    if (role && role !== 'All') {
      params = params.set('role', role);
    }

    if (isActive !== undefined && isActive !== null) {
      params = params.set('isActive', isActive.toString());
    }

    return this.http.get<PagedResult<AdminUserSummary>>(`${this.baseUrl}/users`, { params });
  }

  createUser(request: CreateUserRequest): Observable<AdminUserSummary> {
    return this.http.post<AdminUserSummary>(`${this.baseUrl}/users`, request);
  }

  updateUserStatus(userId: string, isActive: boolean): Observable<AdminUserSummary> {
    const payload: UpdateUserStatusRequest = { isActive };
    return this.http.patch<AdminUserSummary>(`${this.baseUrl}/users/${userId}/status`, payload);
  }

  updateUserRoles(userId: string, roles: string[]): Observable<AdminUserSummary> {
    const payload: UpdateUserRolesRequest = { roles };
    return this.http.put<AdminUserSummary>(`${this.baseUrl}/users/${userId}/roles`, payload);
  }

  getRoles(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/roles`);
  }

  getAuditLogs(filters?: AuditLogFilterParams): Observable<PagedResult<AuditLog>> {
    let params = new HttpParams()
      .set('page', (filters?.page ?? 1).toString())
      .set('pageSize', (filters?.pageSize ?? 20).toString());

    if (filters?.action && filters.action !== 'All') {
      params = params.set('action', filters.action);
    }

    if (filters?.entityType && filters.entityType !== 'All') {
      params = params.set('entityType', filters.entityType);
    }

    if (filters?.actorId) {
      params = params.set('actorId', filters.actorId);
    }

    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }

    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }

    return this.http.get<PagedResult<AuditLog>>(`${this.baseUrl}/audit-logs`, { params });
  }
}
