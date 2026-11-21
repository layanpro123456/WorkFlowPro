import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ReportService } from '../../core/reports/report.service';
import {
  TaskReport,
  TaskReportFilterParams,
  TicketReport,
  TicketReportFilterParams
} from '../../core/reports/report.models';
import { TASK_STATUSES } from '../../core/tasks/task.models';
import { TICKET_CATEGORIES, TICKET_PRIORITIES } from '../../core/tickets/ticket.models';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="reports-page">
      <header class="page-header">
        <div>
          <h1>Reports & Analytics</h1>
          <p class="page-subtitle">Bounded operational summaries, breakdown distributions, and CSV exports.</p>
        </div>
      </header>

      <!-- Tabs Navigation -->
      <nav class="report-tabs">
        <button
          type="button"
          class="tab-btn"
          [class.active]="activeTab() === 'tasks'"
          (click)="setTab('tasks')">
          📋 Tasks Report
        </button>
        <button
          type="button"
          class="tab-btn"
          [class.active]="activeTab() === 'tickets'"
          (click)="setTab('tickets')">
          🎫 Service Desk Report
        </button>
      </nav>

      <!-- TASKS REPORT TAB -->
      @if (activeTab() === 'tasks') {
        <section class="report-section">
          <!-- Filters Card -->
          <div class="filter-card">
            <div class="filter-grid">
              <div class="filter-group">
                <label for="task-status">Status</label>
                <select id="task-status" [(ngModel)]="taskFilters.status">
                  <option value="All">All Statuses</option>
                  @for (status of taskStatuses; track status) {
                    <option [value]="status">{{ status }}</option>
                  }
                </select>
              </div>

              <div class="filter-group">
                <label for="task-start-date">From Date</label>
                <input id="task-start-date" type="date" [(ngModel)]="taskFilters.startDate" />
              </div>

              <div class="filter-group">
                <label for="task-end-date">To Date</label>
                <input id="task-end-date" type="date" [(ngModel)]="taskFilters.endDate" />
              </div>

              <div class="filter-actions">
                <button type="button" class="btn btn-primary" (click)="loadTaskReport(1)">Apply Filter</button>
                <button type="button" class="btn btn-secondary" (click)="resetTaskFilters()">Reset</button>
                <button
                  type="button"
                  class="btn btn-export"
                  [disabled]="exporting()"
                  (click)="exportTasksCsv()">
                  📥 Export CSV
                </button>
              </div>
            </div>
          </div>

          @if (taskLoading()) {
            <div class="loading-state">
              <div class="spinner"></div>
              <p>Generating task report...</p>
            </div>
          } @else if (taskError()) {
            <div class="error-banner">{{ taskError() }}</div>
          } @else if (taskReport(); as tr) {
            <!-- KPI Summary Cards -->
            <div class="kpi-grid">
              <div class="kpi-card">
                <span class="kpi-val">{{ tr.totalTasks }}</span>
                <span class="kpi-lbl">Total Tasks</span>
              </div>
              <div class="kpi-card">
                <span class="kpi-val">{{ tr.completedTasks }}</span>
                <span class="kpi-lbl">Completed</span>
              </div>
              <div class="kpi-card">
                <span class="kpi-val">{{ tr.completionRate | number:'1.0-1' }}%</span>
                <span class="kpi-lbl">Completion Rate</span>
              </div>
              <div class="kpi-card" [class.warning]="tr.overdueTasks > 0">
                <span class="kpi-val">{{ tr.overdueTasks }}</span>
                <span class="kpi-lbl">Overdue</span>
              </div>
            </div>

            <!-- Distribution Breakdowns -->
            <div class="breakdown-grid">
              <div class="breakdown-card">
                <h3>Status Distribution</h3>
                <div class="pill-cloud">
                  @for (entry of getDistributionEntries(tr.statusDistribution); track entry.key) {
                    <span class="stat-pill">
                      <strong>{{ entry.key }}:</strong> {{ entry.value }}
                    </span>
                  }
                </div>
              </div>
              <div class="breakdown-card">
                <h3>Priority Breakdown</h3>
                <div class="pill-cloud">
                  @for (entry of getDistributionEntries(tr.priorityDistribution); track entry.key) {
                    <span class="stat-pill">
                      <strong>{{ entry.key }}:</strong> {{ entry.value }}
                    </span>
                  }
                </div>
              </div>
            </div>

            <!-- Report Results Table -->
            <div class="table-container">
              <table class="report-table">
                <thead>
                  <tr>
                    <th>Project</th>
                    <th>Title</th>
                    <th>Status</th>
                    <th>Priority</th>
                    <th>Assignee</th>
                    <th>Due Date</th>
                    <th>Created</th>
                  </tr>
                </thead>
                <tbody>
                  @for (task of tr.tasks.items; track task.taskId) {
                    <tr>
                      <td><span class="project-pill">{{ task.projectCode }}</span></td>
                      <td class="font-medium">{{ task.title }}</td>
                      <td><span class="status-badge" [attr.data-status]="task.status">{{ task.status }}</span></td>
                      <td><span class="priority-badge" [attr.data-priority]="task.priority">{{ task.priority }}</span></td>
                      <td>{{ task.assigneeName || '—' }}</td>
                      <td>{{ task.dueDateUtc ? (task.dueDateUtc | date:'shortDate') : '—' }}</td>
                      <td>{{ task.createdAtUtc | date:'shortDate' }}</td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="7" class="empty-cell">No tasks match the selected criteria.</td>
                    </tr>
                  }
                </tbody>
              </table>

              <!-- Pagination -->
              @if (tr.tasks.totalPages > 1) {
                <div class="pagination-bar">
                  <button
                    class="btn btn-sm btn-secondary"
                    [disabled]="tr.tasks.page <= 1"
                    (click)="loadTaskReport(tr.tasks.page - 1)">
                    Previous
                  </button>
                  <span class="page-indicator">
                    Page {{ tr.tasks.page }} of {{ tr.tasks.totalPages }} ({{ tr.tasks.totalCount }} tasks)
                  </span>
                  <button
                    class="btn btn-sm btn-secondary"
                    [disabled]="tr.tasks.page >= tr.tasks.totalPages"
                    (click)="loadTaskReport(tr.tasks.page + 1)">
                    Next
                  </button>
                </div>
              }
            </div>
          }
        </section>
      }

      <!-- TICKETS REPORT TAB -->
      @if (activeTab() === 'tickets') {
        <section class="report-section">
          <!-- Filters Card -->
          <div class="filter-card">
            <div class="filter-grid">
              <div class="filter-group">
                <label for="ticket-category">Category</label>
                <select id="ticket-category" [(ngModel)]="ticketFilters.category">
                  <option value="All">All Categories</option>
                  @for (cat of ticketCategories; track cat) {
                    <option [value]="cat">{{ cat }}</option>
                  }
                </select>
              </div>

              <div class="filter-group">
                <label for="ticket-priority">Priority</label>
                <select id="ticket-priority" [(ngModel)]="ticketFilters.priority">
                  <option value="All">All Priorities</option>
                  @for (priority of ticketPriorities; track priority) {
                    <option [value]="priority">{{ priority }}</option>
                  }
                </select>
              </div>

              <div class="filter-group">
                <label for="ticket-start-date">From Date</label>
                <input id="ticket-start-date" type="date" [(ngModel)]="ticketFilters.startDate" />
              </div>

              <div class="filter-group">
                <label for="ticket-end-date">To Date</label>
                <input id="ticket-end-date" type="date" [(ngModel)]="ticketFilters.endDate" />
              </div>

              <div class="filter-actions">
                <button type="button" class="btn btn-primary" (click)="loadTicketReport(1)">Apply Filter</button>
                <button type="button" class="btn btn-secondary" (click)="resetTicketFilters()">Reset</button>
                <button
                  type="button"
                  class="btn btn-export"
                  [disabled]="exporting()"
                  (click)="exportTicketsCsv()">
                  📥 Export CSV
                </button>
              </div>
            </div>
          </div>

          @if (ticketLoading()) {
            <div class="loading-state">
              <div class="spinner"></div>
              <p>Generating service desk report...</p>
            </div>
          } @else if (ticketError()) {
            <div class="error-banner">{{ ticketError() }}</div>
          } @else if (ticketReport(); as tkr) {
            <!-- KPI Summary Cards -->
            <div class="kpi-grid">
              <div class="kpi-card">
                <span class="kpi-val">{{ tkr.totalTickets }}</span>
                <span class="kpi-lbl">Total Tickets</span>
              </div>
              <div class="kpi-card">
                <span class="kpi-val">{{ tkr.resolvedTickets }}</span>
                <span class="kpi-lbl">Resolved / Closed</span>
              </div>
              <div class="kpi-card">
                <span class="kpi-val">{{ tkr.resolutionRate | number:'1.0-1' }}%</span>
                <span class="kpi-lbl">Resolution Rate</span>
              </div>
            </div>

            <!-- Distribution Breakdowns -->
            <div class="breakdown-grid">
              <div class="breakdown-card">
                <h3>Category Distribution</h3>
                <div class="pill-cloud">
                  @for (entry of getDistributionEntries(tkr.categoryDistribution); track entry.key) {
                    <span class="stat-pill">
                      <strong>{{ entry.key }}:</strong> {{ entry.value }}
                    </span>
                  }
                </div>
              </div>
              <div class="breakdown-card">
                <h3>Priority Breakdown</h3>
                <div class="pill-cloud">
                  @for (entry of getDistributionEntries(tkr.priorityDistribution); track entry.key) {
                    <span class="stat-pill">
                      <strong>{{ entry.key }}:</strong> {{ entry.value }}
                    </span>
                  }
                </div>
              </div>
              <div class="breakdown-card">
                <h3>Status Distribution</h3>
                <div class="pill-cloud">
                  @for (entry of getDistributionEntries(tkr.statusDistribution); track entry.key) {
                    <span class="stat-pill">
                      <strong>{{ entry.key }}:</strong> {{ entry.value }}
                    </span>
                  }
                </div>
              </div>
            </div>

            <!-- Report Results Table -->
            <div class="table-container">
              <table class="report-table">
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
                    <th>Resolved</th>
                  </tr>
                </thead>
                <tbody>
                  @for (ticket of tkr.tickets.items; track ticket.ticketNumber) {
                    <tr>
                      <td><strong>#{{ ticket.ticketNumber }}</strong></td>
                      <td class="font-medium">{{ ticket.subject }}</td>
                      <td><span class="meta-tag">{{ ticket.category }}</span></td>
                      <td><span class="priority-badge" [attr.data-priority]="ticket.priority">{{ ticket.priority }}</span></td>
                      <td><span class="status-badge" [attr.data-status]="ticket.status">{{ ticket.status }}</span></td>
                      <td>{{ ticket.requesterName }}</td>
                      <td>{{ ticket.assigneeName || '—' }}</td>
                      <td>{{ ticket.createdAtUtc | date:'shortDate' }}</td>
                      <td>{{ ticket.resolvedAtUtc ? (ticket.resolvedAtUtc | date:'shortDate') : '—' }}</td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="9" class="empty-cell">No tickets match the selected criteria.</td>
                    </tr>
                  }
                </tbody>
              </table>

              <!-- Pagination -->
              @if (tkr.tickets.totalPages > 1) {
                <div class="pagination-bar">
                  <button
                    class="btn btn-sm btn-secondary"
                    [disabled]="tkr.tickets.page <= 1"
                    (click)="loadTicketReport(tkr.tickets.page - 1)">
                    Previous
                  </button>
                  <span class="page-indicator">
                    Page {{ tkr.tickets.page }} of {{ tkr.tickets.totalPages }} ({{ tkr.tickets.totalCount }} tickets)
                  </span>
                  <button
                    class="btn btn-sm btn-secondary"
                    [disabled]="tkr.tickets.page >= tkr.tickets.totalPages"
                    (click)="loadTicketReport(tkr.tickets.page + 1)">
                    Next
                  </button>
                </div>
              }
            </div>
          }
        </section>
      }
    </div>
  `,
  styles: [`
    .reports-page {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .page-header h1 {
      margin: 0;
      font-size: 1.6rem;
      color: #0f172a;
    }
    .page-subtitle {
      margin: 0.25rem 0 0;
      color: #64748b;
      font-size: 0.9rem;
    }
    .report-tabs {
      display: flex;
      gap: 0.5rem;
      border-bottom: 2px solid #e2e8f0;
    }
    .tab-btn {
      padding: 0.65rem 1.25rem;
      background: none;
      border: none;
      font-size: 0.9rem;
      font-weight: 600;
      color: #64748b;
      cursor: pointer;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      transition: all 0.15s ease-in-out;
    }
    .tab-btn:hover { color: #1e293b; }
    .tab-btn.active {
      color: #2563eb;
      border-bottom-color: #2563eb;
    }
    .report-section {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .filter-card {
      background: #ffffff;
      padding: 1.25rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    .filter-grid {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: 1rem;
    }
    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
    }
    .filter-group label {
      font-size: 0.75rem;
      font-weight: 600;
      color: #475569;
      text-transform: uppercase;
    }
    .filter-group select,
    .filter-group input {
      padding: 0.45rem 0.65rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.85rem;
      color: #1e293b;
      background: #ffffff;
      min-width: 140px;
    }
    .filter-actions {
      display: flex;
      gap: 0.5rem;
      margin-left: auto;
    }
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
      gap: 1rem;
    }
    .kpi-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1rem 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .kpi-card.warning {
      background: #fffbeb;
      border-color: #fed7aa;
    }
    .kpi-val {
      font-size: 1.5rem;
      font-weight: 700;
      color: #0f172a;
    }
    .kpi-lbl {
      font-size: 0.75rem;
      color: #64748b;
      font-weight: 600;
      text-transform: uppercase;
    }
    .breakdown-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 1rem;
    }
    .breakdown-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1rem 1.25rem;
    }
    .breakdown-card h3 {
      margin: 0 0 0.75rem;
      font-size: 0.85rem;
      color: #475569;
      text-transform: uppercase;
      letter-spacing: 0.025em;
    }
    .pill-cloud {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
    }
    .stat-pill {
      background: #f1f5f9;
      color: #334155;
      font-size: 0.75rem;
      padding: 3px 8px;
      border-radius: 12px;
      border: 1px solid #e2e8f0;
    }
    .table-container {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      overflow-x: auto;
    }
    .report-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.85rem;
    }
    .report-table th {
      text-align: left;
      padding: 0.65rem 0.85rem;
      background: #f8fafc;
      color: #64748b;
      font-weight: 600;
      font-size: 0.75rem;
      border-bottom: 1px solid #e2e8f0;
      text-transform: uppercase;
    }
    .report-table td {
      padding: 0.65rem 0.85rem;
      border-bottom: 1px solid #f1f5f9;
      color: #334155;
    }
    .font-medium { font-weight: 500; color: #0f172a; }
    .project-pill {
      background: #f1f5f9;
      padding: 2px 6px;
      border-radius: 4px;
      font-weight: 600;
      font-size: 0.75rem;
      color: #475569;
    }
    .meta-tag {
      background: #e2e8f0;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 0.75rem;
      color: #334155;
    }
    .status-badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 0.75rem;
      font-weight: 600;
      background: #f1f5f9;
      color: #475569;
    }
    .status-badge[data-status="InProgress"],
    .status-badge[data-status="Assigned"] {
      background: #e0f2fe;
      color: #0284c7;
    }
    .status-badge[data-status="Done"],
    .status-badge[data-status="Resolved"] {
      background: #dcfce7;
      color: #16a34a;
    }
    .status-badge[data-status="Blocked"] {
      background: #fee2e2;
      color: #dc2626;
    }
    .priority-badge {
      font-size: 0.75rem;
      font-weight: 600;
    }
    .priority-badge[data-priority="Urgent"],
    .priority-badge[data-priority="Critical"] { color: #dc2626; }
    .priority-badge[data-priority="High"] { color: #ea580c; }
    .priority-badge[data-priority="Normal"] { color: #0284c7; }
    .priority-badge[data-priority="Low"] { color: #64748b; }
    .empty-cell {
      padding: 2rem;
      text-align: center;
      color: #94a3b8;
    }
    .pagination-bar {
      padding: 0.75rem 1rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-top: 1px solid #e2e8f0;
      background: #f8fafc;
    }
    .page-indicator {
      font-size: 0.8rem;
      color: #64748b;
    }
    .btn {
      padding: 0.45rem 0.85rem;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
      border: none;
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
    .btn-primary {
      background: #2563eb;
      color: white;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .btn-secondary {
      background: #f1f5f9;
      color: #334155;
      border: 1px solid #cbd5e1;
    }
    .btn-secondary:hover { background: #e2e8f0; }
    .btn-export {
      background: #10b981;
      color: white;
    }
    .btn-export:hover { background: #059669; }
    .btn-export:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .btn-sm {
      padding: 0.25rem 0.6rem;
      font-size: 0.75rem;
    }
    .loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 3rem;
      color: #64748b;
      gap: 0.75rem;
    }
    .spinner {
      width: 28px;
      height: 28px;
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    .error-banner {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
      padding: 0.85rem 1rem;
      border-radius: 6px;
      font-size: 0.85rem;
    }
  `]
})
export class ReportsComponent implements OnInit {
  private readonly reportService = inject(ReportService);

  readonly taskStatuses = TASK_STATUSES;
  readonly ticketCategories = TICKET_CATEGORIES;
  readonly ticketPriorities = TICKET_PRIORITIES;

  activeTab = signal<'tasks' | 'tickets'>('tasks');
  exporting = signal<boolean>(false);

  // Task report state
  taskReport = signal<TaskReport | null>(null);
  taskLoading = signal<boolean>(false);
  taskError = signal<string | null>(null);
  taskFilters: TaskReportFilterParams = {
    status: 'All',
    startDate: '',
    endDate: '',
    page: 1,
    pageSize: 20
  };

  // Ticket report state
  ticketReport = signal<TicketReport | null>(null);
  ticketLoading = signal<boolean>(false);
  ticketError = signal<string | null>(null);
  ticketFilters: TicketReportFilterParams = {
    category: 'All',
    priority: 'All',
    startDate: '',
    endDate: '',
    page: 1,
    pageSize: 20
  };

  ngOnInit(): void {
    this.loadTaskReport(1);
  }

  setTab(tab: 'tasks' | 'tickets'): void {
    this.activeTab.set(tab);
    if (tab === 'tasks' && !this.taskReport()) {
      this.loadTaskReport(1);
    } else if (tab === 'tickets' && !this.ticketReport()) {
      this.loadTicketReport(1);
    }
  }

  loadTaskReport(page: number): void {
    this.taskLoading.set(true);
    this.taskError.set(null);
    this.taskFilters.page = page;

    this.reportService.getTaskReport(this.taskFilters).subscribe({
      next: (res) => {
        this.taskReport.set(res);
        this.taskLoading.set(false);
      },
      error: () => {
        this.taskError.set('Failed to generate task report.');
        this.taskLoading.set(false);
      }
    });
  }

  resetTaskFilters(): void {
    this.taskFilters = {
      status: 'All',
      startDate: '',
      endDate: '',
      page: 1,
      pageSize: 20
    };
    this.loadTaskReport(1);
  }

  exportTasksCsv(): void {
    this.exporting.set(true);
    this.reportService.exportTaskReportCsv(this.taskFilters).subscribe({
      next: (blob) => {
        this.downloadBlob(blob, 'tasks-report.csv');
        this.exporting.set(false);
      },
      error: () => {
        this.taskError.set('Failed to export task report CSV.');
        this.exporting.set(false);
      }
    });
  }

  loadTicketReport(page: number): void {
    this.ticketLoading.set(true);
    this.ticketError.set(null);
    this.ticketFilters.page = page;

    this.reportService.getTicketReport(this.ticketFilters).subscribe({
      next: (res) => {
        this.ticketReport.set(res);
        this.ticketLoading.set(false);
      },
      error: () => {
        this.ticketError.set('Failed to generate service desk report.');
        this.ticketLoading.set(false);
      }
    });
  }

  resetTicketFilters(): void {
    this.ticketFilters = {
      category: 'All',
      priority: 'All',
      startDate: '',
      endDate: '',
      page: 1,
      pageSize: 20
    };
    this.loadTicketReport(1);
  }

  exportTicketsCsv(): void {
    this.exporting.set(true);
    this.reportService.exportTicketReportCsv(this.ticketFilters).subscribe({
      next: (blob) => {
        this.downloadBlob(blob, 'tickets-report.csv');
        this.exporting.set(false);
      },
      error: () => {
        this.ticketError.set('Failed to export ticket report CSV.');
        this.exporting.set(false);
      }
    });
  }

  getDistributionEntries(distribution?: Record<string, number>): { key: string; value: number }[] {
    if (!distribution) return [];
    return Object.entries(distribution).map(([key, value]) => ({ key, value }));
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    window.URL.revokeObjectURL(url);
  }
}
