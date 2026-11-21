import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TicketService } from '../../core/tickets/ticket.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  ALLOWED_TICKET_TRANSITIONS,
  SupportAgent,
  TicketDetail,
  TicketStatus
} from '../../core/tickets/ticket.models';

@Component({
  selector: 'app-ticket-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="ticket-detail-container">
      @if (isLoading()) {
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Loading ticket details...</p>
        </div>
      } @else if (errorMessage() && !ticket()) {
        <div class="alert alert-error">
          <span>{{ errorMessage() }}</span>
          <a routerLink="/tickets" class="btn-link">Back to Tickets</a>
        </div>
      } @else if (ticket(); as item) {
        <!-- Breadcrumbs -->
        <nav class="breadcrumb-nav">
          <a routerLink="/tickets">Service Desk</a>
          <span class="sep">/</span>
          <span class="breadcrumb-current">Ticket #{{ item.ticketNumber }}</span>
        </nav>

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

        <!-- Header -->
        <header class="detail-header">
          <div class="header-main">
            <div class="badges-row">
              <span class="ticket-badge">#{{ item.ticketNumber }}</span>
              <span class="category-badge">{{ item.category }}</span>
              <span class="priority-tag" [class]="'tag-' + item.priority.toLowerCase()">
                {{ item.priority }}
              </span>
              <span class="status-pill" [class]="'status-' + item.status.toLowerCase()">
                {{ item.status }}
              </span>
            </div>
            <h1 class="ticket-heading">{{ item.subject }}</h1>
            <p class="requester-line">
              Submitted by <strong>{{ item.requesterName }}</strong> ({{ item.requesterEmail }}) on {{ item.createdAtUtc | date:'medium' }}
            </p>
          </div>
        </header>

        <!-- Action Bar: Status Transitions & Assignment -->
        <section class="action-bar" aria-label="Ticket actions">
          <!-- Status Transitions -->
          <div class="transition-section">
            <span class="bar-label">Available Transitions:</span>
            <div class="action-buttons">
              @for (target of getAvailableTransitions(item.status); track target) {
                <button
                  type="button"
                  class="transition-btn"
                  [class]="'btn-trans-' + target.toLowerCase()"
                  [disabled]="isUpdatingStatus()"
                  (click)="promptStatusTransition(target)">
                  Move to {{ target }}
                </button>
              } @empty {
                <span class="terminal-msg">No transitions available.</span>
              }
            </div>
          </div>

          <!-- Assignment (Support Agents Only) -->
          @if (isAgentOrAdmin()) {
            <div class="assign-section">
              <label for="assign-select" class="bar-label">Assignee:</label>
              <select
                id="assign-select"
                class="assign-select"
                [ngModel]="item.assigneeId"
                (ngModelChange)="onAssigneeChanged($event)">
                <option [ngValue]="null">Unassigned</option>
                @for (agent of agents(); track agent.userId) {
                  <option [value]="agent.userId">{{ agent.displayName }} ({{ agent.primaryRole }})</option>
                }
              </select>
            </div>
          }
        </section>

        <!-- Two Column Content Grid -->
        <div class="content-grid">
          <!-- Left Column: Description & Messages Thread -->
          <div class="main-column">
            <!-- Issue Description -->
            <section class="content-card description-card">
              <h2>Issue Description</h2>
              <div class="description-body">{{ item.description }}</div>
            </section>

            <!-- Conversation / Messages Thread -->
            <section class="content-card conversation-card">
              <h2>Conversation ({{ item.messages.length }})</h2>

              <!-- Post Message Box -->
              <form (ngSubmit)="onSendMessage()" class="message-form">
                <textarea
                  rows="3"
                  [(ngModel)]="messageBody"
                  name="messageBody"
                  placeholder="Type a response or question..."
                  required
                  [disabled]="isSendingMessage()"></textarea>

                <div class="message-form-footer">
                  @if (isAgentOrAdmin()) {
                    <label class="internal-note-toggle">
                      <input
                        type="checkbox"
                        [(ngModel)]="isInternalNote"
                        name="isInternalNote" />
                      <span>🔒 Post as internal note (hidden from customer)</span>
                    </label>
                  } @else {
                    <div></div>
                  }

                  <button
                    type="submit"
                    class="primary-btn"
                    [class.btn-internal]="isInternalNote"
                    [disabled]="!messageBody.trim() || isSendingMessage()">
                    {{ isSendingMessage() ? 'Sending...' : (isInternalNote ? 'Post Internal Note' : 'Send Reply') }}
                  </button>
                </div>
              </form>

              <!-- Messages List -->
              <div class="messages-list">
                @for (msg of item.messages; track msg.id) {
                  <article class="message-card" [class.internal-note]="msg.isInternalNote">
                    @if (msg.isInternalNote) {
                      <div class="internal-banner">
                        🔒 Confidential Internal Note (Visible to Support Staff Only)
                      </div>
                    }
                    <header class="message-header">
                      <span class="message-author">{{ msg.authorName }}</span>
                      <time class="message-time">{{ msg.createdAtUtc | date:'medium' }}</time>
                    </header>
                    <div class="message-body">{{ msg.body }}</div>
                  </article>
                } @empty {
                  <p class="empty-text">No replies yet.</p>
                }
              </div>
            </section>
          </div>

          <!-- Right Column: Sidebar & Audit History -->
          <aside class="sidebar-column">
            <!-- Properties Card -->
            <section class="content-card meta-card">
              <h3>Ticket Details</h3>
              <dl class="meta-list">
                <div class="meta-item">
                  <dt>Ticket #</dt>
                  <dd class="mono-val">#{{ item.ticketNumber }}</dd>
                </div>
                <div class="meta-item">
                  <dt>Requester</dt>
                  <dd>{{ item.requesterName }}</dd>
                </div>
                <div class="meta-item">
                  <dt>Assignee</dt>
                  <dd>{{ item.assigneeName || 'Unassigned' }}</dd>
                </div>
                <div class="meta-item">
                  <dt>Category</dt>
                  <dd>{{ item.category }}</dd>
                </div>
                <div class="meta-item">
                  <dt>Priority</dt>
                  <dd>{{ item.priority }}</dd>
                </div>
                @if (item.relatedProjectCode) {
                  <div class="meta-item">
                    <dt>Project</dt>
                    <dd>[{{ item.relatedProjectCode }}] {{ item.relatedProjectName }}</dd>
                  </div>
                }
                <div class="meta-item">
                  <dt>Created</dt>
                  <dd>{{ item.createdAtUtc | date:'short' }}</dd>
                </div>
                @if (item.resolvedAtUtc) {
                  <div class="meta-item">
                    <dt>Resolved</dt>
                    <dd>{{ item.resolvedAtUtc | date:'short' }}</dd>
                  </div>
                }
                @if (item.closedAtUtc) {
                  <div class="meta-item">
                    <dt>Closed</dt>
                    <dd>{{ item.closedAtUtc | date:'short' }}</dd>
                  </div>
                }
              </dl>
            </section>

            <!-- Status Audit Timeline -->
            <section class="content-card history-card">
              <h3>Lifecycle Audit History</h3>
              <ol class="history-timeline">
                @for (h of item.history; track h.id) {
                  <li class="history-entry">
                    <div class="timeline-dot"></div>
                    <div class="history-details">
                      <div class="history-change">
                        @if (h.fromStatus) {
                          <span class="status-chip">{{ h.fromStatus }}</span>
                          <span class="arrow">➔</span>
                        }
                        <span class="status-chip active-chip">{{ h.toStatus }}</span>
                      </div>
                      <div class="history-actor">
                        by {{ h.changedByName }} on {{ h.changedAtUtc | date:'short' }}
                      </div>
                      @if (h.comment) {
                        <div class="history-comment">"{{ h.comment }}"</div>
                      }
                    </div>
                  </li>
                }
              </ol>
            </section>
          </aside>
        </div>

        <!-- Status Transition Modal -->
        @if (showTransitionModal()) {
          <div class="modal-backdrop" (click)="closeTransitionModal()">
            <div class="modal-card" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
              <header class="modal-header">
                <h2>Transition to {{ pendingStatus() }}</h2>
                <button class="modal-close" (click)="closeTransitionModal()">✕</button>
              </header>

              <form (ngSubmit)="executeStatusTransition()" class="modal-form">
                <p class="modal-info">
                  Move ticket from <strong>{{ item.status }}</strong> to <strong>{{ pendingStatus() }}</strong>.
                </p>

                <div class="form-group">
                  <label for="status-comment">Comment or Resolution Notes</label>
                  <textarea
                    id="status-comment"
                    rows="3"
                    [(ngModel)]="statusComment"
                    name="statusComment"
                    placeholder="Details about this status update"></textarea>
                </div>

                <footer class="modal-footer">
                  <button type="button" class="btn-cancel" (click)="closeTransitionModal()">Cancel</button>
                  <button type="submit" class="primary-btn" [disabled]="isUpdatingStatus()">
                    {{ isUpdatingStatus() ? 'Updating...' : 'Confirm Transition' }}
                  </button>
                </footer>
              </form>
            </div>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .ticket-detail-container {
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
    .breadcrumb-nav a:hover { text-decoration: underline; }
    .sep { color: #94a3b8; }
    .breadcrumb-current { font-weight: 600; color: #1e293b; }
    .detail-header {
      background: #ffffff;
      padding: 1.25rem 1.5rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    .badges-row {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      margin-bottom: 0.5rem;
    }
    .ticket-badge {
      font-family: monospace;
      font-weight: 700;
      color: #2563eb;
      background: #eff6ff;
      border: 1px solid #bfdbfe;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 0.85rem;
    }
    .category-badge {
      font-size: 0.75rem;
      background: #f1f5f9;
      color: #334155;
      padding: 2px 7px;
      border-radius: 4px;
      font-weight: 600;
    }
    .ticket-heading {
      margin: 0.25rem 0 0.5rem;
      font-size: 1.6rem;
      font-weight: 700;
      color: #0f172a;
    }
    .requester-line {
      margin: 0;
      font-size: 0.85rem;
      color: #64748b;
    }
    .action-bar {
      background: #f8fafc;
      padding: 0.85rem 1.25rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 1rem;
    }
    .transition-section, .assign-section {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-wrap: wrap;
    }
    .bar-label { font-size: 0.85rem; font-weight: 600; color: #475569; }
    .action-buttons { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .transition-btn {
      padding: 0.4rem 0.85rem;
      border-radius: 6px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.15s;
    }
    .btn-trans-assigned { background: #e0e7ff; color: #3730a3; border-color: #c7d2fe; }
    .btn-trans-inprogress { background: #fef3c7; color: #92400e; border-color: #fde68a; }
    .btn-trans-waitingforrequester { background: #ffedd5; color: #c2410c; border-color: #fed7aa; }
    .btn-trans-resolved { background: #dcfce7; color: #166534; border-color: #bbf7d0; }
    .btn-trans-closed { background: #f1f5f9; color: #475569; border-color: #cbd5e1; }
    .assign-select {
      padding: 0.4rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.85rem;
      background: #ffffff;
    }
    .content-grid {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: 1.5rem;
    }
    @media (max-width: 900px) {
      .content-grid { grid-template-columns: 1fr; }
    }
    .main-column, .sidebar-column {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .content-card {
      background: #ffffff;
      padding: 1.25rem 1.5rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    .content-card h2, .content-card h3 {
      margin: 0 0 1rem;
      font-size: 1.15rem;
      font-weight: 700;
      color: #0f172a;
    }
    .description-body {
      font-size: 0.95rem;
      line-height: 1.6;
      color: #334155;
      white-space: pre-wrap;
    }
    .message-form {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      margin-bottom: 1.5rem;
    }
    .message-form textarea {
      padding: 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-family: inherit;
      font-size: 0.9rem;
    }
    .message-form-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .internal-note-toggle {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.85rem;
      font-weight: 600;
      color: #92400e;
      cursor: pointer;
    }
    .btn-internal {
      background: #b45309 !important;
    }
    .messages-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .message-card {
      padding: 0.85rem;
      background: #f8fafc;
      border-radius: 6px;
      border: 1px solid #e2e8f0;
    }
    .message-card.internal-note {
      background: #fffbeb;
      border-color: #fde68a;
    }
    .internal-banner {
      font-size: 0.75rem;
      font-weight: 700;
      color: #92400e;
      background: #fef3c7;
      padding: 3px 6px;
      border-radius: 4px;
      margin-bottom: 0.5rem;
      display: inline-block;
    }
    .message-header {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.35rem;
      font-size: 0.8rem;
    }
    .message-author { font-weight: 600; color: #1e293b; }
    .message-time { color: #94a3b8; }
    .message-body { font-size: 0.9rem; color: #334155; white-space: pre-wrap; }
    .meta-list { margin: 0; display: flex; flex-direction: column; gap: 0.75rem; }
    .meta-item { display: flex; justify-content: space-between; font-size: 0.85rem; }
    .meta-item dt { color: #64748b; }
    .meta-item dd { margin: 0; font-weight: 500; color: #1e293b; text-align: right; }
    .mono-val { font-family: monospace; font-weight: 700; }
    .history-timeline { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 1rem; }
    .history-entry { display: flex; gap: 0.75rem; font-size: 0.85rem; }
    .timeline-dot { width: 10px; height: 10px; border-radius: 50%; background: #2563eb; margin-top: 4px; flex-shrink: 0; }
    .history-details { display: flex; flex-direction: column; gap: 0.2rem; }
    .history-change { display: flex; align-items: center; gap: 0.35rem; }
    .status-chip { font-size: 0.7rem; padding: 1px 6px; border-radius: 4px; background: #e2e8f0; color: #475569; }
    .active-chip { font-weight: 700; background: #dbeafe; color: #1e40af; }
    .arrow { font-size: 0.75rem; color: #94a3b8; }
    .history-actor { font-size: 0.75rem; color: #94a3b8; }
    .history-comment { font-size: 0.8rem; color: #475569; font-style: italic; }
    .status-pill { display: inline-block; font-size: 0.75rem; padding: 2px 8px; border-radius: 12px; font-weight: 600; }
    .status-new { background: #e0f2fe; color: #0369a1; }
    .status-assigned { background: #e0e7ff; color: #3730a3; }
    .status-inprogress { background: #fef3c7; color: #92400e; }
    .status-waitingforrequester { background: #ffedd5; color: #c2410c; }
    .status-resolved { background: #dcfce7; color: #166534; }
    .status-closed { background: #f1f5f9; color: #64748b; }
    .priority-tag { font-size: 0.75rem; font-weight: 600; padding: 2px 8px; border-radius: 4px; }
    .tag-urgent { background: #fee2e2; color: #b91c1c; }
    .tag-high { background: #ffedd5; color: #c2410c; }
    .tag-normal { background: #e0f2fe; color: #0369a1; }
    .tag-low { background: #f1f5f9; color: #475569; }
    .primary-btn {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .primary-btn:hover:not(:disabled) { background: #1d4ed8; }
    .primary-btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn-cancel {
      background: #f1f5f9;
      border: 1px solid #cbd5e1;
      color: #475569;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      cursor: pointer;
    }
    .alert { padding: 0.75rem 1rem; border-radius: 6px; display: flex; justify-content: space-between; align-items: center; font-size: 0.9rem; }
    .alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
    .alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
    .alert-close { background: none; border: none; font-size: 1rem; cursor: pointer; color: inherit; }
    .btn-link { color: #2563eb; text-decoration: underline; }
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
      max-width: 500px;
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
    .modal-close { background: none; border: none; font-size: 1.1rem; cursor: pointer; }
    .modal-form { padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.35rem; }
    .form-group label { font-size: 0.8rem; font-weight: 600; color: #475569; }
    .form-group textarea {
      padding: 0.5rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
    }
    .modal-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 0.5rem; }
  `]
})
export class TicketDetailComponent implements OnInit {
  private readonly ticketService = inject(TicketService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  ticket = signal<TicketDetail | null>(null);
  agents = signal<SupportAgent[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  // Status transition modal
  showTransitionModal = signal<boolean>(false);
  pendingStatus = signal<TicketStatus>('InProgress');
  statusComment = '';
  isUpdatingStatus = signal<boolean>(false);

  // Messages
  messageBody = '';
  isInternalNote = false;
  isSendingMessage = signal<boolean>(false);

  isAgentOrAdmin(): boolean {
    return this.authService.hasAnyRole(['SupportAgent', 'Manager', 'Administrator']);
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadTicket(id);
      if (this.isAgentOrAdmin()) {
        this.loadAgents();
      }
    }
  }

  loadTicket(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.ticketService.getById(id).subscribe({
      next: (data) => {
        this.ticket.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Ticket not found or access denied.');
        this.isLoading.set(false);
      }
    });
  }

  loadAgents(): void {
    this.ticketService.getAgents().subscribe({
      next: (list) => this.agents.set(list),
      error: () => this.agents.set([])
    });
  }

  getAvailableTransitions(currentStatus: TicketStatus): TicketStatus[] {
    const allowed = ALLOWED_TICKET_TRANSITIONS[currentStatus] || [];
    if (!this.isAgentOrAdmin()) {
      // Requesters can only transition to 'Closed'
      return allowed.filter(s => s === 'Closed');
    }
    return allowed;
  }

  promptStatusTransition(target: TicketStatus): void {
    this.pendingStatus.set(target);
    this.statusComment = '';
    this.showTransitionModal.set(true);
  }

  closeTransitionModal(): void {
    this.showTransitionModal.set(false);
    this.statusComment = '';
  }

  executeStatusTransition(): void {
    const current = this.ticket();
    if (!current) return;

    this.isUpdatingStatus.set(true);
    this.errorMessage.set('');

    this.ticketService.changeStatus(current.id, {
      status: this.pendingStatus(),
      comment: this.statusComment.trim() || undefined
    }).subscribe({
      next: () => {
        this.successMessage.set(`Status changed to ${this.pendingStatus()}.`);
        this.isUpdatingStatus.set(false);
        this.closeTransitionModal();
        this.loadTicket(current.id);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Failed to change status.');
        this.isUpdatingStatus.set(false);
        this.closeTransitionModal();
      }
    });
  }

  onAssigneeChanged(newAssigneeId: string | null): void {
    const current = this.ticket();
    if (!current || !newAssigneeId || newAssigneeId === current.assigneeId) return;

    this.ticketService.assign(current.id, { assigneeId: newAssigneeId }).subscribe({
      next: () => {
        this.successMessage.set('Ticket assignee updated.');
        this.loadTicket(current.id);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Failed to update assignee.');
      }
    });
  }

  onSendMessage(): void {
    const current = this.ticket();
    if (!current || !this.messageBody.trim()) return;

    this.isSendingMessage.set(true);
    this.errorMessage.set('');

    this.ticketService.addMessage(current.id, {
      body: this.messageBody.trim(),
      isInternalNote: this.isAgentOrAdmin() && this.isInternalNote
    }).subscribe({
      next: (newMsg) => {
        this.ticket.update(t => t ? { ...t, messages: [...t.messages, newMsg] } : null);
        this.messageBody = '';
        this.isInternalNote = false;
        this.isSendingMessage.set(false);
        this.successMessage.set('Message posted.');
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Failed to send message.');
        this.isSendingMessage.set(false);
      }
    });
  }
}
