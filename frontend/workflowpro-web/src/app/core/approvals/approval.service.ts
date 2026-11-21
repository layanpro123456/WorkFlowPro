import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult } from '../projects/project.models';
import {
  ApprovalRequestDetail,
  ApprovalRequestSummary,
  CancelApprovalRequest,
  CreateWorkflowDefinitionRequest,
  RecordDecisionRequest,
  SubmitApprovalRequest,
  UpdateWorkflowDefinitionRequest,
  WorkflowDefinitionDetail,
  WorkflowDefinitionSummary
} from './approval.models';

export interface ApprovalFilterParams {
  status?: string;
  inboxOnly?: boolean;
  myRequestsOnly?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ApprovalService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/approvals';
  private readonly defBaseUrl = '/api/v1/workflow-definitions';

  // Workflow Definitions
  listDefinitions(includeInactive = false): Observable<WorkflowDefinitionSummary[]> {
    const params = new HttpParams().set('includeInactive', includeInactive.toString());
    return this.http.get<WorkflowDefinitionSummary[]>(this.defBaseUrl, { params });
  }

  getDefinitionById(id: string): Observable<WorkflowDefinitionDetail> {
    return this.http.get<WorkflowDefinitionDetail>(`${this.defBaseUrl}/${id}`);
  }

  createDefinition(payload: CreateWorkflowDefinitionRequest): Observable<WorkflowDefinitionDetail> {
    return this.http.post<WorkflowDefinitionDetail>(this.defBaseUrl, payload);
  }

  updateDefinition(id: string, payload: UpdateWorkflowDefinitionRequest): Observable<WorkflowDefinitionDetail> {
    return this.http.put<WorkflowDefinitionDetail>(`${this.defBaseUrl}/${id}`, payload);
  }

  // Approval Requests
  listRequests(filters?: ApprovalFilterParams): Observable<PagedResult<ApprovalRequestSummary>> {
    let params = new HttpParams()
      .set('page', (filters?.page ?? 1).toString())
      .set('pageSize', (filters?.pageSize ?? 20).toString());

    if (filters?.status && filters.status !== 'All') {
      params = params.set('status', filters.status);
    }
    if (filters?.inboxOnly !== undefined) {
      params = params.set('inboxOnly', filters.inboxOnly.toString());
    }
    if (filters?.myRequestsOnly !== undefined) {
      params = params.set('myRequestsOnly', filters.myRequestsOnly.toString());
    }
    if (filters?.search && filters.search.trim()) {
      params = params.set('search', filters.search.trim());
    }

    return this.http.get<PagedResult<ApprovalRequestSummary>>(this.baseUrl, { params });
  }

  getRequestById(id: string): Observable<ApprovalRequestDetail> {
    return this.http.get<ApprovalRequestDetail>(`${this.baseUrl}/${id}`);
  }

  submitRequest(payload: SubmitApprovalRequest): Observable<ApprovalRequestSummary> {
    return this.http.post<ApprovalRequestSummary>(this.baseUrl, payload);
  }

  recordDecision(id: string, payload: RecordDecisionRequest): Observable<ApprovalRequestDetail> {
    return this.http.post<ApprovalRequestDetail>(`${this.baseUrl}/${id}/decisions`, payload);
  }

  cancelRequest(id: string, payload: CancelApprovalRequest): Observable<ApprovalRequestDetail> {
    return this.http.post<ApprovalRequestDetail>(`${this.baseUrl}/${id}/cancel`, payload);
  }
}
