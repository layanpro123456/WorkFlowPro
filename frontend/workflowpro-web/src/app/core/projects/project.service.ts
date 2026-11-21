import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AddMemberPayload,
  CreateProjectPayload,
  PagedResult,
  ProjectDetail,
  ProjectMember,
  ProjectSummary,
  UpdateProjectPayload
} from './project.models';

@Injectable({
  providedIn: 'root'
})
export class ProjectService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/projects';

  list(status?: string, search?: string, page = 1, pageSize = 20): Observable<PagedResult<ProjectSummary>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (status && status !== 'All') {
      params = params.set('status', status);
    }
    if (search && search.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http.get<PagedResult<ProjectSummary>>(this.baseUrl, { params });
  }

  getById(id: string): Observable<ProjectDetail> {
    return this.http.get<ProjectDetail>(`${this.baseUrl}/${id}`);
  }

  create(payload: CreateProjectPayload): Observable<ProjectSummary> {
    return this.http.post<ProjectSummary>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateProjectPayload): Observable<ProjectDetail> {
    return this.http.put<ProjectDetail>(`${this.baseUrl}/${id}`, payload);
  }

  archive(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/archive`, {});
  }

  getMembers(id: string): Observable<ProjectMember[]> {
    return this.http.get<ProjectMember[]>(`${this.baseUrl}/${id}/members`);
  }

  addMember(id: string, payload: AddMemberPayload): Observable<ProjectMember> {
    return this.http.post<ProjectMember>(`${this.baseUrl}/${id}/members`, payload);
  }

  removeMember(projectId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${projectId}/members/${userId}`);
  }
}
