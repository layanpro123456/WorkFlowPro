import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApprovalService } from '../../core/approvals/approval.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  ApprovalDecisionType,
  ApprovalRequestDetail
} from '../../core/approvals/approval.models';

@Component({
  selector: 'app-approval-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="approval-detail-container">
      @if (isLoading()) {
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Loading approval details...</p>
        </div>
      } @else if (errorMessage() && !request()) {
        <div class="alert alert-error">
          <span>{{ errorMessage() }}</span>
          <a routerLink="/approvals" class="btn-link">Back to Approvals</a>
        </div>
      } @else if (request(); as item) {
        <!-- Breadcrumbs -->
        <nav class="breadcrumb-nav">
          <a routerLink="/approvals">Approval Workflows</a>
          <span class="sep">/</span>
          <span class="breadcrumb-current">Request #{{ item.requestNumber }}</span>
        </nav>

        <!-- Alerts -->
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

        <!-- Header -->
        <header class="detail-header">
          <div class="header-main">
            <div class="badges-row">
              <span class="req-badge">#{{ item.requestNumber }}</span>
              <span class="workflow-pill">{{ item.workflowName }}</span>
              <span class="status-pill" [class]="'status-' + item.status.toLowerCase()">
                {{ item.status }}
              </span>
              <span class="version-tag">v{{ item.version }}</span>
            </div>
            <h1 class="request-heading">{{ item.title }}</h1>
            <p class="requester-line">
              Submitted by <strong>{{ item.requesterName }}</strong> ({{ item.requesterEmail }}) on {{ item.submittedAtUtc | date:'medium' }}
            </p>
          </div>

          <div class="header-meta">
            @if (item.completedAtUtc) {
              <div class="completed-badge">
                Finalized on {{ item.completedAtUtc | date:'medium' }}
              </div>
            }
          </div>
        </header>

        <!-- Main Layout: 2 Columns -->
        <div class="detail-layout">
          <!-- Left Column: Stepper & Decisions -->
          <div class="main-column">
            <!-- Sequential Visual Stepper -->
            <section class="card stepper-card">
              <h2>Approval Hierarchy & Progress</h2>
              <div class="stepper">
                @for (step of item.steps; track step.id; let idx = $index) {
                  <div
                    class="step-node"
                    [class.step-active]="item.currentStepOrder === step.stepOrder"
                    [class.step-approved]="step.status === 'Approved'"
                    [class.step-rejected]="step.status === 'Rejected'"
                    [class.step-skipped]="step.status === 'Skipped'">
                    <div class="step-indicator-circle">
                      @if (step.status === 'Approved') {
                        <span>✓</span>
                      } @else if (step.status === 'Rejected') {
                        <span>✕</span>
                      } @else if (step.status === 'Skipped') {
                        <span>—</span>
                      } @else {
                        <span>{{ step.stepOrder }}</span>
                      }
                    </div>

                    <div class="step-details">
                      <div class="step-header-row">
                        <h3 class="step-title">Step {{ step.stepOrder }}: {{ step.name }}</h3>
                        <span class="step-status-tag" [class]="'step-tag-' + step.status.toLowerCase()">
                          {{ step.status }}
                        </span>
                      </div>

                      <div class="step-approver-info">
                        Designated Approver: <strong>{{ step.approverName }}</strong> ({{ step.approverEmail }})
                      </div>

                      @if (step.decision; as dec) {
                        <div class="decision-box" [class]="'dec-' + dec.decision.toLowerCase()">
                          <div class="dec-header">
                            <strong>{{ dec.decision }}</strong> by {{ dec.approverName }} on {{ dec.decidedAtUtc | date:'medium' }}
                          </div>
                          @if (dec.comment) {
                            <p class="dec-comment">"{{ dec.comment }}"</p>
                          }
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            </section>

            <!-- Request Description -->
            <section class="card">
              <h2>Business Justification & Details</h2>
              <div class="description-content">
                {{ item.description }}
              </div>
            </section>
          </div>

          <!-- Right Column: Decision Actions -->
          <div class="side-column">
            <!-- Action Card for Current Approver -->
            @if (canDecide()) {
              <div class="action-card decision-card">
                <h3>⚡ Action Required</h3>
                <p class="action-desc">
                  You are the designated approver for <strong>Step {{ item.currentStepOrder }}: {{ item.currentStepName }}</strong>.
                </p>

                <div class="form-group">
                  <label for="decision-comment">Decision Comment (optional for approval, recommended for rejection)</label>
                  <textarea
                    id="decision-comment"
                    rows="3"
                    [(ngModel)]="decisionComment"
                    placeholder="Enter review notes, budget codes, or reasons..."></textarea>
                </div>

                <div class="decision-buttons">
                  <button
                    class="btn-approve"
                    [disabled]="isDeciding()"
                    (click)="submitDecision('Approved')">
                    ✓ Approve Step
                  </button>
                  <button
                    class="btn-reject"
                    [disabled]="isDeciding()"
                    (click)="submitDecision('Rejected')">
                    ✕ Reject Request
                  </button>
                </div>
              </div>
            }

            <!-- Cancellation Card -->
            @if (canCancel()) {
              <div class="action-card cancel-card">
                <h3>Cancel Request</h3>
                <p class="action-desc">
                  As the requester, you may cancel this pending request before final approval.
                </p>
                <button
                  class="btn-cancel-request"
                  [disabled]="isCancelling()"
                  (click)="onCancelRequest()">
                  Cancel This Request
                </button>
              </div>
            }

            <!-- Metadata Summary -->
            <div class="card meta-card">
              <h3>Request Properties</h3>
              <dl class="meta-list">
                <dt>Status</dt>
                <dd>
                  <span class="status-pill" [class]="'status-' + item.status.toLowerCase()">
                    {{ item.status }}
                  </span>
                </dd>
                <dt>Workflow</dt>
                <dd>{{ item.workflowName }}</dd>
                <dt>Submitted Date</dt>
                <dd>{{ item.submittedAtUtc | date:'medium' }}</dd>
                <dt>Current Step</dt>
                <dd>{{ item.currentStepOrder ? 'Step ' + item.currentStepOrder + ' (' + item.currentStepName + ')' : 'None (Terminal)' }}</dd>
                <dt>Version Token</dt>
                <dd><span class="version-token">{{ item.version }}</span></dd>
              </dl>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .approval-detail-container {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .breadcrumb-nav {
      font-size: 0.85rem;
      color: #64748b;
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
    .breadcrumb-nav a { color: #2563eb; text-decoration: none; }
    .breadcrumb-current { color: #0f172a; font-weight: 600; }
    .detail-header {
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1.5rem;
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      flex-wrap: wrap;
      gap: 1rem;
    }
    .badges-row {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      margin-bottom: 0.5rem;
    }
    .req-badge {
      font-family: monospace;
      font-weight: 700;
      color: #2563eb;
      font-size: 1.1rem;
    }
    .workflow-pill {
      background: #eff6ff;
      color: #1d4ed8;
      border: 1px solid #bfdbfe;
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .version-tag {
      font-size: 0.7rem;
      color: #64748b;
      background: #f1f5f9;
      padding: 2px 6px;
      border-radius: 4px;
    }
    .request-heading {
      margin: 0;
      font-size: 1.5rem;
      color: #0f172a;
    }
    .requester-line {
      margin: 0.35rem 0 0;
      color: #64748b;
      font-size: 0.85rem;
    }
    .completed-badge {
      background: #f1f5f9;
      color: #475569;
      padding: 0.4rem 0.8rem;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 500;
    }
    .detail-layout {
      display: grid;
      grid-template-columns: 1fr 340px;
      gap: 1.5rem;
      align-items: flex-start;
    }
    @media (max-width: 900px) {
      .detail-layout { grid-template-columns: 1fr; }
    }
    .main-column, .side-column {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .card {
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1.5rem;
    }
    .card h2 {
      margin: 0 0 1.25rem;
      font-size: 1.15rem;
      color: #0f172a;
      border-bottom: 1px solid #f1f5f9;
      padding-bottom: 0.75rem;
    }
    .stepper {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
      position: relative;
    }
    .step-node {
      display: flex;
      gap: 1rem;
      position: relative;
    }
    .step-indicator-circle {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: #e2e8f0;
      color: #475569;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 0.9rem;
      flex-shrink: 0;
      z-index: 2;
    }
    .step-active .step-indicator-circle {
      background: #2563eb;
      color: white;
      box-shadow: 0 0 0 4px #dbeafe;
    }
    .step-approved .step-indicator-circle {
      background: #16a34a;
      color: white;
    }
    .step-rejected .step-indicator-circle {
      background: #dc2626;
      color: white;
    }
    .step-skipped .step-indicator-circle {
      background: #f1f5f9;
      color: #94a3b8;
    }
    .step-details {
      flex: 1;
      background: #f8fafc;
      padding: 1rem 1.25rem;
      border-radius: 6px;
      border: 1px solid #e2e8f0;
    }
    .step-header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.35rem;
    }
    .step-title {
      margin: 0;
      font-size: 0.95rem;
      color: #0f172a;
    }
    .step-status-tag {
      font-size: 0.7rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
      text-transform: uppercase;
    }
    .step-tag-pending { background: #fef3c7; color: #92400e; }
    .step-tag-approved { background: #dcfce7; color: #166534; }
    .step-tag-rejected { background: #fee2e2; color: #991b1b; }
    .step-tag-skipped { background: #f1f5f9; color: #94a3b8; }
    .step-approver-info {
      font-size: 0.85rem;
      color: #475569;
    }
    .decision-box {
      margin-top: 0.75rem;
      padding: 0.65rem 0.85rem;
      border-radius: 4px;
      font-size: 0.85rem;
    }
    .dec-approved { background: #ecfdf5; border: 1px solid #a7f3d0; color: #065f46; }
    .dec-rejected { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
    .dec-comment { margin: 0.35rem 0 0; font-style: italic; }
    .description-content {
      font-size: 0.95rem;
      line-height: 1.6;
      color: #334155;
      white-space: pre-wrap;
    }
    .action-card {
      border-radius: 8px;
      padding: 1.25rem;
    }
    .decision-card {
      background: #eff6ff;
      border: 2px solid #2563eb;
    }
    .decision-card h3 { margin: 0 0 0.4rem; color: #1e3a8a; }
    .action-desc { margin: 0 0 0.85rem; font-size: 0.85rem; color: #3b82f6; }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      margin-bottom: 1rem;
    }
    .form-group label { font-size: 0.8rem; font-weight: 600; color: #1e293b; }
    .form-group textarea {
      padding: 0.5rem;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      font-size: 0.85rem;
    }
    .decision-buttons {
      display: flex;
      gap: 0.5rem;
    }
    .btn-approve {
      flex: 1;
      background: #16a34a;
      color: white;
      border: none;
      padding: 0.6rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-approve:hover:not(:disabled) { background: #15803d; }
    .btn-reject {
      flex: 1;
      background: #dc2626;
      color: white;
      border: none;
      padding: 0.6rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-reject:hover:not(:disabled) { background: #b91c1c; }
    .cancel-card {
      background: #fff;
      border: 1px dashed #cbd5e1;
    }
    .cancel-card h3 { margin: 0 0 0.4rem; font-size: 0.95rem; color: #64748b; }
    .btn-cancel-request {
      width: 100%;
      background: #fee2e2;
      color: #991b1b;
      border: 1px solid #fecaca;
      padding: 0.45rem;
      border-radius: 6px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-cancel-request:hover:not(:disabled) { background: #fca5a5; }
    .meta-card h3 { margin: 0 0 0.75rem; font-size: 0.95rem; color: #0f172a; }
    .meta-list {
      margin: 0;
      display: grid;
      grid-template-columns: 100px 1fr;
      gap: 0.5rem 0.75rem;
      font-size: 0.85rem;
    }
    .meta-list dt { color: #64748b; font-weight: 500; }
    .meta-list dd { margin: 0; color: #0f172a; font-weight: 600; }
    .status-pill {
      font-size: 0.75rem;
      font-weight: 600;
      padding: 2px 8px;
      border-radius: 12px;
      display: inline-block;
    }
    .status-pending { background: #fef3c7; color: #92400e; }
    .status-approved { background: #dcfce7; color: #166534; }
    .status-rejected { background: #fee2e2; color: #991b1b; }
    .status-cancelled { background: #f1f5f9; color: #64748b; }
    .version-token { font-family: monospace; }
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
    .btn-link { color: #2563eb; margin-left: 1rem; text-decoration: underline; }
    .loading-state { text-align: center; padding: 3rem; color: #64748b; }
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
export class ApprovalDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly approvalService = inject(ApprovalService);
  private readonly authService = inject(AuthService);

  request = signal<ApprovalRequestDetail | null>(null);
  isLoading = signal<boolean>(false);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  decisionComment = '';
  isDeciding = signal<boolean>(false);
  isCancelling = signal<boolean>(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadRequest(id);
    }
  }

  loadRequest(id: string, clearError = true): void {
    this.isLoading.set(true);
    if (clearError) {
      this.errorMessage.set('');
    }

    this.approvalService.getRequestById(id).subscribe({
      next: (data) => {
        this.request.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Approval request not found or access denied.');
        this.isLoading.set(false);
      }
    });
  }

  canDecide(): boolean {
    const item = this.request();
    if (!item || item.status !== 'Pending' || !item.currentApproverId) {
      return false;
    }
    const currentUserId = this.authService.currentUser()?.id;
    return currentUserId === item.currentApproverId;
  }

  canCancel(): boolean {
    const item = this.request();
    if (!item || item.status !== 'Pending') {
      return false;
    }
    const currentUserId = this.authService.currentUser()?.id;
    const isAdmin = this.authService.hasRole('Administrator');
    return currentUserId === item.requesterId || isAdmin;
  }

  submitDecision(decision: ApprovalDecisionType): void {
    const item = this.request();
    if (!item) return;

    this.isDeciding.set(true);
    this.errorMessage.set('');

    this.approvalService.recordDecision(item.id, {
      decision,
      comment: this.decisionComment.trim() || undefined,
      expectedVersion: item.version
    }).subscribe({
      next: (updated) => {
        this.request.set(updated);
        this.decisionComment = '';
        this.isDeciding.set(false);
        this.successMessage.set(`Successfully recorded '${decision}' decision.`);
      },
      error: (err) => {
        if (err.status === 409) {
          this.errorMessage.set(
            'Concurrency Conflict: The approval request was updated by another user or step. Please review the refreshed state.'
          );
          this.loadRequest(item.id, false);
        } else {
          this.errorMessage.set(err.error?.detail || 'Failed to record decision.');
        }
        this.isDeciding.set(false);
      }
    });
  }

  onCancelRequest(): void {
    const item = this.request();
    if (!item) return;

    if (!confirm('Are you sure you want to cancel this approval request?')) {
      return;
    }

    this.isCancelling.set(true);
    this.errorMessage.set('');

    this.approvalService.cancelRequest(item.id, {
      expectedVersion: item.version
    }).subscribe({
      next: (updated) => {
        this.request.set(updated);
        this.isCancelling.set(false);
        this.successMessage.set('Approval request cancelled.');
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Failed to cancel approval request.');
        this.isCancelling.set(false);
      }
    });
  }
}
