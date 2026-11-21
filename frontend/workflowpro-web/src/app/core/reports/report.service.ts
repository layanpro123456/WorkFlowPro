import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ProjectPerformanceReport,
  TaskReport,
  TaskReportFilterParams,
  TicketReport,
  TicketReportFilterParams
} from './report.models';

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/reports';

  getTaskReport(filters?: TaskReportFilterParams): Observable<TaskReport> {
    let params = new HttpParams()
      .set('page', (filters?.page ?? 1).toString())
      .set('pageSize', (filters?.pageSize ?? 50).toString());

    if (filters?.projectId && filters.projectId !== 'All') {
      params = params.set('projectId', filters.projectId);
    }
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }
    if (filters?.status && filters.status !== 'All') {
      params = params.set('status', filters.status);
    }

    return this.http.get<TaskReport>(`${this.baseUrl}/tasks`, { params });
  }

  exportTaskReportCsv(filters?: TaskReportFilterParams): Observable<Blob> {
    let params = new HttpParams().set('exportCsv', 'true');

    if (filters?.projectId && filters.projectId !== 'All') {
      params = params.set('projectId', filters.projectId);
    }
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }
    if (filters?.status && filters.status !== 'All') {
      params = params.set('status', filters.status);
    }

    return this.http.get(`${this.baseUrl}/tasks`, {
      params,
      responseType: 'blob'
    });
  }

  getTicketReport(filters?: TicketReportFilterParams): Observable<TicketReport> {
    let params = new HttpParams()
      .set('page', (filters?.page ?? 1).toString())
      .set('pageSize', (filters?.pageSize ?? 50).toString());

    if (filters?.category && filters.category !== 'All') {
      params = params.set('category', filters.category);
    }
    if (filters?.priority && filters.priority !== 'All') {
      params = params.set('priority', filters.priority);
    }
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }

    return this.http.get<TicketReport>(`${this.baseUrl}/tickets`, { params });
  }

  exportTicketReportCsv(filters?: TicketReportFilterParams): Observable<Blob> {
    let params = new HttpParams().set('exportCsv', 'true');

    if (filters?.category && filters.category !== 'All') {
      params = params.set('category', filters.category);
    }
    if (filters?.priority && filters.priority !== 'All') {
      params = params.set('priority', filters.priority);
    }
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }

    return this.http.get(`${this.baseUrl}/tickets`, {
      params,
      responseType: 'blob'
    });
  }

  getProjectPerformance(projectId: string, startDate?: string, endDate?: string): Observable<ProjectPerformanceReport> {
    let params = new HttpParams();
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);

    return this.http.get<ProjectPerformanceReport>(`${this.baseUrl}/projects/${projectId}`, { params });
  }
}
