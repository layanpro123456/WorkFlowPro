import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReportService } from './report.service';
import { TaskReport, TicketReport, ProjectPerformanceReport } from './report.models';

describe('ReportService', () => {
  let service: ReportService;
  let httpTesting: HttpTestingController;

  const mockTaskReport: TaskReport = {
    totalTasks: 10,
    completedTasks: 7,
    completionRate: 70,
    overdueTasks: 1,
    statusDistribution: { Done: 7, InProgress: 3 },
    priorityDistribution: { Normal: 8, High: 2 },
    tasks: {
      items: [],
      page: 1,
      pageSize: 50,
      totalCount: 10,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }
  };

  const mockTicketReport: TicketReport = {
    totalTickets: 8,
    resolvedTickets: 5,
    resolutionRate: 62.5,
    categoryDistribution: { Technical: 5, Access: 3 },
    priorityDistribution: { High: 4, Low: 4 },
    statusDistribution: { Resolved: 5, Open: 3 },
    tickets: {
      items: [],
      page: 1,
      pageSize: 50,
      totalCount: 8,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(ReportService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('getTaskReport passes filters and pagination', () => {
    service.getTaskReport({ status: 'InProgress', page: 2, pageSize: 25 }).subscribe(res => {
      expect(res).toEqual(mockTaskReport);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/reports/tasks' &&
      r.params.get('status') === 'InProgress' &&
      r.params.get('page') === '2' &&
      r.params.get('pageSize') === '25'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockTaskReport);
  });

  it('exportTaskReportCsv requests blob with exportCsv=true', () => {
    const mockBlob = new Blob(['Task ID,Project Code,Title\n1,PROJ,Test Task'], { type: 'text/csv' });

    service.exportTaskReportCsv({ status: 'Done' }).subscribe(res => {
      expect(res).toEqual(mockBlob);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/reports/tasks' &&
      r.params.get('exportCsv') === 'true' &&
      r.params.get('status') === 'Done'
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(mockBlob);
  });

  it('getTicketReport passes category and priority params', () => {
    service.getTicketReport({ category: 'Technical', priority: 'High', page: 1, pageSize: 20 }).subscribe(res => {
      expect(res).toEqual(mockTicketReport);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/reports/tickets' &&
      r.params.get('category') === 'Technical' &&
      r.params.get('priority') === 'High' &&
      r.params.get('page') === '1' &&
      r.params.get('pageSize') === '20'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockTicketReport);
  });

  it('exportTicketReportCsv requests blob with exportCsv=true', () => {
    const mockBlob = new Blob(['Ticket #,Subject,Status\n1001,Bug,Resolved'], { type: 'text/csv' });

    service.exportTicketReportCsv().subscribe(res => {
      expect(res).toEqual(mockBlob);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/reports/tickets' &&
      r.params.get('exportCsv') === 'true'
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(mockBlob);
  });

  it('getProjectPerformance queries project performance endpoint', () => {
    const mockPerf: ProjectPerformanceReport = {
      projectId: 'proj-1',
      projectCode: 'ALPHA',
      projectName: 'Project Alpha',
      status: 'Active',
      startDate: '2026-01-01',
      targetDate: '2026-12-31',
      totalTasks: 20,
      completedTasks: 15,
      overdueTasks: 2,
      completionRate: 75,
      memberCount: 5
    };

    service.getProjectPerformance('proj-1', '2026-01-01', '2026-06-30').subscribe(res => {
      expect(res).toEqual(mockPerf);
    });

    const req = httpTesting.expectOne('/api/v1/reports/projects/proj-1?startDate=2026-01-01&endDate=2026-06-30');
    expect(req.request.method).toBe('GET');
    req.flush(mockPerf);
  });
});
