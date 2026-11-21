import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { DashboardComponent } from './dashboard.component';
import { DashboardService } from '../../core/dashboard/dashboard.service';
import { AuthService } from '../../core/auth/auth.service';
import { DashboardSummary } from '../../core/dashboard/dashboard.models';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let dashboardService: DashboardService;
  let authService: AuthService;

  const mockSummary: DashboardSummary = {
    assignedTasksCount: 3,
    overdueTasksCount: 1,
    openTicketsCount: 2,
    pendingApprovalsCount: 4,
    unreadNotificationsCount: 1,
    managedProjectsCount: 1,
    recentTasks: [
      {
        id: 'task-1',
        projectId: 'proj-1',
        projectCode: 'ALPHA',
        projectName: 'Alpha Project',
        title: 'Fix authentication bug',
        status: 'InProgress',
        priority: 'High',
        assigneeId: 'user-1',
        assigneeName: 'Test User',
        createdById: 'user-2',
        createdByName: 'Admin',
        dueAtUtc: '2026-12-01T00:00:00Z',
        estimateMinutes: 60,
        version: 1,
        commentCount: 2,
        createdAtUtc: '2026-11-20T00:00:00Z',
        updatedAtUtc: null
      }
    ],
    recentTickets: [
      {
        id: 'tick-1',
        ticketNumber: 101,
        subject: 'VPN Access request',
        category: 'Access',
        priority: 'Normal',
        status: 'Assigned',
        requesterId: 'user-2',
        requesterName: 'Jane Doe',
        assigneeId: 'user-1',
        assigneeName: 'Test User',
        relatedProjectId: null,
        relatedProjectCode: null,
        relatedProjectName: null,
        messageCount: 1,
        createdAtUtc: '2026-11-21T00:00:00Z',
        updatedAtUtc: null,
        resolvedAtUtc: null,
        closedAtUtc: null
      }
    ],
    recentApprovals: [
      {
        id: 'appr-1',
        requestNumber: 1001,
        workflowDefinitionId: 'wf-1',
        workflowName: 'Expense Approval',
        requesterId: 'user-3',
        requesterName: 'Alice',
        title: 'Conference pass',
        status: 'Pending',
        currentStepOrder: 1,
        currentStepName: 'Manager Signoff',
        currentApproverId: 'user-1',
        currentApproverName: 'Test User',
        version: 1,
        submittedAtUtc: '2026-11-21T00:00:00Z',
        completedAtUtc: null
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    dashboardService = TestBed.inject(DashboardService);
    authService = TestBed.inject(AuthService);
  });

  it('loads dashboard summary on initialization', () => {
    const summarySpy = vi.spyOn(dashboardService, 'getSummary').mockReturnValue(of(mockSummary));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(summarySpy).toHaveBeenCalled();
    expect(component.loading()).toBe(false);
    expect(component.summary()).toEqual(mockSummary);
    expect(component.summary()?.assignedTasksCount).toBe(3);
    expect(component.summary()?.recentApprovals.length).toBe(1);
    expect(component.summary()?.recentTasks.length).toBe(1);
    expect(component.summary()?.recentTickets.length).toBe(1);
  });

  it('renders error banner when loading summary fails', () => {
    vi.spyOn(dashboardService, 'getSummary').mockReturnValue(throwError(() => new Error('Server error')));

    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBe('Failed to load dashboard data. Please try again.');
  });
});
