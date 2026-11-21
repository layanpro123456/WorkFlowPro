import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ApprovalDetailComponent } from './approval-detail.component';
import { ApprovalService } from '../../core/approvals/approval.service';
import { AuthService } from '../../core/auth/auth.service';
import { ApprovalRequestDetail } from '../../core/approvals/approval.models';

describe('ApprovalDetailComponent', () => {
  let component: ApprovalDetailComponent;
  let fixture: ComponentFixture<ApprovalDetailComponent>;
  let approvalService: ApprovalService;
  let authService: AuthService;

  const mockDetail: ApprovalRequestDetail = {
    id: 'req-100',
    requestNumber: 1055,
    workflowDefinitionId: 'def-1',
    workflowName: 'Capex Flow',
    requesterId: 'u-requester',
    requesterName: 'Jane Requester',
    requesterEmail: 'jane@workflowpro.local',
    title: 'Workstation Purchase',
    description: 'Hardware upgrade for CAD work',
    status: 'Pending',
    currentStepOrder: 1,
    currentStepName: 'Manager Signoff',
    currentApproverId: 'u-approver-1',
    currentApproverName: 'Manager Mike',
    version: 1,
    submittedAtUtc: '2026-11-21T00:00:00Z',
    steps: [
      {
        id: 's-1',
        stepOrder: 1,
        name: 'Manager Signoff',
        approverUserId: 'u-approver-1',
        approverName: 'Manager Mike',
        approverEmail: 'mike@workflowpro.local',
        status: 'Pending',
        decidedAtUtc: null,
        decision: null
      },
      {
        id: 's-2',
        stepOrder: 2,
        name: 'Finance Approval',
        approverUserId: 'u-finance',
        approverName: 'Finance Fiona',
        approverEmail: 'fiona@workflowpro.local',
        status: 'Pending',
        decidedAtUtc: null,
        decision: null
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ApprovalDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? 'req-100' : null
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ApprovalDetailComponent);
    component = fixture.componentInstance;
    approvalService = TestBed.inject(ApprovalService);
    authService = TestBed.inject(AuthService);
  });

  it('loads request detail and visual steps on init', () => {
    const getSpy = vi.spyOn(approvalService, 'getRequestById').mockReturnValue(of(mockDetail));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(getSpy).toHaveBeenCalledWith('req-100');
    expect(component.request()?.title).toBe('Workstation Purchase');
    expect(component.request()?.steps.length).toBe(2);
  });

  it('canDecide returns true only for current designated approver', () => {
    vi.spyOn(approvalService, 'getRequestById').mockReturnValue(of(mockDetail));
    fixture.detectChanges();

    // Wrong user
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      id: 'wrong-user',
      email: 'wrong@workflowpro.local',
      displayName: 'Wrong',
      roles: ['Employee']
    });
    expect(component.canDecide()).toBe(false);

    // Designated approver
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      id: 'u-approver-1',
      email: 'mike@workflowpro.local',
      displayName: 'Manager Mike',
      roles: ['Manager']
    });
    expect(component.canDecide()).toBe(true);
  });

  it('submits decision and advances step', () => {
    vi.spyOn(approvalService, 'getRequestById').mockReturnValue(of(mockDetail));
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      id: 'u-approver-1',
      email: 'mike@workflowpro.local',
      displayName: 'Manager Mike',
      roles: ['Manager']
    });

    fixture.detectChanges();

    const updatedDetail: ApprovalRequestDetail = {
      ...mockDetail,
      currentStepOrder: 2,
      currentStepName: 'Finance Approval',
      currentApproverId: 'u-finance',
      currentApproverName: 'Finance Fiona',
      version: 2,
      steps: [
        {
          ...mockDetail.steps[0],
          status: 'Approved',
          decidedAtUtc: '2026-11-21T01:00:00Z',
          decision: {
            id: 'dec-1',
            approverId: 'u-approver-1',
            approverName: 'Manager Mike',
            decision: 'Approved',
            comment: 'Hardware approved',
            decidedAtUtc: '2026-11-21T01:00:00Z'
          }
        },
        mockDetail.steps[1]
      ]
    };

    const decideSpy = vi.spyOn(approvalService, 'recordDecision').mockReturnValue(of(updatedDetail));

    component.decisionComment = 'Hardware approved';
    component.submitDecision('Approved');

    expect(decideSpy).toHaveBeenCalledWith('req-100', {
      decision: 'Approved',
      comment: 'Hardware approved',
      expectedVersion: 1
    });

    expect(component.request()?.currentStepOrder).toBe(2);
    expect(component.request()?.version).toBe(2);
    expect(component.successMessage()).toContain('Approved');
  });

  it('handles 409 concurrency conflict by displaying warning and reloading', () => {
    const getSpy = vi.spyOn(approvalService, 'getRequestById').mockReturnValue(of(mockDetail));
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      id: 'u-approver-1',
      email: 'mike@workflowpro.local',
      displayName: 'Manager Mike',
      roles: ['Manager']
    });

    fixture.detectChanges();

    vi.spyOn(approvalService, 'recordDecision').mockReturnValue(throwError(() => ({
      status: 409,
      error: { detail: 'Version conflict' }
    })));

    component.submitDecision('Approved');

    expect(component.errorMessage()).toContain('Concurrency Conflict');
    expect(getSpy).toHaveBeenCalledTimes(2); // Initial + reload on conflict
  });

  it('allows requester to cancel request', () => {
    vi.spyOn(approvalService, 'getRequestById').mockReturnValue(of(mockDetail));
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      id: 'u-requester',
      email: 'jane@workflowpro.local',
      displayName: 'Jane Requester',
      roles: ['Employee']
    });
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    fixture.detectChanges();

    const cancelledDetail: ApprovalRequestDetail = {
      ...mockDetail,
      status: 'Cancelled',
      currentStepOrder: null,
      version: 2
    };

    const cancelSpy = vi.spyOn(approvalService, 'cancelRequest').mockReturnValue(of(cancelledDetail));

    component.onCancelRequest();

    expect(cancelSpy).toHaveBeenCalledWith('req-100', { expectedVersion: 1 });
    expect(component.request()?.status).toBe('Cancelled');
  });
});
