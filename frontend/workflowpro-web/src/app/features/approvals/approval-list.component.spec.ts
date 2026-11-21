import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ApprovalListComponent } from './approval-list.component';
import { ApprovalService } from '../../core/approvals/approval.service';
import { AuthService } from '../../core/auth/auth.service';
import { ApprovalRequestSummary, WorkflowDefinitionDetail, WorkflowDefinitionSummary } from '../../core/approvals/approval.models';

describe('ApprovalListComponent', () => {
  let component: ApprovalListComponent;
  let fixture: ComponentFixture<ApprovalListComponent>;
  let approvalService: ApprovalService;
  let authService: AuthService;

  const mockRequests: ApprovalRequestSummary[] = [
    {
      id: 'req-1',
      requestNumber: 1001,
      workflowDefinitionId: 'def-1',
      workflowName: 'Purchase Flow',
      requesterId: 'u-2',
      requesterName: 'Alice',
      title: 'Ergonomic Desk',
      status: 'Pending',
      currentStepOrder: 1,
      currentStepName: 'Manager Signoff',
      currentApproverId: 'u-1',
      currentApproverName: 'Bob Manager',
      version: 1,
      submittedAtUtc: '2026-11-21'
    }
  ];

  const mockWorkflows: WorkflowDefinitionSummary[] = [
    {
      id: 'def-1',
      name: 'Purchase Flow',
      description: 'Standard PO Flow',
      isActive: true,
      stepCount: 2,
      createdById: 'u-admin',
      createdAtUtc: '2026-11-21'
    }
  ];

  const mockWorkflowDetail: WorkflowDefinitionDetail = {
    id: 'def-1',
    name: 'Purchase Flow',
    description: 'Standard PO Flow',
    isActive: true,
    createdById: 'u-admin',
    createdByName: 'Admin',
    createdAtUtc: '2026-11-21',
    steps: [
      { id: 's-1', stepOrder: 1, name: 'Manager Signoff', approverRole: 'Manager', approverDisplayName: null }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ApprovalListComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ApprovalListComponent);
    component = fixture.componentInstance;
    approvalService = TestBed.inject(ApprovalService);
    authService = TestBed.inject(AuthService);
  });

  it('loads requests and workflows on init', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    const listReqsSpy = vi.spyOn(approvalService, 'listRequests').mockReturnValue(of({
      items: mockRequests,
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    const listDefsSpy = vi.spyOn(approvalService, 'listDefinitions').mockReturnValue(of(mockWorkflows));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(listReqsSpy).toHaveBeenCalledWith(expect.objectContaining({ inboxOnly: true }));
    expect(listDefsSpy).toHaveBeenCalled();
    expect(component.requests().length).toBe(1);
    expect(component.requests()[0].title).toBe('Ergonomic Desk');
  });

  it('switches tabs and updates list filter', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    vi.spyOn(approvalService, 'listDefinitions').mockReturnValue(of(mockWorkflows));
    const listReqsSpy = vi.spyOn(approvalService, 'listRequests').mockReturnValue(of({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    fixture.detectChanges();

    component.setTab('mine');
    expect(component.activeTab()).toBe('mine');
    expect(listReqsSpy).toHaveBeenCalledWith(expect.objectContaining({ myRequestsOnly: true }));
  });

  it('opens submit modal and previews workflow steps', () => {
    vi.spyOn(approvalService, 'listDefinitions').mockReturnValue(of(mockWorkflows));
    const getDefSpy = vi.spyOn(approvalService, 'getDefinitionById').mockReturnValue(of(mockWorkflowDetail));

    component.workflows.set(mockWorkflows);
    component.openSubmitModal();

    expect(component.showSubmitModal()).toBe(true);
    expect(component.submitModel.workflowDefinitionId).toBe('def-1');
    expect(getDefSpy).toHaveBeenCalledWith('def-1');
    expect(component.selectedWorkflowDetail()?.steps.length).toBe(1);
  });

  it('submits new approval request and reloads list', () => {
    vi.spyOn(approvalService, 'listDefinitions').mockReturnValue(of(mockWorkflows));
    const listReqsSpy = vi.spyOn(approvalService, 'listRequests').mockReturnValue(of({
      items: mockRequests,
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    const submitSpy = vi.spyOn(approvalService, 'submitRequest').mockReturnValue(of({
      id: 'req-2',
      requestNumber: 1002,
      workflowDefinitionId: 'def-1',
      workflowName: 'Purchase Flow',
      requesterId: 'u-1',
      requesterName: 'Me',
      title: 'New Server',
      status: 'Pending',
      version: 1,
      submittedAtUtc: '2026-11-21'
    }));

    component.workflows.set(mockWorkflows);
    component.openSubmitModal();
    component.submitModel.title = 'New Server';
    component.submitModel.description = 'Server rack upgrade';
    component.onSubmitRequest();

    expect(submitSpy).toHaveBeenCalledWith(expect.objectContaining({ title: 'New Server' }));
    expect(component.showSubmitModal()).toBe(false);
    expect(component.successMessage()).toContain('Approval Request #1002 submitted');
  });
});
