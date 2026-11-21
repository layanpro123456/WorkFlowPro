import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { TicketListComponent } from './ticket-list.component';
import { TicketService } from '../../core/tickets/ticket.service';
import { AuthService } from '../../core/auth/auth.service';
import { ProjectService } from '../../core/projects/project.service';
import { TicketSummary } from '../../core/tickets/ticket.models';

describe('TicketListComponent', () => {
  let component: TicketListComponent;
  let fixture: ComponentFixture<TicketListComponent>;
  let ticketService: TicketService;
  let authService: AuthService;
  let projectService: ProjectService;

  const mockTickets: TicketSummary[] = [
    {
      id: 'tick-100',
      ticketNumber: 1001,
      requesterId: 'u-1',
      requesterName: 'Alice',
      subject: 'Hardware failure',
      category: 'Hardware',
      priority: 'High',
      status: 'New',
      messageCount: 0,
      createdAtUtc: '2026-11-21'
    },
    {
      id: 'tick-101',
      ticketNumber: 1002,
      requesterId: 'u-2',
      requesterName: 'Bob',
      subject: 'Billing question',
      category: 'Billing',
      priority: 'Normal',
      status: 'Assigned',
      messageCount: 2,
      createdAtUtc: '2026-11-21'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketListComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketListComponent);
    component = fixture.componentInstance;
    ticketService = TestBed.inject(TicketService);
    authService = TestBed.inject(AuthService);
    projectService = TestBed.inject(ProjectService);
  });

  it('loads tickets and projects on init', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    vi.spyOn(projectService, 'list').mockReturnValue(of({
      items: [],
      page: 1,
      pageSize: 100,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    const listSpy = vi.spyOn(ticketService, 'list').mockReturnValue(of({
      items: mockTickets,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(listSpy).toHaveBeenCalled();
    expect(component.tickets().length).toBe(2);
    expect(component.tickets()[0].ticketNumber).toBe(1001);
  });

  it('switches tabs and reloads with myTicketsOnly filter', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    vi.spyOn(projectService, 'list').mockReturnValue(of({
      items: [],
      page: 1,
      pageSize: 100,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    const listSpy = vi.spyOn(ticketService, 'list').mockReturnValue(of({
      items: [mockTickets[0]],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    fixture.detectChanges();
    component.setTab('mine');

    expect(component.activeTab()).toBe('mine');
    expect(listSpy).toHaveBeenCalledWith(expect.objectContaining({ myTicketsOnly: true }));
  });

  it('opens and closes create modal', () => {
    expect(component.showCreateModal()).toBe(false);
    component.openCreateModal();
    expect(component.showCreateModal()).toBe(true);
    expect(component.createModel.subject).toBe('');

    component.closeCreateModal();
    expect(component.showCreateModal()).toBe(false);
  });

  it('submits a ticket and refreshes list on success', () => {
    vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);
    vi.spyOn(projectService, 'list').mockReturnValue(of({
      items: [],
      page: 1,
      pageSize: 100,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    const listSpy = vi.spyOn(ticketService, 'list').mockReturnValue(of({
      items: mockTickets,
      page: 1,
      pageSize: 20,
      totalCount: 2,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    const createSpy = vi.spyOn(ticketService, 'create').mockReturnValue(of({
      id: 'tick-102',
      ticketNumber: 1003,
      requesterId: 'u-1',
      requesterName: 'Alice',
      subject: 'New issue',
      category: 'General',
      priority: 'Normal',
      status: 'New',
      messageCount: 0,
      createdAtUtc: '2026-11-21'
    }));

    fixture.detectChanges();

    component.openCreateModal();
    component.createModel.subject = 'New issue';
    component.createModel.description = 'Issue details';
    component.onCreateTicket();

    expect(createSpy).toHaveBeenCalledWith(expect.objectContaining({ subject: 'New issue' }));
    expect(component.showCreateModal()).toBe(false);
    expect(component.successMessage()).toContain('Ticket #1003 created successfully');
    expect(listSpy).toHaveBeenCalledTimes(2);
  });
});
