import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { TicketDetailComponent } from './ticket-detail.component';
import { TicketService } from '../../core/tickets/ticket.service';
import { AuthService } from '../../core/auth/auth.service';
import { TicketDetail, TicketSummary } from '../../core/tickets/ticket.models';

describe('TicketDetailComponent', () => {
  let component: TicketDetailComponent;
  let fixture: ComponentFixture<TicketDetailComponent>;
  let ticketService: TicketService;
  let authService: AuthService;

  const mockSummary: TicketSummary = {
    id: 'tick-200',
    ticketNumber: 1042,
    subject: 'Cannot access internal dashboard',
    category: 'Access',
    priority: 'High',
    status: 'Assigned',
    requesterId: 'user-req',
    requesterName: 'Jane Requester',
    assigneeId: 'agent-1',
    assigneeName: 'Bob Agent',
    messageCount: 2,
    createdAtUtc: '2026-11-21T10:00:00Z'
  };

  const mockTicket: TicketDetail = {
    id: 'tick-200',
    ticketNumber: 1042,
    requesterId: 'user-req',
    requesterName: 'Jane Requester',
    requesterEmail: 'jane@example.com',
    assigneeId: 'agent-1',
    assigneeName: 'Bob Agent',
    subject: 'Cannot access internal dashboard',
    description: 'Received 403 Forbidden when accessing analytics page',
    category: 'Access',
    priority: 'High',
    status: 'Assigned',
    createdAtUtc: '2026-11-21T10:00:00Z',
    messages: [
      {
        id: 'msg-1',
        ticketId: 'tick-200',
        authorId: 'user-req',
        authorName: 'Jane Requester',
        body: 'Here is the screenshot of the error.',
        isInternalNote: false,
        createdAtUtc: '2026-11-21T10:05:00Z'
      },
      {
        id: 'msg-2',
        ticketId: 'tick-200',
        authorId: 'agent-1',
        authorName: 'Bob Agent',
        body: 'Internal note: checked Okta group membership, user missing group.',
        isInternalNote: true,
        createdAtUtc: '2026-11-21T10:15:00Z'
      }
    ],
    history: [
      {
        id: 'hist-1',
        ticketId: 'tick-200',
        fromStatus: null,
        toStatus: 'New',
        changedById: 'user-req',
        changedByName: 'Jane Requester',
        changedAtUtc: '2026-11-21T10:00:00Z',
        comment: 'Ticket created.'
      },
      {
        id: 'hist-2',
        ticketId: 'tick-200',
        fromStatus: 'New',
        toStatus: 'Assigned',
        changedById: 'agent-1',
        changedByName: 'Bob Agent',
        changedAtUtc: '2026-11-21T10:10:00Z',
        comment: 'Assigned to self.'
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? 'tick-200' : null
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketDetailComponent);
    component = fixture.componentInstance;
    ticketService = TestBed.inject(TicketService);
    authService = TestBed.inject(AuthService);
  });

  it('loads ticket details and messages on init for agent', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    const getByIdSpy = vi.spyOn(ticketService, 'getById').mockReturnValue(of(mockTicket));
    const getAgentsSpy = vi.spyOn(ticketService, 'getAgents').mockReturnValue(of([]));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(getByIdSpy).toHaveBeenCalledWith('tick-200');
    expect(getAgentsSpy).toHaveBeenCalled();
    expect(component.ticket()?.ticketNumber).toBe(1042);
    expect(component.ticket()?.messages.length).toBe(2);
    expect(component.ticket()?.history.length).toBe(2);
  });

  it('filters transitions appropriately based on user role', () => {
    // For support agent
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    expect(component.getAvailableTransitions('Assigned')).toContain('InProgress');
    expect(component.getAvailableTransitions('Assigned')).toContain('WaitingForRequester');

    // For standard requester
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(false);
    expect(component.getAvailableTransitions('Assigned')).toEqual(['Closed']);
  });

  it('executes status transition to InProgress', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    vi.spyOn(ticketService, 'getById').mockReturnValue(of(mockTicket));
    vi.spyOn(ticketService, 'getAgents').mockReturnValue(of([]));
    fixture.detectChanges();

    const changeStatusSpy = vi.spyOn(ticketService, 'changeStatus').mockReturnValue(of({
      ...mockSummary,
      status: 'InProgress'
    }));

    component.promptStatusTransition('InProgress');
    expect(component.showTransitionModal()).toBe(true);

    component.statusComment = 'Starting investigation';
    component.executeStatusTransition();

    expect(changeStatusSpy).toHaveBeenCalledWith('tick-200', {
      status: 'InProgress',
      comment: 'Starting investigation'
    });
    expect(component.showTransitionModal()).toBe(false);
  });

  it('adds internal note message when agent selects internal note', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    vi.spyOn(ticketService, 'getById').mockReturnValue(of(mockTicket));
    vi.spyOn(ticketService, 'getAgents').mockReturnValue(of([]));
    fixture.detectChanges();

    const addMessageSpy = vi.spyOn(ticketService, 'addMessage').mockReturnValue(of({
      id: 'msg-3',
      ticketId: 'tick-200',
      authorId: 'agent-1',
      authorName: 'Bob Agent',
      body: 'Verified IAM policy, applying fix.',
      isInternalNote: true,
      createdAtUtc: '2026-11-21T10:30:00Z'
    }));

    component.messageBody = 'Verified IAM policy, applying fix.';
    component.isInternalNote = true;
    component.onSendMessage();

    expect(addMessageSpy).toHaveBeenCalledWith('tick-200', {
      body: 'Verified IAM policy, applying fix.',
      isInternalNote: true
    });
    expect(component.ticket()?.messages.length).toBe(3);
    expect(component.messageBody).toBe('');
    expect(component.isInternalNote).toBe(false);
  });

  it('assigns ticket to new agent', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    vi.spyOn(ticketService, 'getById').mockReturnValue(of(mockTicket));
    vi.spyOn(ticketService, 'getAgents').mockReturnValue(of([]));
    fixture.detectChanges();

    const assignSpy = vi.spyOn(ticketService, 'assign').mockReturnValue(of({
      ...mockSummary,
      assigneeId: 'agent-2',
      assigneeName: 'Agent Cooper'
    }));

    component.onAssigneeChanged('agent-2');

    expect(assignSpy).toHaveBeenCalledWith('tick-200', { assigneeId: 'agent-2' });
  });
});
