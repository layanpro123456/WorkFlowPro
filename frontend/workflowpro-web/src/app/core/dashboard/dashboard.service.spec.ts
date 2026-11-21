import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardService } from './dashboard.service';
import { DashboardSummary } from './dashboard.models';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpTesting: HttpTestingController;

  const mockSummary: DashboardSummary = {
    assignedTasksCount: 4,
    overdueTasksCount: 1,
    openTicketsCount: 2,
    pendingApprovalsCount: 3,
    unreadNotificationsCount: 5,
    managedProjectsCount: 2,
    recentTasks: [],
    recentTickets: [],
    recentApprovals: []
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(DashboardService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('getSummary sends GET request to dashboard endpoint', () => {
    service.getSummary().subscribe(res => {
      expect(res).toEqual(mockSummary);
      expect(res.assignedTasksCount).toBe(4);
      expect(res.overdueTasksCount).toBe(1);
    });

    const req = httpTesting.expectOne('/api/v1/dashboard/summary');
    expect(req.request.method).toBe('GET');
    req.flush(mockSummary);
  });
});
