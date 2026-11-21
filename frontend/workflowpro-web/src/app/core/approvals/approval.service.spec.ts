import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApprovalService } from './approval.service';
import { PagedResult } from '../projects/project.models';
import {
  ApprovalRequestDetail,
  ApprovalRequestSummary,
  CreateWorkflowDefinitionRequest,
  RecordDecisionRequest,
  SubmitApprovalRequest,
  WorkflowDefinitionSummary
} from './approval.models';

describe('ApprovalService', () => {
  let service: ApprovalService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(ApprovalService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('listDefinitions fetches workflow definitions', () => {
    const mockDefs: WorkflowDefinitionSummary[] = [{
      id: 'def-1',
      name: 'Travel Signoff',
      description: 'Travel expenses',
      isActive: true,
      stepCount: 2,
      createdById: 'u-1',
      createdAtUtc: '2026-11-21'
    }];

    service.listDefinitions().subscribe(res => {
      expect(res).toEqual(mockDefs);
    });

    const req = httpTesting.expectOne('/api/v1/workflow-definitions?includeInactive=false');
    expect(req.request.method).toBe('GET');
    req.flush(mockDefs);
  });

  it('createDefinition sends POST request with definition data', () => {
    const payload: CreateWorkflowDefinitionRequest = {
      name: 'New Flow',
      description: 'Testing',
      steps: [{ stepOrder: 1, name: 'Step 1', approverRole: 'Manager', approverUserId: null }]
    };

    service.createDefinition(payload).subscribe();

    const req = httpTesting.expectOne('/api/v1/workflow-definitions');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ ...payload, id: 'def-new', isActive: true, createdById: 'u-1', createdByName: 'Admin', createdAtUtc: '2026-11-21', steps: [] });
  });

  it('listRequests sends correct query parameters for inbox', () => {
    const mockResult: PagedResult<ApprovalRequestSummary> = {
      items: [{
        id: 'req-1',
        requestNumber: 1001,
        workflowDefinitionId: 'def-1',
        workflowName: 'Travel Signoff',
        requesterId: 'u-2',
        requesterName: 'Jane Requester',
        title: 'Trip to Tokyo',
        status: 'Pending',
        currentStepOrder: 1,
        currentStepName: 'Manager Signoff',
        currentApproverId: 'u-1',
        currentApproverName: 'Admin Approver',
        version: 1,
        submittedAtUtc: '2026-11-21'
      }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    };

    service.listRequests({ inboxOnly: true, status: 'Pending', page: 1, pageSize: 20 }).subscribe(res => {
      expect(res).toEqual(mockResult);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/approvals' &&
      r.params.get('inboxOnly') === 'true' &&
      r.params.get('status') === 'Pending' &&
      r.params.get('page') === '1' &&
      r.params.get('pageSize') === '20'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('submitRequest posts new approval request', () => {
    const payload: SubmitApprovalRequest = {
      workflowDefinitionId: 'def-1',
      title: 'Monitor Purchase',
      description: 'Dell 32 inch 4k',
      stepApproverOverrides: null
    };

    service.submitRequest(payload).subscribe();

    const req = httpTesting.expectOne('/api/v1/approvals');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ id: 'req-10', requestNumber: 1010, title: 'Monitor Purchase' });
  });

  it('recordDecision posts decision and expected version', () => {
    const payload: RecordDecisionRequest = {
      decision: 'Approved',
      comment: 'Looks solid',
      expectedVersion: 1
    };

    service.recordDecision('req-1', payload).subscribe();

    const req = httpTesting.expectOne('/api/v1/approvals/req-1/decisions');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ id: 'req-1', status: 'Approved', version: 2 });
  });

  it('cancelRequest posts cancel with expected version', () => {
    service.cancelRequest('req-1', { expectedVersion: 1 }).subscribe();

    const req = httpTesting.expectOne('/api/v1/approvals/req-1/cancel');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ expectedVersion: 1 });
    req.flush({ id: 'req-1', status: 'Cancelled', version: 2 });
  });
});
