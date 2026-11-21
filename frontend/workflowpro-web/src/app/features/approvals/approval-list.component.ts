import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApprovalService } from '../../core/approvals/approval.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  APPROVAL_REQUEST_STATUSES,
  ApprovalRequestStatus,
  ApprovalRequestSummary,
  SubmitApprovalRequest,
  WorkflowDefinitionDetail,
  WorkflowDefinitionSummary
} from '../../core/approvals/approval.models';

@Component({
  selector: 'app-approval-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="approvals-page">
      <header class="page-header">
        <div class="header-titles">
          <h1>Approval Workflows</h1>
          <p class="subtitle">Review pending approvals, track multi-level requests, and submit items for signoff</p>
        </div>

        <div class="header-actions">
          <button class="primary-btn" (click)="openSubmitModal()">
            + Submit Request
          </button>
        </div>
      </header>

      <!-- Tab Switcher -->
      <nav class="tabs-nav" role="tablist">
        <button
          type="button"
          class="tab-btn"
          [class.active]="activeTab() === 'inbox'"
          (click)="setTab('inbox')">
          ⚡ My Inbox (Awaiting My Action)
        </button>
        <button
          type="button"
          class="tab-btn"
          [class.active]="activeTab() === 'mine'"
          (click)="setTab('mine')">
          My Submitted Requests
        </button>
        @if (canViewAll()) {
          <button
            type="button"
            class="tab-btn"
            [class.active]="activeTab() === 'all'"
            (click)="setTab('all')">
            All Organizational Requests
          </button>
        }
      </nav>

      <!-- Filter Bar -->
      <section class="filters-card">
        <div class="filter-group">
          <label for="search-approvals">Search</label>
          <input
            id="search-approvals"
            type="text"
            placeholder="Search by title or #..."
            [(ngModel)]="searchQuery"
            (keyup.enter)="loadRequests()" />
        </div>

        <div class="filter-group">
          <label for="status-filter">Status</label>
          <select id="status-filter" [(ngModel)]="statusFilter" (change)="loadRequests()">
            <option value="All">All Statuses</option>
            @for (s of requestStatuses; track s) {
              <option [value]="s">{{ s }}</option>
            }
          </select>
        </div>

        <div class="filter-actions">
          <button class="filter-btn" (click)="loadRequests()">Filter</button>
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
          <p>Loading approval requests...</p>
        </div>
      } @else {
        <!-- Requests Table -->
        <div class="table-card">
          <table class="data-table">
            <thead>
              <tr>
                <th>Request #</th>
                <th>Title & Workflow</th>
                <th>Status</th>
                <th>Current Step</th>
                <th>Current Approver</th>
                <th>Requester</th>
                <th>Submitted</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (req of requests(); track req.id) {
                <tr>
                  <td><span class="req-num">#{{ req.requestNumber }}</span></td>
                  <td class="title-cell">
                    <a [routerLink]="['/approvals', req.id]" class="table-title-link">{{ req.title }}</a>
                    <span class="workflow-badge">{{ req.workflowName }}</span>
                  </td>
                  <td>
                    <span class="status-pill" [class]="'status-' + req.status.toLowerCase()">
                      {{ req.status }}
                    </span>
                  </td>
                  <td>
                    @if (req.currentStepName) {
                      <span class="step-indicator">Step {{ req.currentStepOrder }}: {{ req.currentStepName }}</span>
                    } @else {
                      <span class="terminal-text">—</span>
                    }
                  </td>
                  <td>
                    @if (req.currentApproverName) {
                      <span class="approver-name">{{ req.currentApproverName }}</span>
                    } @else {
                      <span class="terminal-text">—</span>
                    }
                  </td>
                  <td>{{ req.requesterName }}</td>
                  <td>{{ req.submittedAtUtc | date:'mediumDate' }}</td>
                  <td>
                    <a [routerLink]="['/approvals', req.id]" class="action-btn">
                      {{ activeTab() === 'inbox' ? 'Review' : 'View' }}
                    </a>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="8" class="empty-cell">
                    {{ activeTab() === 'inbox' ? 'No pending approvals require your action.' : 'No approval requests found.' }}
                  </td>
                </tr>
              }
            </tbody>
          </table>

          <!-- Pagination -->
          @if (totalPages() > 1) {
            <footer class="pagination-footer">
              <span class="page-info">
                Page {{ currentPage() }} of {{ totalPages() }} ({{ totalCount() }} requests)
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

      <!-- Submit Request Modal -->
      @if (showSubmitModal()) {
        <div class="modal-backdrop" (click)="closeSubmitModal()">
          <div class="modal-card modal-lg" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
            <header class="modal-header">
              <h2>Submit Approval Request</h2>
              <button class="modal-close" (click)="closeSubmitModal()">✕</button>
            </header>

            <form (ngSubmit)="onSubmitRequest()" class="modal-form">
              <div class="form-group">
                <label for="select-workflow">Select Workflow *</label>
                <select
                  id="select-workflow"
                  [(ngModel)]="submitModel.workflowDefinitionId"
                  (change)="onWorkflowChanged()"
                  name="workflowDefinitionId"
                  required>
                  <option value="" disabled>Choose an active workflow definition...</option>
                  @for (w of workflows(); track w.id) {
                    <option [value]="w.id">{{ w.name }} ({{ w.stepCount }} steps)</option>
                  }
                </select>
              </div>

              <!-- Workflow Steps Preview -->
              @if (selectedWorkflowDetail()) {
                <div class="workflow-preview-box">
                  <h4>Workflow Approval Hierarchy</h4>
                  <ol class="step-preview-list">
                    @for (step of selectedWorkflowDetail()?.steps; track step.id) {
                      <li class="step-preview-item">
                        <strong>Step {{ step.stepOrder }}: {{ step.name }}</strong>
                        <span class="step-approver-hint">
                          @if (step.approverDisplayName) {
                            Assigned to {{ step.approverDisplayName }}
                          } @else if (step.approverRole) {
                            Required Role: [{{ step.approverRole }}]
                          }
                        </span>
                      </li>
                    }
                  </ol>
                </div>
              }

              <div class="form-group">
                <label for="submit-title">Request Title *</label>
                <input
                  id="submit-title"
                  type="text"
                  [(ngModel)]="submitModel.title"
                  name="title"
                  maxlength="200"
                  required
                  placeholder="e.g., Annual Dev Conference Travel, MacBook Pro Upgrade" />
              </div>

              <div class="form-group">
                <label for="submit-desc">Description & Justification *</label>
                <textarea
                  id="submit-desc"
                  rows="4"
                  [(ngModel)]="submitModel.description"
                  name="description"
                  required
                  placeholder="Provide business justification, cost estimates, or relevant URLs..."></textarea>
              </div>

              @if (modalError()) {
                <div class="modal-error">{{ modalError() }}</div>
              }

              <footer class="modal-footer">
                <button type="button" class="btn-cancel" (click)="closeSubmitModal()">Cancel</button>
                <button type="submit" class="primary-btn" [disabled]="isSubmitting()">
                  {{ isSubmitting() ? 'Submitting...' : 'Submit for Approval' }}
                </button>
              </footer>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .approvals-page {
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
      min-width: 180px;
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
      padding: 0.45rem 0.85rem;
      border-radius: 6px;
      cursor: pointer;
    }
    .reset-btn {
      background: #e2e8f0;
      color: #334155;
      border: none;
      padding: 0.45rem 0.85rem;
      border-radius: 6px;
      cursor: pointer;
    }
    .alert {
      padding: 0.75rem 1rem;
      border-radius: 6px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.9rem;
    }
    .alert-error { background: #fee2e2; color: #991b1b; }
    .alert-success { background: #dcfce7; color: #166534; }
    .alert-close { background: none; border: none; cursor: pointer; color: inherit; }
    .table-card {
      background: white;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      overflow: hidden;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
    }
    .data-table th {
      background: #f8fafc;
      padding: 0.75rem 1rem;
      font-size: 0.8rem;
      font-weight: 600;
      color: #64748b;
      border-bottom: 1px solid #e2e8f0;
      text-transform: uppercase;
    }
    .data-table td {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      font-size: 0.9rem;
      vertical-align: middle;
    }
    .req-num {
      font-family: monospace;
      font-weight: 700;
      color: #2563eb;
    }
    .title-cell {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .table-title-link {
      font-weight: 600;
      color: #0f172a;
      text-decoration: none;
    }
    .table-title-link:hover { color: #2563eb; text-decoration: underline; }
    .workflow-badge {
      font-size: 0.7rem;
      background: #f1f5f9;
      color: #475569;
      padding: 2px 6px;
      border-radius: 4px;
      align-self: flex-start;
    }
    .status-pill {
      font-size: 0.75rem;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: 12px;
      display: inline-block;
    }
    .status-pending { background: #fef3c7; color: #92400e; }
    .status-approved { background: #dcfce7; color: #166534; }
    .status-rejected { background: #fee2e2; color: #991b1b; }
    .status-cancelled { background: #f1f5f9; color: #64748b; }
    .step-indicator {
      font-size: 0.85rem;
      font-weight: 500;
      color: #334155;
    }
    .approver-name {
      font-weight: 500;
      color: #1e293b;
    }
    .terminal-text { color: #94a3b8; }
    .action-btn {
      background: #f1f5f9;
      color: #1e293b;
      padding: 0.35rem 0.75rem;
      border-radius: 4px;
      text-decoration: none;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .action-btn:hover { background: #e2e8f0; }
    .empty-cell {
      text-align: center;
      padding: 2.5rem;
      color: #94a3b8;
    }
    .pagination-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem 1rem;
      background: #f8fafc;
      border-top: 1px solid #e2e8f0;
    }
    .page-info { font-size: 0.85rem; color: #64748b; }
    .page-buttons { display: flex; gap: 0.5rem; }
    .btn-page {
      padding: 0.35rem 0.75rem;
      border: 1px solid #cbd5e1;
      background: white;
      border-radius: 4px;
      font-size: 0.85rem;
      cursor: pointer;
    }
    .btn-page:disabled { opacity: 0.5; cursor: not-allowed; }
    .modal-backdrop {
      position: fixed;
      top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(15, 23, 42, 0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
      padding: 1rem;
    }
    .modal-card {
      background: white;
      border-radius: 8px;
      width: 100%;
      max-width: 550px;
      box-shadow: 0 20px 25px -5px rgba(0,0,0,0.1);
      overflow: hidden;
    }
    .modal-lg { max-width: 650px; }
    .modal-header {
      padding: 1.25rem 1.5rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid #e2e8f0;
    }
    .modal-header h2 { margin: 0; font-size: 1.25rem; font-weight: 700; }
    .modal-close { background: none; border: none; font-size: 1.2rem; cursor: pointer; color: #64748b; }
    .modal-form {
      padding: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
    }
    .form-group label {
      font-size: 0.85rem;
      font-weight: 600;
      color: #334155;
    }
    .form-group input, .form-group select, .form-group textarea {
      padding: 0.55rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
    }
    .workflow-preview-box {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      padding: 0.85rem 1rem;
    }
    .workflow-preview-box h4 {
      margin: 0 0 0.5rem;
      font-size: 0.85rem;
      color: #475569;
    }
    .step-preview-list {
      margin: 0;
      padding-left: 1.25rem;
      font-size: 0.85rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .step-preview-item {
      display: flex;
      justify-content: space-between;
    }
    .step-approver-hint { color: #64748b; font-size: 0.8rem; }
    .modal-error {
      background: #fee2e2;
      color: #991b1b;
      padding: 0.5rem 0.75rem;
      border-radius: 4px;
      font-size: 0.85rem;
    }
    .modal-footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 0.5rem;
    }
    .btn-cancel {
      background: #e2e8f0;
      border: none;
      padding: 0.55rem 1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .loading-state {
      text-align: center;
      padding: 3rem;
      color: #64748b;
    }
    .spinner {
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      width: 28px;
      height: 28px;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 0.75rem;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class ApprovalListComponent implements OnInit {
  private readonly approvalService = inject(ApprovalService);
  private readonly authService = inject(AuthService);

  readonly requestStatuses = APPROVAL_REQUEST_STATUSES;

  activeTab = signal<'inbox' | 'mine' | 'all'>('inbox');
  requests = signal<ApprovalRequestSummary[]>([]);
  workflows = signal<WorkflowDefinitionSummary[]>([]);
  selectedWorkflowDetail = signal<WorkflowDefinitionDetail | null>(null);

  isLoading = signal<boolean>(false);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  currentPage = signal<number>(1);
  totalPages = signal<number>(1);
  totalCount = signal<number>(0);

  searchQuery = '';
  statusFilter = 'All';

  // Modal
  showSubmitModal = signal<boolean>(false);
  isSubmitting = signal<boolean>(false);
  modalError = signal<string>('');
  submitModel: SubmitApprovalRequest = {
    workflowDefinitionId: '',
    title: '',
    description: '',
    stepApproverOverrides: []
  };

  canViewAll(): boolean {
    return this.authService.hasAnyRole(['Manager', 'Administrator']);
  }

  ngOnInit(): void {
    this.loadRequests();
    this.loadWorkflows();
  }

  setTab(tab: 'inbox' | 'mine' | 'all'): void {
    this.activeTab.set(tab);
    this.currentPage.set(1);
    this.loadRequests();
  }

  loadRequests(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    const inboxOnly = this.activeTab() === 'inbox';
    const myRequestsOnly = this.activeTab() === 'mine';

    this.approvalService.listRequests({
      status: this.statusFilter !== 'All' ? this.statusFilter : undefined,
      inboxOnly: inboxOnly ? true : undefined,
      myRequestsOnly: myRequestsOnly ? true : undefined,
      search: this.searchQuery.trim() || undefined,
      page: this.currentPage(),
      pageSize: 20
    }).subscribe({
      next: (res) => {
        this.requests.set(res.items);
        this.currentPage.set(res.page);
        this.totalPages.set(res.totalPages);
        this.totalCount.set(res.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Failed to load requests: ' + (err.error?.detail || err.message));
        this.isLoading.set(false);
      }
    });
  }

  loadWorkflows(): void {
    this.approvalService.listDefinitions(false).subscribe({
      next: (defs) => this.workflows.set(defs),
      error: () => this.workflows.set([])
    });
  }

  onWorkflowChanged(): void {
    if (!this.submitModel.workflowDefinitionId) {
      this.selectedWorkflowDetail.set(null);
      return;
    }

    this.approvalService.getDefinitionById(this.submitModel.workflowDefinitionId).subscribe({
      next: (detail) => this.selectedWorkflowDetail.set(detail),
      error: () => this.selectedWorkflowDetail.set(null)
    });
  }

  resetFilters(): void {
    this.searchQuery = '';
    this.statusFilter = 'All';
    this.currentPage.set(1);
    this.loadRequests();
  }

  changePage(newPage: number): void {
    this.currentPage.set(newPage);
    this.loadRequests();
  }

  openSubmitModal(): void {
    this.modalError.set('');
    this.submitModel = {
      workflowDefinitionId: this.workflows().length > 0 ? this.workflows()[0].id : '',
      title: '',
      description: '',
      stepApproverOverrides: []
    };
    this.showSubmitModal.set(true);
    if (this.submitModel.workflowDefinitionId) {
      this.onWorkflowChanged();
    }
  }

  closeSubmitModal(): void {
    this.showSubmitModal.set(false);
    this.modalError.set('');
    this.selectedWorkflowDetail.set(null);
  }

  onSubmitRequest(): void {
    if (!this.submitModel.workflowDefinitionId) {
      this.modalError.set('Please select a workflow definition.');
      return;
    }
    if (!this.submitModel.title.trim() || !this.submitModel.description.trim()) {
      this.modalError.set('Title and description are required.');
      return;
    }

    this.isSubmitting.set(true);
    this.modalError.set('');

    this.approvalService.submitRequest(this.submitModel).subscribe({
      next: (created) => {
        this.successMessage.set(`Approval Request #${created.requestNumber} submitted successfully.`);
        this.isSubmitting.set(false);
        this.closeSubmitModal();
        this.loadRequests();
      },
      error: (err) => {
        this.modalError.set(err.error?.detail || 'Failed to submit approval request.');
        this.isSubmitting.set(false);
      }
    });
  }
}
