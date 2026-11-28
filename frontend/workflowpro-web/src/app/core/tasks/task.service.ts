import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult } from '../projects/project.models';
import {
  AssignTaskSprintRequest,
  CreateIssueLinkRequest,
  IssueLink,
  MoveTaskOnBoardRequest
} from '../sprints/sprint.models';
import {
  AddTaskCommentRequest,
  ChangeTaskStatusRequest,
  CreateTaskRequest,
  TaskComment,
  TaskDetail,
  TaskStatusHistory,
  TaskSummary,
  UpdateTaskRequest
} from './task.models';

export interface TaskFilterParams {
  status?: string;
  priority?: string;
  assigneeId?: string;
  search?: string;
  prState?: string;
  sprintId?: string;
  issueType?: string;
  epicId?: string;
  backlogOnly?: boolean;
  page?: number;
  pageSize?: number;
}

@Injectable({
  providedIn: 'root'
})
export class TaskService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  listProjectTasks(projectId: string, filters?: TaskFilterParams): Observable<PagedResult<TaskSummary>> {
    let params = new HttpParams()
      .set('page', (filters?.page ?? 1).toString())
      .set('pageSize', (filters?.pageSize ?? 50).toString());

    if (filters?.status && filters.status !== 'All') {
      params = params.set('status', filters.status);
    }
    if (filters?.priority && filters.priority !== 'All') {
      params = params.set('priority', filters.priority);
    }
    if (filters?.assigneeId && filters.assigneeId !== 'All') {
      params = params.set('assigneeId', filters.assigneeId);
    }
    if (filters?.search && filters.search.trim()) {
      params = params.set('search', filters.search.trim());
    }
    if (filters?.prState && filters.prState !== 'All') {
      params = params.set('prState', filters.prState);
    }
    if (filters?.sprintId && filters.sprintId !== 'All') {
      params = params.set('sprintId', filters.sprintId);
    }
    if (filters?.issueType && filters.issueType !== 'All') {
      params = params.set('issueType', filters.issueType);
    }
    if (filters?.epicId && filters.epicId !== 'All') {
      params = params.set('epicId', filters.epicId);
    }
    if (filters?.backlogOnly) {
      params = params.set('backlogOnly', 'true');
    }

    return this.http.get<PagedResult<TaskSummary>>(
      `${this.baseUrl}/projects/${projectId}/tasks`,
      { params }
    );
  }

  getById(taskId: string): Observable<TaskDetail> {
    return this.http.get<TaskDetail>(`${this.baseUrl}/tasks/${taskId}`);
  }

  create(projectId: string, payload: CreateTaskRequest): Observable<TaskSummary> {
    return this.http.post<TaskSummary>(`${this.baseUrl}/projects/${projectId}/tasks`, payload);
  }

  update(taskId: string, payload: UpdateTaskRequest): Observable<TaskSummary> {
    return this.http.put<TaskSummary>(`${this.baseUrl}/tasks/${taskId}`, payload);
  }

  changeStatus(taskId: string, payload: ChangeTaskStatusRequest): Observable<TaskSummary> {
    return this.http.patch<TaskSummary>(`${this.baseUrl}/tasks/${taskId}/status`, payload);
  }

  moveOnBoard(taskId: string, payload: MoveTaskOnBoardRequest): Observable<TaskSummary> {
    return this.http.patch<TaskSummary>(`${this.baseUrl}/tasks/${taskId}/move`, payload);
  }

  assignSprint(taskId: string, payload: AssignTaskSprintRequest): Observable<TaskSummary> {
    return this.http.patch<TaskSummary>(`${this.baseUrl}/tasks/${taskId}/sprint`, payload);
  }

  createLink(taskId: string, payload: CreateIssueLinkRequest): Observable<IssueLink> {
    return this.http.post<IssueLink>(`${this.baseUrl}/tasks/${taskId}/links`, payload);
  }

  deleteLink(taskId: string, linkId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/tasks/${taskId}/links/${linkId}`);
  }

  addComment(taskId: string, payload: AddTaskCommentRequest): Observable<TaskComment> {
    return this.http.post<TaskComment>(`${this.baseUrl}/tasks/${taskId}/comments`, payload);
  }

  getComments(taskId: string): Observable<TaskComment[]> {
    return this.http.get<TaskComment[]>(`${this.baseUrl}/tasks/${taskId}/comments`);
  }

  getHistory(taskId: string): Observable<TaskStatusHistory[]> {
    return this.http.get<TaskStatusHistory[]>(`${this.baseUrl}/tasks/${taskId}/history`);
  }
}
