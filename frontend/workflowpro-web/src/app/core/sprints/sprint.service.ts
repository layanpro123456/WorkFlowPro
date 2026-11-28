import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  BoardColumnConfig,
  CompleteSprintRequest,
  CompleteSprintResult,
  CreateSprintRequest,
  Sprint,
  SprintBurndown,
  StartSprintRequest,
  UpdateSprintRequest,
  VelocityChart
} from './sprint.models';

@Injectable({
  providedIn: 'root'
})
export class SprintService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  listProjectSprints(projectId: string): Observable<Sprint[]> {
    return this.http.get<Sprint[]>(`${this.baseUrl}/projects/${projectId}/sprints`);
  }

  createSprint(projectId: string, payload: CreateSprintRequest): Observable<Sprint> {
    return this.http.post<Sprint>(`${this.baseUrl}/projects/${projectId}/sprints`, payload);
  }

  updateSprint(sprintId: string, payload: UpdateSprintRequest): Observable<Sprint> {
    return this.http.put<Sprint>(`${this.baseUrl}/sprints/${sprintId}`, payload);
  }

  startSprint(sprintId: string, payload: StartSprintRequest): Observable<Sprint> {
    return this.http.post<Sprint>(`${this.baseUrl}/sprints/${sprintId}/start`, payload);
  }

  completeSprint(sprintId: string, payload: CompleteSprintRequest): Observable<CompleteSprintResult> {
    return this.http.post<CompleteSprintResult>(`${this.baseUrl}/sprints/${sprintId}/complete`, payload);
  }

  getBurndown(sprintId: string): Observable<SprintBurndown> {
    return this.http.get<SprintBurndown>(`${this.baseUrl}/sprints/${sprintId}/burndown`);
  }

  getVelocity(projectId: string): Observable<VelocityChart> {
    return this.http.get<VelocityChart>(`${this.baseUrl}/projects/${projectId}/velocity`);
  }

  getBoardConfigs(projectId: string): Observable<BoardColumnConfig[]> {
    return this.http.get<BoardColumnConfig[]>(`${this.baseUrl}/projects/${projectId}/board-config`);
  }

  updateBoardConfigs(projectId: string, columns: BoardColumnConfig[]): Observable<BoardColumnConfig[]> {
    return this.http.put<BoardColumnConfig[]>(`${this.baseUrl}/projects/${projectId}/board-config`, { columns });
  }
}
