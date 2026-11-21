import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ReportsComponent } from './reports.component';
import { ReportService } from '../../core/reports/report.service';
import { TaskReport, TicketReport } from '../../core/reports/report.models';

describe('ReportsComponent', () => {
  let component: ReportsComponent;
  let fixture: ComponentFixture<ReportsComponent>;
  let reportService: ReportService;

  const mockTaskReport: TaskReport = {
    totalTasks: 5,
    completedTasks: 3,
    completionRate: 60,
    overdueTasks: 1,
    statusDistribution: { Done: 3, InProgress: 2 },
    priorityDistribution: { Normal: 4, Urgent: 1 },
    tasks: {
      items: [
        {
          taskId: 't-1',
          projectCode: 'PROJ',
          projectName: 'Project Alpha',
          title: 'Implement Reports',
          status: 'InProgress',
          priority: 'Urgent',
          assigneeName: 'Developer',
          dueDateUtc: '2026-11-25T00:00:00Z',
          createdAtUtc: '2026-11-20T00:00:00Z'
        }
      ],
      page: 1,
      pageSize: 20,
      totalCount: 5,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }
  };

  const mockTicketReport: TicketReport = {
    totalTickets: 4,
    resolvedTickets: 2,
    resolutionRate: 50,
    categoryDistribution: { Technical: 3, General: 1 },
    priorityDistribution: { High: 2, Normal: 2 },
    statusDistribution: { Resolved: 2, InProgress: 2 },
    tickets: {
      items: [
        {
          ticketNumber: 101,
          subject: 'Printer network offline',
          category: 'Hardware',
          priority: 'High',
          status: 'Resolved',
          requesterName: 'Jane User',
          assigneeName: 'Tech Support',
          createdAtUtc: '2026-11-21T00:00:00Z',
          resolvedAtUtc: '2026-11-21T01:00:00Z'
        }
      ],
      page: 1,
      pageSize: 20,
      totalCount: 4,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ReportsComponent);
    component = fixture.componentInstance;
    reportService = TestBed.inject(ReportService);
  });

  it('loads task report on initialization', () => {
    const taskSpy = vi.spyOn(reportService, 'getTaskReport').mockReturnValue(of(mockTaskReport));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(taskSpy).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 20 }));
    expect(component.taskReport()).toEqual(mockTaskReport);
    expect(component.taskReport()?.totalTasks).toBe(5);
    expect(component.taskReport()?.completedTasks).toBe(3);
  });

  it('switches to tickets tab and fetches ticket report', () => {
    vi.spyOn(reportService, 'getTaskReport').mockReturnValue(of(mockTaskReport));
    const ticketSpy = vi.spyOn(reportService, 'getTicketReport').mockReturnValue(of(mockTicketReport));

    fixture.detectChanges();

    component.setTab('tickets');

    expect(component.activeTab()).toBe('tickets');
    expect(ticketSpy).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 20 }));
    expect(component.ticketReport()).toEqual(mockTicketReport);
    expect(component.ticketReport()?.totalTickets).toBe(4);
  });

  it('triggers task report CSV export', () => {
    vi.spyOn(reportService, 'getTaskReport').mockReturnValue(of(mockTaskReport));
    const mockBlob = new Blob(['Task ID,Title\n1,Task 1'], { type: 'text/csv' });
    const exportSpy = vi.spyOn(reportService, 'exportTaskReportCsv').mockReturnValue(of(mockBlob));

    // Mock window.URL methods
    const createUrlSpy = vi.spyOn(window.URL, 'createObjectURL').mockReturnValue('blob:http://localhost/dummy');
    const revokeUrlSpy = vi.spyOn(window.URL, 'revokeObjectURL').mockImplementation(() => {});

    fixture.detectChanges();

    component.exportTasksCsv();

    expect(exportSpy).toHaveBeenCalled();
    expect(createUrlSpy).toHaveBeenCalledWith(mockBlob);
    expect(revokeUrlSpy).toHaveBeenCalled();
    expect(component.exporting()).toBe(false);
  });

  it('handles task report error gracefully', () => {
    vi.spyOn(reportService, 'getTaskReport').mockReturnValue(throwError(() => new Error('Error')));

    fixture.detectChanges();

    expect(component.taskLoading()).toBe(false);
    expect(component.taskError()).toBe('Failed to generate task report.');
  });
});
