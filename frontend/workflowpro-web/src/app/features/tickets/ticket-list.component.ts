import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TicketService } from '../../core/tickets/ticket.service';
import { AuthService } from '../../core/auth/auth.service';
import { ProjectService } from '../../core/projects/project.service';
import { ProjectSummary } from '../../core/projects/project.models';
import {
  CreateTicketRequest,
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  TICKET_STATUSES,
  TicketCategory,
  TicketPriority,
  TicketSummary
} from '../../core/tickets/ticket.models';

@Component({
  selector: 'app-ticket-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="tickets-page">
      <header class="page-header">
        <div class="header-titles">
          <h1>Service Desk</h1>
          <p class="subtitle">Submit support requests, track resolutions, and manage agent assignments</p>
        </div>

        <div class="header-actions">
          <button class="primary-btn" (click)="openCreateModal()">
            + Submit Ticket
          </button>
        </div>
      </header>

      <!-- View Switcher (for Agents & Admins) -->
      @if (canViewAllTickets()) {
        <nav class="tabs-nav" role="tablist">
          <button
            type="button"
            class="tab-btn"
            [class.active]="activeTab() === 'all'"
            (click)="setTab('all')">
            All Tickets
          </button>
          <button
            type="button"
            class="tab-btn"
            [class.active]="activeTab() === 'mine'"
            (click)="setTab('mine')">
            My Submitted Tickets
          </button>
        </nav>
      }

      <!-- Filter Toolbar -->
      <section class="filters-card">
        <div class="filter-group">
          <label for="search-tickets">Search</label>
          <input
            id="search-tickets"
            type="text"
            placeholder="Search tickets..."
            [(ngModel)]="searchQuery"
            (keyup.enter)="loadTickets()" />
        </div>

        <div class="filter-group">
          <label for="status-filter">Status</label>
          <select id="status-filter" [(ngModel)]="statusFilter" (change)="loadTickets()">
            <option value="All">All Statuses</option>
            @for (s of ticketStatuses; track s) {
              <option [value]="s">{{ s }}</option>
            }
          </select>
        </div>

        <div class="filter-group">
          <label for="priority-filter">Priority</label>
          <select id="priority-filter" [(ngModel)]="priorityFilter" (change)="loadTickets()">
            <option value="All">All Priorities</option>
            @for (p of ticketPriorities; track p) {
              <option [value]="p">{{ p }}</option>
            }
          </select>
        </div>

        <div class="filter-group">
          <label for="category-filter">Category</label>
          <select id="category-filter" [(ngModel)]="categoryFilter" (change)="loadTickets()">
            <option value="All">All Categories</option>
            @for (c of ticketCategories; track c) {
              <option [value]="c">{{ c }}</option>
            }
          </select>
        </div>

        <div class="filter-actions">
          <button class="filter-btn" (click)="loadTickets()">Filter</button>
          <button class="reset-btn" (click)="resetFilters()">Reset</button>
        </div>
      </section>

      <!-- Status Alerts -->
      @if (errorMessage()) {
        <div class="alert alert-error" role="alert">
          <span>{{ errorMessage() }}</span>
          <button class="alert-close" (click)="errorMessage.set('')">✕</button>
        </div>
      }
      @if (successMessage()) {
        <div class="alert alert-success" role="alert">
          <span>{{ successMessage() }}</span>
          <button class="alert-close" (click)="successMessage.set('')">✕</button>
        </div>
      }

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Loading tickets...</p>
        </div>
      } @else {
        <!-- Data Table -->
        <div class="table-card">
          <table class="data-table">
            <thead>
              <tr>
                <th>Ticket #</th>
                <th>Subject</th>
                <th>Category</th>
                <th>Priority</th>
                <th>Status</th>
                <th>Requester</th>
                <th>Assignee</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (ticket of tickets(); track ticket.id) {
                <tr>
                  <td><span class="ticket-num">#{{ ticket.ticketNumber }}</span></td>
                  <td class="subject-cell">
                    <a [routerLink]="['/tickets', ticket.id]" class="table-title-link">{{ ticket.subject }}</a>
                    @if (ticket.relatedProjectCode) {
                      <span class="project-pill">{{ ticket.relatedProjectCode }}</span>
                    }
                  </td>
                  <td><span class="category-pill">{{ ticket.category }}</span></td>
                  <td>
                    <span class="priority-tag" [class]="'tag-' + ticket.priority.toLowerCase()">
                      {{ ticket.priority }}
                    </span>
                  </td>
                  <td>
                    <span class="status-pill" [class]="'status-' + ticket.status.toLowerCase()">
                      {{ ticket.status }}
                    </span>
                  </td>
                  <td>{{ ticket.requesterName }}</td>
                  <td>{{ ticket.assigneeName || 'Unassigned' }}</td>
                  <td>{{ ticket.createdAtUtc | date:'mediumDate' }}</td>
                  <td>
                    <a [routerLink]="['/tickets', ticket.id]" class="action-btn">View</a>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="9" class="empty-cell">No tickets match the selected criteria.</td>
                </tr>
              }
            </tbody>
          </table>

          <!-- Pagination -->
          @if (totalPages() > 1) {
            <footer class="pagination-footer">
              <span class="page-info">
                Page {{ currentPage() }} of {{ totalPages() }} ({{ totalCount() }} tickets)
              </span>
              <div class="page-buttons">
                <button
                  class="btn-page"
                  [disabled]="currentPage() <= 1"
                  (click)="changePage(currentPage() - 1)">
                  Previous
                </button>
                <button
                  class="btn-page"
                  [disabled]="currentPage() >= totalPages()"
                  (click)="changePage(currentPage() + 1)">
                  Next
                </button>
              </div>
            </footer>
          }
        </div>
      }

      <!-- Submit Ticket Modal -->
      @if (showCreateModal()) {
        <div class="modal-backdrop" (click)="closeCreateModal()">
          <div class="modal-card" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
            <header class="modal-header">
              <h2>Submit Support Ticket</h2>
              <button class="modal-close" (click)="closeCreateModal()">✕</button>
            </header>

            <form (ngSubmit)="onCreateTicket()" class="modal-form">
              <div class="form-group">
                <label for="create-subject">Subject *</label>
                <input
                  id="create-subject"
                  type="text"
                  [(ngModel)]="createModel.subject"
                  name="subject"
                  maxlength="200"
                  required
                  placeholder="Summary of the issue" />
              </div>

              <div class="form-group">
                <label for="create-description">Description *</label>
                <textarea
                  id="create-description"
                  rows="4"
                  [(ngModel)]="createModel.description"
                  name="description"
                  required
                  placeholder="Provide complete steps, expected vs actual behavior, error messages..."></textarea>
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label for="create-category">Category *</label>
                  <select id="create-category" [(ngModel)]="createModel.category" name="category" required>
                    @for (c of ticketCategories; track c) {
                      <option [value]="c">{{ c }}</option>
                    }
                  </select>
                </div>

                <div class="form-group">
                  <label for="create-priority">Priority *</label>
                  <select id="create-priority" [(ngModel)]="createModel.priority" name="priority" required>
                    @for (p of ticketPriorities; track p) {
                      <option [value]="p">{{ p }}</option>
                    }
                  </select>
                </div>
              </div>

              <div class="form-group">
                <label for="create-project">Related Project (optional)</label>
                <select id="create-project" [(ngModel)]="createModel.relatedProjectId" name="relatedProjectId">
                  <option [ngValue]="null">None</option>
                  @for (proj of projects(); track proj.id) {
                    <option [value]="proj.id">[{{ proj.projectCode }}] {{ proj.name }}</option>
                  }
                </select>
              </div>

              @if (modalError()) {
                <div class="modal-error">{{ modalError() }}</div>
              }

              <footer class="modal-footer">
                <button type="button" class="btn-cancel" (click)="closeCreateModal()">Cancel</button>
                <button type="submit" class="primary-btn" [disabled]="isSubmitting()">
                  {{ isSubmitting() ? 'Submitting...' : 'Submit Ticket' }}
                </button>
              </footer>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .tickets-page {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 1rem;
    }
    .header-titles h1 {
      margin: 0;
      font-size: 1.75rem;
      font-weight: 700;
      color: #0f172a;
    }
    .subtitle {
      margin: 0.25rem 0 0;
      color: #64748b;
      font-size: 0.95rem;
    }
    .primary-btn {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.55rem 1.1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .primary-btn:hover:not(:disabled) { background: #1d4ed8; }
    .tabs-nav {
      display: flex;
      gap: 0.5rem;
      border-bottom: 2px solid #e2e8f0;
      padding-bottom: 0.25rem;
    }
    .tab-btn {
      background: none;
      border: none;
      padding: 0.5rem 1rem;
      font-size: 0.95rem;
      font-weight: 600;
      color: #64748b;
      cursor: pointer;
      border-bottom: 2px solid transparent;
      margin-bottom: -0.35rem;
    }
    .tab-btn.active {
      color: #2563eb;
      border-bottom-color: #2563eb;
    }
    .filters-card {
      background: #ffffff;
      padding: 1rem 1.25rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      align-items: flex-end;
    }
    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      min-width: 150px;
      flex: 1;
    }
    .filter-group label {
      font-size: 0.8rem;
      font-weight: 600;
      color: #475569;
    }
    .filter-group input, .filter-group select {
      padding: 0.45rem 0.65rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
    }
    .filter-actions { display: flex; gap: 0.5rem; }
    .filter-btn {
      background: #0f172a;
      color: white;
      border: none;
      padding: 0.5rem 0.9rem;
      border-radius: 6px;
      cursor: pointer;
      font-weight: 500;
    }
    .reset-btn {
      background: #f1f5f9;
      color: #475569;
      border: 1px solid #cbd5e1;
      padding: 0.5rem 0.75rem;
      border-radius: 6px;
      cursor: pointer;
    }
    .table-card {
      background: #ffffff;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      overflow-x: auto;
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
      font-size: 0.88rem;
    }
    .data-table th, .data-table td {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #e2e8f0;
    }
    .data-table th {
      background: #f8fafc;
      color: #475569;
      font-weight: 600;
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    .ticket-num {
      font-family: monospace;
      font-weight: 700;
      color: #2563eb;
    }
    .subject-cell {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .table-title-link {
      font-weight: 600;
      color: #0f172a;
      text-decoration: none;
    }
    .table-title-link:hover { color: #2563eb; text-decoration: underline; }
    .project-pill {
      font-size: 0.7rem;
      background: #f1f5f9;
      padding: 1px 5px;
      border-radius: 4px;
      color: #64748b;
    }
    .category-pill {
      font-size: 0.75rem;
      background: #e2e8f0;
      padding: 2px 7px;
      border-radius: 4px;
      color: #334155;
    }
    .status-pill {
      display: inline-block;
      font-size: 0.75rem;
      padding: 2px 8px;
      border-radius: 12px;
      font-weight: 600;
    }
    .status-new { background: #e0f2fe; color: #0369a1; }
    .status-assigned { background: #e0e7ff; color: #3730a3; }
    .status-inprogress { background: #fef3c7; color: #92400e; }
    .status-waitingforrequester { background: #ffedd5; color: #c2410c; }
    .status-resolved { background: #dcfce7; color: #166534; }
    .status-closed { background: #f1f5f9; color: #64748b; }
    .priority-tag {
      font-size: 0.7rem;
      font-weight: 600;
      padding: 1px 6px;
      border-radius: 4px;
    }
    .tag-urgent { background: #fee2e2; color: #b91c1c; }
    .tag-high { background: #ffedd5; color: #c2410c; }
    .tag-normal { background: #e0f2fe; color: #0369a1; }
    .tag-low { background: #f1f5f9; color: #475569; }
    .action-btn { color: #2563eb; text-decoration: none; font-weight: 500; }
    .empty-cell { text-align: center; color: #94a3b8; padding: 3rem !important; }
    .pagination-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem 1rem;
      background: #f8fafc;
      font-size: 0.85rem;
      color: #64748b;
    }
    .btn-page {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      padding: 0.35rem 0.75rem;
      border-radius: 4px;
      cursor: pointer;
    }
    .btn-page:disabled { opacity: 0.5; cursor: not-allowed; }
    .alert {
      padding: 0.75rem 1rem;
      border-radius: 6px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.9rem;
    }
    .alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
    .alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
    .alert-close { background: none; border: none; font-size: 1rem; cursor: pointer; color: inherit; }
    .loading-state { padding: 3rem; text-align: center; color: #64748b; }
    .spinner {
      width: 32px;
      height: 32px;
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 0.75rem;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(15, 23, 42, 0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
      padding: 1rem;
    }
    .modal-card {
      background: #ffffff;
      border-radius: 10px;
      width: 100%;
      max-width: 540px;
      box-shadow: 0 10px 25px rgba(0,0,0,0.2);
    }
    .modal-header {
      padding: 1rem 1.25rem;
      border-bottom: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .modal-header h2 { margin: 0; font-size: 1.2rem; }
    .modal-close { background: none; border: none; font-size: 1.1rem; cursor: pointer; color: #64748b; }
    .modal-form { padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.35rem; flex: 1; }
    .form-row { display: flex; gap: 1rem; }
    .form-group label { font-size: 0.8rem; font-weight: 600; color: #475569; }
    .form-group input, .form-group textarea, .form-group select {
      padding: 0.5rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
    }
    .modal-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 0.5rem; }
    .btn-cancel {
      background: #f1f5f9;
      border: 1px solid #cbd5e1;
      color: #475569;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      cursor: pointer;
    }
    .modal-error {
      background: #fee2e2;
      border: 1px solid #fecaca;
      color: #991b1b;
      padding: 0.5rem 0.75rem;
      border-radius: 6px;
      font-size: 0.85rem;
    }
  `]
})
export class TicketListComponent implements OnInit {
  private readonly ticketService = inject(TicketService);
  private readonly authService = inject(AuthService);
  private readonly projectService = inject(ProjectService);

  readonly ticketStatuses = TICKET_STATUSES;
  readonly ticketPriorities = TICKET_PRIORITIES;
  readonly ticketCategories = TICKET_CATEGORIES;

  activeTab = signal<'all' | 'mine'>('all');
  tickets = signal<TicketSummary[]>([]);
  projects = signal<ProjectSummary[]>([]);
  isLoading = signal<boolean>(false);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  currentPage = signal<number>(1);
  totalPages = signal<number>(1);
  totalCount = signal<number>(0);

  searchQuery = '';
  statusFilter = 'All';
  priorityFilter = 'All';
  categoryFilter = 'All';

  // Modal
  showCreateModal = signal<boolean>(false);
  isSubmitting = signal<boolean>(false);
  modalError = signal<string>('');
  createModel: CreateTicketRequest = {
    subject: '',
    description: '',
    category: 'General',
    priority: 'Normal',
    relatedProjectId: null
  };

  canViewAllTickets(): boolean {
    return this.authService.hasAnyRole(['SupportAgent', 'Manager', 'Administrator']);
  }

  ngOnInit(): void {
    if (!this.canViewAllTickets()) {
      this.activeTab.set('mine');
    }
    this.loadTickets();
    this.loadProjects();
  }

  setTab(tab: 'all' | 'mine'): void {
    this.activeTab.set(tab);
    this.currentPage.set(1);
    this.loadTickets();
  }

  loadTickets(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    const myTicketsOnly = this.activeTab() === 'mine' || !this.canViewAllTickets();

    this.ticketService.list({
      status: this.statusFilter !== 'All' ? this.statusFilter : undefined,
      priority: this.priorityFilter !== 'All' ? this.priorityFilter : undefined,
      category: this.categoryFilter !== 'All' ? this.categoryFilter : undefined,
      search: this.searchQuery.trim() || undefined,
      myTicketsOnly,
      page: this.currentPage(),
      pageSize: 20
    }).subscribe({
      next: (res) => {
        this.tickets.set(res.items);
        this.currentPage.set(res.page);
        this.totalPages.set(res.totalPages);
        this.totalCount.set(res.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Failed to load tickets: ' + (err.error?.detail || err.message));
        this.isLoading.set(false);
      }
    });
  }

  loadProjects(): void {
    this.projectService.list('All', undefined, 1, 100).subscribe({
      next: (res) => this.projects.set(res.items),
      error: () => this.projects.set([])
    });
  }

  resetFilters(): void {
    this.searchQuery = '';
    this.statusFilter = 'All';
    this.priorityFilter = 'All';
    this.categoryFilter = 'All';
    this.currentPage.set(1);
    this.loadTickets();
  }

  changePage(newPage: number): void {
    this.currentPage.set(newPage);
    this.loadTickets();
  }

  openCreateModal(): void {
    this.modalError.set('');
    this.createModel = {
      subject: '',
      description: '',
      category: 'General',
      priority: 'Normal',
      relatedProjectId: null
    };
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
    this.modalError.set('');
  }

  onCreateTicket(): void {
    if (!this.createModel.subject.trim() || !this.createModel.description.trim()) {
      this.modalError.set('Subject and description are required.');
      return;
    }

    this.isSubmitting.set(true);
    this.modalError.set('');

    this.ticketService.create(this.createModel).subscribe({
      next: (created) => {
        this.successMessage.set(`Ticket #${created.ticketNumber} created successfully.`);
        this.isSubmitting.set(false);
        this.closeCreateModal();
        this.loadTickets();
      },
      error: (err) => {
        this.modalError.set(err.error?.detail || 'Failed to submit ticket.');
        this.isSubmitting.set(false);
      }
    });
  }
}
