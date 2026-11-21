import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult } from '../projects/project.models';
import {
  AddTicketMessageRequest,
  AssignTicketRequest,
  ChangeTicketStatusRequest,
  CreateTicketRequest,
  SupportAgent,
  TicketDetail,
  TicketMessage,
  TicketStatusHistory,
  TicketSummary
} from './ticket.models';

export interface TicketFilterParams {
  status?: string;
  priority?: string;
  category?: string;
  assigneeId?: string;
  search?: string;
  myTicketsOnly?: boolean;
  page?: number;
  pageSize?: number;
}

@Injectable({
  providedIn: 'root'
})
export class TicketService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/tickets';

  list(filters?: TicketFilterParams): Observable<PagedResult<TicketSummary>> {
    let params = new HttpParams()
      .set('page', (filters?.page ?? 1).toString())
      .set('pageSize', (filters?.pageSize ?? 20).toString());

    if (filters?.status && filters.status !== 'All') {
      params = params.set('status', filters.status);
    }
    if (filters?.priority && filters.priority !== 'All') {
      params = params.set('priority', filters.priority);
    }
    if (filters?.category && filters.category !== 'All') {
      params = params.set('category', filters.category);
    }
    if (filters?.assigneeId && filters.assigneeId !== 'All') {
      params = params.set('assigneeId', filters.assigneeId);
    }
    if (filters?.search && filters.search.trim()) {
      params = params.set('search', filters.search.trim());
    }
    if (filters?.myTicketsOnly !== undefined) {
      params = params.set('myTicketsOnly', filters.myTicketsOnly.toString());
    }

    return this.http.get<PagedResult<TicketSummary>>(this.baseUrl, { params });
  }

  getById(id: string): Observable<TicketDetail> {
    return this.http.get<TicketDetail>(`${this.baseUrl}/${id}`);
  }

  create(payload: CreateTicketRequest): Observable<TicketSummary> {
    return this.http.post<TicketSummary>(this.baseUrl, payload);
  }

  assign(id: string, payload: AssignTicketRequest): Observable<TicketSummary> {
    return this.http.put<TicketSummary>(`${this.baseUrl}/${id}/assign`, payload);
  }

  changeStatus(id: string, payload: ChangeTicketStatusRequest): Observable<TicketSummary> {
    return this.http.patch<TicketSummary>(`${this.baseUrl}/${id}/status`, payload);
  }

  addMessage(id: string, payload: AddTicketMessageRequest): Observable<TicketMessage> {
    return this.http.post<TicketMessage>(`${this.baseUrl}/${id}/messages`, payload);
  }

  getMessages(id: string): Observable<TicketMessage[]> {
    return this.http.get<TicketMessage[]>(`${this.baseUrl}/${id}/messages`);
  }

  getHistory(id: string): Observable<TicketStatusHistory[]> {
    return this.http.get<TicketStatusHistory[]>(`${this.baseUrl}/${id}/history`);
  }

  getAgents(): Observable<SupportAgent[]> {
    return this.http.get<SupportAgent[]>(`${this.baseUrl}/agents`);
  }
}
