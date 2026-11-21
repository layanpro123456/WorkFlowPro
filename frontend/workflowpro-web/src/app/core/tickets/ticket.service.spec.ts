import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TicketService } from './ticket.service';
import { PagedResult } from '../projects/project.models';
import { SupportAgent, TicketDetail, TicketMessage, TicketStatusHistory, TicketSummary } from './ticket.models';

describe('TicketService', () => {
  let service: TicketService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(TicketService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('list sends correct query parameters', () => {
    const mockResult: PagedResult<TicketSummary> = {
      items: [{
        id: 'tick-1',
        ticketNumber: 1001,
        requesterId: 'u-1',
        requesterName: 'Jane Doe',
        subject: 'VPN Connection Failure',
        category: 'Technical',
        priority: 'High',
        status: 'New',
        messageCount: 1,
        createdAtUtc: '2026-11-21'
      }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    };

    service.list({ status: 'New', priority: 'High', category: 'Technical', search: 'VPN', myTicketsOnly: true, page: 1, pageSize: 20 }).subscribe(res => {
      expect(res).toEqual(mockResult);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/tickets' &&
      r.params.get('status') === 'New' &&
      r.params.get('priority') === 'High' &&
      r.params.get('category') === 'Technical' &&
      r.params.get('search') === 'VPN' &&
      r.params.get('myTicketsOnly') === 'true' &&
      r.params.get('page') === '1' &&
      r.params.get('pageSize') === '20'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('getById retrieves ticket details', () => {
    const mockDetail: TicketDetail = {
      id: 'tick-1',
      ticketNumber: 1001,
      requesterId: 'u-1',
      requesterName: 'Jane Doe',
      requesterEmail: 'jane@example.com',
      subject: 'VPN Issue',
      description: 'Cannot connect to Cisco VPN',
      category: 'Technical',
      priority: 'High',
      status: 'New',
      createdAtUtc: '2026-11-21',
      messages: [],
      history: []
    };

    service.getById('tick-1').subscribe(res => {
      expect(res).toEqual(mockDetail);
    });

    const req = httpTesting.expectOne('/api/v1/tickets/tick-1');
    expect(req.request.method).toBe('GET');
    req.flush(mockDetail);
  });

  it('create sends POST request with ticket data', () => {
    const payload: { subject: string; description: string; category: any; priority: any } = {
      subject: 'Hardware replacement',
      description: 'Monitor flickering',
      category: 'Hardware',
      priority: 'Normal'
    };

    const mockSummary: TicketSummary = {
      id: 'tick-2',
      ticketNumber: 1002,
      requesterId: 'u-1',
      requesterName: 'Jane Doe',
      subject: 'Hardware replacement',
      category: 'Hardware',
      priority: 'Normal',
      status: 'New',
      messageCount: 0,
      createdAtUtc: '2026-11-21'
    };

    service.create(payload).subscribe(res => {
      expect(res).toEqual(mockSummary);
    });

    const req = httpTesting.expectOne('/api/v1/tickets');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(mockSummary);
  });

  it('assign sends PUT request with assigneeId', () => {
    const payload = { assigneeId: 'agent-1' };
    const mockSummary: TicketSummary = {
      id: 'tick-1',
      ticketNumber: 1001,
      requesterId: 'u-1',
      requesterName: 'Jane Doe',
      assigneeId: 'agent-1',
      assigneeName: 'Agent Smith',
      subject: 'VPN Issue',
      category: 'Technical',
      priority: 'High',
      status: 'Assigned',
      messageCount: 0,
      createdAtUtc: '2026-11-21'
    };

    service.assign('tick-1', payload).subscribe(res => {
      expect(res).toEqual(mockSummary);
    });

    const req = httpTesting.expectOne('/api/v1/tickets/tick-1/assign');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(payload);
    req.flush(mockSummary);
  });

  it('changeStatus sends PATCH request with new status and comment', () => {
    const payload: { status: any; comment: string } = { status: 'InProgress', comment: 'Investigating network logs' };
    const mockSummary: TicketSummary = {
      id: 'tick-1',
      ticketNumber: 1001,
      requesterId: 'u-1',
      requesterName: 'Jane',
      subject: 'VPN Issue',
      category: 'Technical',
      priority: 'High',
      status: 'InProgress',
      messageCount: 0,
      createdAtUtc: '2026-11-21'
    };

    service.changeStatus('tick-1', payload).subscribe(res => {
      expect(res).toEqual(mockSummary);
    });

    const req = httpTesting.expectOne('/api/v1/tickets/tick-1/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual(payload);
    req.flush(mockSummary);
  });

  it('addMessage sends POST request with isInternalNote flag', () => {
    const payload = { body: 'Internal agent debugging note', isInternalNote: true };
    const mockMessage: TicketMessage = {
      id: 'msg-1',
      ticketId: 'tick-1',
      authorId: 'agent-1',
      authorName: 'Agent Smith',
      body: 'Internal agent debugging note',
      isInternalNote: true,
      createdAtUtc: '2026-11-21'
    };

    service.addMessage('tick-1', payload).subscribe(res => {
      expect(res).toEqual(mockMessage);
    });

    const req = httpTesting.expectOne('/api/v1/tickets/tick-1/messages');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(mockMessage);
  });

  it('getAgents fetches available support agents', () => {
    const mockAgents: SupportAgent[] = [
      { userId: 'agent-1', displayName: 'Agent 1', email: 'agent1@workflowpro.local', primaryRole: 'SupportAgent' }
    ];

    service.getAgents().subscribe(res => {
      expect(res).toEqual(mockAgents);
    });

    const req = httpTesting.expectOne('/api/v1/tickets/agents');
    expect(req.request.method).toBe('GET');
    req.flush(mockAgents);
  });
});
