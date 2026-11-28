import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ConnectGitRepositoryRequest,
  ConnectGitRepositoryResponse,
  CreateTaskBranchRequest,
  GitConnectionTestResult,
  GitSyncResult,
  LinkPullRequestRequest,
  ProjectGitRepository,
  ProjectPullRequestSummary,
  TaskGitBranch,
  TaskGitDetail,
  TaskPullRequest,
  UpdateGitRepositoryRequest
} from './git.models';

@Injectable({
  providedIn: 'root'
})
export class GitService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  listProjectRepositories(projectId: string): Observable<ProjectGitRepository[]> {
    return this.http.get<ProjectGitRepository[]>(`${this.baseUrl}/projects/${projectId}/git/repositories`);
  }

  connectRepository(projectId: string, payload: ConnectGitRepositoryRequest): Observable<ConnectGitRepositoryResponse> {
    return this.http.post<ConnectGitRepositoryResponse>(`${this.baseUrl}/projects/${projectId}/git/repositories`, payload);
  }

  updateRepository(
    projectId: string,
    repositoryId: string,
    payload: UpdateGitRepositoryRequest
  ): Observable<ProjectGitRepository> {
    return this.http.put<ProjectGitRepository>(
      `${this.baseUrl}/projects/${projectId}/git/repositories/${repositoryId}`,
      payload
    );
  }

  disconnectRepository(projectId: string, repositoryId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/projects/${projectId}/git/repositories/${repositoryId}`);
  }

  testRepositoryConnection(projectId: string, repositoryId: string): Observable<GitConnectionTestResult> {
    return this.http.post<GitConnectionTestResult>(
      `${this.baseUrl}/projects/${projectId}/git/repositories/${repositoryId}/test`,
      {}
    );
  }

  syncPullRequests(projectId: string, repositoryId: string): Observable<GitSyncResult> {
    return this.http.post<GitSyncResult>(
      `${this.baseUrl}/projects/${projectId}/git/repositories/${repositoryId}/sync`,
      {}
    );
  }

  listProjectPullRequests(projectId: string, state?: string): Observable<ProjectPullRequestSummary[]> {
    let params = new HttpParams();
    if (state && state !== 'All') {
      params = params.set('state', state);
    }
    return this.http.get<ProjectPullRequestSummary[]>(
      `${this.baseUrl}/projects/${projectId}/git/pull-requests`,
      { params }
    );
  }

  getTaskGitDetails(taskId: string): Observable<TaskGitDetail> {
    return this.http.get<TaskGitDetail>(`${this.baseUrl}/tasks/${taskId}/git`);
  }

  createTaskBranch(taskId: string, payload: CreateTaskBranchRequest): Observable<TaskGitBranch> {
    return this.http.post<TaskGitBranch>(`${this.baseUrl}/tasks/${taskId}/git/branches`, payload);
  }

  linkPullRequest(taskId: string, payload: LinkPullRequestRequest): Observable<TaskPullRequest> {
    return this.http.post<TaskPullRequest>(`${this.baseUrl}/tasks/${taskId}/git/pull-requests/link`, payload);
  }

  unlinkPullRequest(taskId: string, pullRequestId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/tasks/${taskId}/git/pull-requests/${pullRequestId}`);
  }
}
