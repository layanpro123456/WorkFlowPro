import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { DashboardService } from '../../core/dashboard/dashboard.service';
import { DashboardSummary } from '../../core/dashboard/dashboard.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="dashboard-page">
      <!-- Welcome Header -->
      <header class="welcome-banner">
        <div class="welcome-text">
          <h1>Welcome back, {{ authService.currentUser()?.displayName }}!</h1>
          <p class="user-meta">Signed in as <strong>{{ authService.currentUser()?.email }}</strong></p>
        </div>
        <div class="roles-container">
          @for (role of authService.userRoles(); track role) {
            <span class="role-tag">{{ role }}</span>
          }
        </div>
      </header>

      @if (loading()) {
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Loading your dashboard...</p>
        </div>
      } @else if (error()) {
        <div class="error-banner">
          <p>{{ error() }}</p>
          <button class="btn btn-secondary" (click)="loadSummary()">Retry</button>
        </div>
      } @else if (summary(); as data) {
        <!-- Metrics Grid -->
        <section class="kpi-grid">
          <div class="kpi-card" routerLink="/tasks">
            <div class="kpi-icon icon-tasks">📋</div>
            <div class="kpi-data">
              <span class="kpi-value">{{ data.assignedTasksCount }}</span>
              <span class="kpi-label">Assigned Tasks</span>
            </div>
          </div>

          <div class="kpi-card" [class.warning]="data.overdueTasksCount > 0" routerLink="/tasks">
            <div class="kpi-icon icon-overdue">⚠️</div>
            <div class="kpi-data">
              <span class="kpi-value">{{ data.overdueTasksCount }}</span>
              <span class="kpi-label">Overdue Tasks</span>
            </div>
          </div>

          <div class="kpi-card" routerLink="/tickets">
            <div class="kpi-icon icon-tickets">🎫</div>
            <div class="kpi-data">
              <span class="kpi-value">{{ data.openTicketsCount }}</span>
              <span class="kpi-label">Open Tickets</span>
            </div>
          </div>

          <div class="kpi-card" [class.highlight]="data.pendingApprovalsCount > 0" routerLink="/approvals">
            <div class="kpi-icon icon-approvals">✅</div>
            <div class="kpi-data">
              <span class="kpi-value">{{ data.pendingApprovalsCount }}</span>
              <span class="kpi-label">Pending Approvals</span>
            </div>
          </div>

          @if (data.managedProjectsCount !== undefined && data.managedProjectsCount !== null) {
            <div class="kpi-card" routerLink="/projects">
              <div class="kpi-icon icon-projects">📁</div>
              <div class="kpi-data">
                <span class="kpi-value">{{ data.managedProjectsCount }}</span>
                <span class="kpi-label">Managed Projects</span>
              </div>
            </div>
          }

          <div class="kpi-card" routerLink="/reports">
            <div class="kpi-icon icon-reports">📊</div>
            <div class="kpi-data">
              <span class="kpi-value">Reports</span>
              <span class="kpi-label">Analytics & CSV Export</span>
            </div>
          </div>
        </section>

        <!-- Main Dashboard Split Content -->
        <div class="dashboard-split">
          <!-- Pending Approvals Requiring Action -->
          @if (data.recentApprovals && data.recentApprovals.length > 0) {
            <div class="dashboard-panel approvals-panel">
              <div class="panel-header">
                <h2>⚡ Approvals Requiring Your Review</h2>
                <a routerLink="/approvals" class="panel-link">View all ({{ data.pendingApprovalsCount }})</a>
              </div>
              <div class="table-responsive">
                <table class="data-table">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Title</th>
                      <th>Workflow</th>
                      <th>Step</th>
                      <th>Requester</th>
                      <th>Submitted</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (item of data.recentApprovals; track item.id) {
                      <tr>
                        <td><strong>#{{ item.requestNumber }}</strong></td>
                        <td>{{ item.title }}</td>
                        <td><span class="meta-tag">{{ item.workflowName }}</span></td>
                        <td>{{ item.currentStepName || 'Review' }}</td>
                        <td>{{ item.requesterName }}</td>
                        <td>{{ item.submittedAtUtc | date:'shortDate' }}</td>
                        <td>
                          <a [routerLink]="['/approvals', item.id]" class="btn btn-sm btn-primary">Review</a>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <div class="panels-grid">
            <!-- My Active Tasks -->
            <div class="dashboard-panel">
              <div class="panel-header">
                <h2>📋 My Active Tasks</h2>
                <a routerLink="/tasks" class="panel-link">View board</a>
              </div>
              @if (data.recentTasks && data.recentTasks.length > 0) {
                <div class="table-responsive">
                  <table class="data-table">
                    <thead>
                      <tr>
                        <th>Project</th>
                        <th>Task</th>
                        <th>Status</th>
                        <th>Priority</th>
                        <th>Due Date</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (task of data.recentTasks; track task.id) {
                        <tr>
                          <td><span class="project-pill">{{ task.projectCode }}</span></td>
                          <td>
                            <a [routerLink]="['/tasks', task.id]" class="table-title-link">{{ task.title }}</a>
                          </td>
                          <td><span class="status-badge" [attr.data-status]="task.status">{{ task.status }}</span></td>
                          <td><span class="priority-badge" [attr.data-priority]="task.priority">{{ task.priority }}</span></td>
                          <td>
                            @if (task.dueAtUtc) {
                              {{ task.dueAtUtc | date:'shortDate' }}
                            } @else {
                              <span class="text-muted">—</span>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              } @else {
                <div class="empty-notice">No active tasks assigned to you right now.</div>
              }
            </div>

            <!-- Recent Tickets -->
            <div class="dashboard-panel">
              <div class="panel-header">
                <h2>🎫 Service Desk Activity</h2>
                <a routerLink="/tickets" class="panel-link">All tickets</a>
              </div>
              @if (data.recentTickets && data.recentTickets.length > 0) {
                <div class="table-responsive">
                  <table class="data-table">
                    <thead>
                      <tr>
                        <th>#</th>
                        <th>Subject</th>
                        <th>Category</th>
                        <th>Status</th>
                        <th>Updated</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (ticket of data.recentTickets; track ticket.id) {
                        <tr>
                          <td><strong>#{{ ticket.ticketNumber }}</strong></td>
                          <td>
                            <a [routerLink]="['/tickets', ticket.id]" class="table-title-link">{{ ticket.subject }}</a>
                          </td>
                          <td><span class="meta-tag">{{ ticket.category }}</span></td>
                          <td><span class="status-badge" [attr.data-status]="ticket.status">{{ ticket.status }}</span></td>
                          <td>{{ ticket.createdAtUtc | date:'shortDate' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              } @else {
                <div class="empty-notice">No open service desk tickets found.</div>
              }
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .dashboard-page {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .welcome-banner {
      background: #ffffff;
      padding: 1.5rem 2rem;
      border-radius: 10px;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
      display: flex;
      justify-content: space-between;
      align-items: center;
      border: 1px solid #e2e8f0;
    }
    h1 {
      margin: 0;
      font-size: 1.5rem;
      color: #0f172a;
    }
    .user-meta {
      margin: 0.25rem 0 0;
      color: #64748b;
      font-size: 0.85rem;
    }
    .roles-container {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .role-tag {
      background: #e0f2fe;
      color: #0369a1;
      padding: 4px 10px;
      border-radius: 12px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 4rem;
      color: #64748b;
      gap: 1rem;
    }
    .spinner {
      width: 32px;
      height: 32px;
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
      padding: 1rem 1.5rem;
      border-radius: 8px;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 1.25rem;
    }
    .kpi-card {
      background: #ffffff;
      padding: 1.25rem;
      border-radius: 10px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
      display: flex;
      align-items: center;
      gap: 1rem;
      cursor: pointer;
      transition: all 0.15s ease-in-out;
      text-decoration: none;
      color: inherit;
    }
    .kpi-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.08);
      border-color: #cbd5e1;
    }
    .kpi-card.warning {
      border-color: #fed7aa;
      background: #fffbeb;
    }
    .kpi-card.highlight {
      border-color: #bfdbfe;
      background: #eff6ff;
    }
    .kpi-icon {
      font-size: 1.75rem;
      width: 48px;
      height: 48px;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: #f1f5f9;
    }
    .kpi-card.warning .kpi-icon { background: #fef3c7; }
    .kpi-card.highlight .kpi-icon { background: #dbeafe; }
    .kpi-data {
      display: flex;
      flex-direction: column;
    }
    .kpi-value {
      font-size: 1.4rem;
      font-weight: 700;
      color: #0f172a;
    }
    .kpi-label {
      font-size: 0.75rem;
      color: #64748b;
      font-weight: 500;
      text-transform: uppercase;
      letter-spacing: 0.025em;
    }
    .dashboard-split {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .dashboard-panel {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      padding: 1.25rem 1.5rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
    }
    .approvals-panel {
      border-left: 4px solid #2563eb;
    }
    .panels-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(450px, 1fr));
      gap: 1.5rem;
    }
    .panel-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
      padding-bottom: 0.75rem;
      border-bottom: 1px solid #f1f5f9;
    }
    .panel-header h2 {
      margin: 0;
      font-size: 1.05rem;
      color: #0f172a;
    }
    .panel-link {
      color: #2563eb;
      font-size: 0.8rem;
      font-weight: 600;
      text-decoration: none;
    }
    .panel-link:hover { text-decoration: underline; }
    .table-responsive {
      overflow-x: auto;
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.85rem;
    }
    .data-table th {
      text-align: left;
      padding: 0.6rem 0.75rem;
      color: #64748b;
      font-weight: 600;
      font-size: 0.75rem;
      border-bottom: 1px solid #e2e8f0;
      text-transform: uppercase;
    }
    .data-table td {
      padding: 0.65rem 0.75rem;
      border-bottom: 1px solid #f1f5f9;
      color: #334155;
    }
    .table-title-link {
      color: #0f172a;
      text-decoration: none;
      font-weight: 500;
    }
    .table-title-link:hover {
      color: #2563eb;
      text-decoration: underline;
    }
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
    .btn {
      padding: 0.4rem 0.8rem;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      text-decoration: none;
      cursor: pointer;
      border: none;
      display: inline-block;
    }
    .btn-primary {
      background: #2563eb;
      color: white;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .btn-sm {
      padding: 0.25rem 0.6rem;
      font-size: 0.75rem;
    }
    .btn-secondary {
      background: #f1f5f9;
      color: #334155;
      border: 1px solid #cbd5e1;
    }
    .text-muted { color: #94a3b8; }
    .empty-notice {
      padding: 2rem;
      text-align: center;
      color: #94a3b8;
      font-size: 0.85rem;
    }
  `]
})
export class DashboardComponent implements OnInit {
  readonly authService = inject(AuthService);
  private readonly dashboardService = inject(DashboardService);

  summary = signal<DashboardSummary | null>(null);
  loading = signal<boolean>(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadSummary();
  }

  loadSummary(): void {
    this.loading.set(true);
    this.error.set(null);
    this.dashboardService.getSummary().subscribe({
      next: (data) => {
        this.summary.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load dashboard data. Please try again.');
        this.loading.set(false);
      }
    });
  }
}
