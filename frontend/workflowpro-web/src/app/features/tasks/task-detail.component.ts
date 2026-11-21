import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TaskService } from '../../core/tasks/task.service';
import { ProjectService } from '../../core/projects/project.service';
import { ProjectMember } from '../../core/projects/project.models';
import {
  ALLOWED_TASK_TRANSITIONS,
  ChangeTaskStatusRequest,
  TASK_PRIORITIES,
  TaskDetail,
  TaskPriority,
  TaskStatus,
  UpdateTaskRequest
} from '../../core/tasks/task.models';

@Component({
  selector: 'app-task-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="task-detail-container">
      @if (isLoading()) {
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Loading task details...</p>
        </div>
      } @else if (errorMessage() && !task()) {
        <div class="alert alert-error">
          <span>{{ errorMessage() }}</span>
          <a routerLink="/tasks" class="btn-link">Back to Tasks</a>
        </div>
      } @else if (task(); as item) {
        <!-- Breadcrumb & Top Bar -->
        <nav class="breadcrumb-nav">
          <a routerLink="/tasks">Tasks</a>
          <span class="sep">/</span>
          <span class="breadcrumb-proj">[{{ item.projectCode }}] {{ item.projectName }}</span>
          <span class="sep">/</span>
          <span class="breadcrumb-current">{{ item.title }}</span>
        </nav>

        <!-- Notification Alerts -->
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
              <span class="project-tag">{{ item.projectCode }}</span>
              <span class="status-pill" [class]="'status-' + item.status.toLowerCase()">
                {{ item.status }}
              </span>
              <span class="priority-tag" [class]="'tag-' + item.priority.toLowerCase()">
                {{ item.priority }}
              </span>
              <span class="version-tag" title="Optimistic Concurrency Version">Version {{ item.version }}</span>
            </div>
            <h1 class="task-heading">{{ item.title }}</h1>
          </div>

          <div class="header-actions">
            <button class="btn-edit" (click)="openEditModal()">Edit Task</button>
          </div>
        </header>

        <!-- Status Transition Action Bar -->
        <section class="transition-bar" aria-label="Status transitions">
          <span class="bar-label">Available Transitions:</span>
          @if (getAllowedTransitions(item.status).length > 0) {
            <div class="transition-buttons">
              @for (target of getAllowedTransitions(item.status); track target) {
                <button
                  type="button"
                  class="transition-btn"
                  [class]="'btn-trans-' + target.toLowerCase()"
                  [disabled]="isTransitioning()"
                  (click)="promptStatusTransition(target)">
                  Move to {{ target }}
                </button>
              }
            </div>
          } @else {
            <span class="final-state-msg">This task is in a terminal state (no further transitions permitted).</span>
          }
        </section>

        <!-- Two Column Content Grid -->
        <div class="content-grid">
          <!-- Left Column: Details & Discussion -->
          <div class="main-column">
            <!-- Description Card -->
            <section class="content-card description-card">
              <h2>Description</h2>
              @if (item.description) {
                <div class="description-body">{{ item.description }}</div>
              } @else {
                <p class="empty-text">No description provided for this task.</p>
              }
            </section>

            <!-- Comments & Discussion -->
            <section class="content-card comments-card">
              <div class="card-title-row">
                <h2>Discussion ({{ item.comments.length }})</h2>
              </div>

              <!-- Add Comment Box -->
              <form (ngSubmit)="onAddComment()" class="comment-form">
                <textarea
                  rows="3"
                  [(ngModel)]="newCommentBody"
                  name="commentBody"
                  placeholder="Add a comment or progress update..."
                  required
                  [disabled]="isSubmittingComment()"></textarea>

                <div class="comment-form-actions">
                  <button
                    type="submit"
                    class="primary-btn"
                    [disabled]="!newCommentBody.trim() || isSubmittingComment()">
                    {{ isSubmittingComment() ? 'Posting...' : 'Post Comment' }}
                  </button>
                </div>
              </form>

              <!-- Comments List -->
              <div class="comments-list">
                @for (c of item.comments; track c.id) {
                  <article class="comment-item">
                    <header class="comment-header">
                      <span class="author-name">{{ c.authorName }}</span>
                      <time class="comment-time">{{ c.createdAtUtc | date:'medium' }}</time>
                    </header>
                    <div class="comment-body">{{ c.body }}</div>
                  </article>
                } @empty {
                  <p class="empty-text">No comments yet. Start the conversation!</p>
                }
              </div>
            </section>
          </div>

          <!-- Right Column: Sidebar & Audit History -->
          <aside class="sidebar-column">
            <!-- Metadata Card -->
            <section class="content-card meta-card">
              <h3>Task Properties</h3>
              <dl class="meta-list">
                <div class="meta-item">
                  <dt>Assignee</dt>
                  <dd>{{ item.assigneeName || 'Unassigned' }}</dd>
                </div>
                <div class="meta-item">
                  <dt>Created By</dt>
                  <dd>{{ item.createdByName }}</dd>
                </div>
                <div class="meta-item">
                  <dt>Due Date</dt>
                  <dd>{{ item.dueAtUtc ? (item.dueAtUtc | date:'mediumDate') : 'None' }}</dd>
                </div>
                <div class="meta-item">
                  <dt>Estimate</dt>
                  <dd>{{ item.estimateMinutes ? item.estimateMinutes + ' minutes' : 'None' }}</dd>
                </div>
                <div class="meta-item">
                  <dt>Created</dt>
                  <dd>{{ item.createdAtUtc | date:'medium' }}</dd>
                </div>
                @if (item.updatedAtUtc) {
                  <div class="meta-item">
                    <dt>Last Updated</dt>
                    <dd>{{ item.updatedAtUtc | date:'medium' }}</dd>
                  </div>
                }
                <div class="meta-item">
                  <dt>Version</dt>
                  <dd><span class="version-tag">v{{ item.version }}</span></dd>
                </div>
              </dl>
            </section>

            <!-- Status Audit Timeline -->
            <section class="content-card history-card">
              <h3>Status History</h3>
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

        <!-- Status Transition Modal with Comment -->
        @if (showTransitionModal()) {
          <div class="modal-backdrop" (click)="closeTransitionModal()">
            <div class="modal-card" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
              <header class="modal-header">
                <h2>Transition to {{ pendingStatus() }}</h2>
                <button class="modal-close" (click)="closeTransitionModal()">✕</button>
              </header>

              <form (ngSubmit)="executeStatusTransition()" class="modal-form">
                <p class="modal-info">
                  Confirm transitioning task status from <strong>{{ item.status }}</strong> to <strong>{{ pendingStatus() }}</strong>.
                </p>

                <div class="form-group">
                  <label for="transition-comment">Comment (optional)</label>
                  <textarea
                    id="transition-comment"
                    rows="3"
                    [(ngModel)]="transitionComment"
                    name="transitionComment"
                    placeholder="Reason or notes for this status change"></textarea>
                </div>

                <footer class="modal-footer">
                  <button type="button" class="btn-cancel" (click)="closeTransitionModal()">Cancel</button>
                  <button type="submit" class="primary-btn" [disabled]="isTransitioning()">
                    {{ isTransitioning() ? 'Updating...' : 'Confirm Transition' }}
                  </button>
                </footer>
              </form>
            </div>
          </div>
        }

        <!-- Edit Task Modal -->
        @if (showEditModal()) {
          <div class="modal-backdrop" (click)="closeEditModal()">
            <div class="modal-card" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
              <header class="modal-header">
                <h2>Edit Task</h2>
                <button class="modal-close" (click)="closeEditModal()">✕</button>
              </header>

              <form (ngSubmit)="onSaveEdit()" class="modal-form">
                <div class="form-group">
                  <label for="edit-title">Title *</label>
                  <input
                    id="edit-title"
                    type="text"
                    [(ngModel)]="editModel.title"
                    name="editTitle"
                    maxlength="200"
                    required />
                </div>

                <div class="form-group">
                  <label for="edit-description">Description</label>
                  <textarea
                    id="edit-description"
                    rows="3"
                    [(ngModel)]="editModel.description"
                    name="editDescription"></textarea>
                </div>

                <div class="form-row">
                  <div class="form-group">
                    <label for="edit-priority">Priority *</label>
                    <select id="edit-priority" [(ngModel)]="editModel.priority" name="editPriority">
                      @for (p of taskPriorities; track p) {
                        <option [value]="p">{{ p }}</option>
                      }
                    </select>
                  </div>

                  <div class="form-group">
                    <label for="edit-assignee">Assignee</label>
                    <select id="edit-assignee" [(ngModel)]="editModel.assigneeId" name="editAssignee">
                      <option [ngValue]="null">Unassigned</option>
                      @for (member of projectMembers(); track member.userId) {
                        <option [value]="member.userId">{{ member.displayName }}</option>
                      }
                    </select>
                  </div>
                </div>

                <div class="form-row">
                  <div class="form-group">
                    <label for="edit-due">Due Date</label>
                    <input
                      id="edit-due"
                      type="date"
                      [(ngModel)]="editModelDueDate"
                      name="editDue" />
                  </div>

                  <div class="form-group">
                    <label for="edit-estimate">Estimate (minutes)</label>
                    <input
                      id="edit-estimate"
                      type="number"
                      min="0"
                      [(ngModel)]="editModel.estimateMinutes"
                      name="editEstimate" />
                  </div>
                </div>

                @if (editError()) {
                  <div class="modal-error">{{ editError() }}</div>
                }

                <footer class="modal-footer">
                  <button type="button" class="btn-cancel" (click)="closeEditModal()">Cancel</button>
                  <button type="submit" class="primary-btn" [disabled]="isSavingEdit()">
                    {{ isSavingEdit() ? 'Saving...' : 'Save Changes' }}
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
    .task-detail-container {
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
    .breadcrumb-nav a {
      color: #2563eb;
      text-decoration: none;
    }
    .breadcrumb-nav a:hover { text-decoration: underline; }
    .sep { color: #94a3b8; }
    .breadcrumb-current { font-weight: 600; color: #1e293b; }
    .detail-header {
      background: #ffffff;
      padding: 1.25rem 1.5rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
    }
    .badges-row {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      margin-bottom: 0.5rem;
    }
    .project-tag {
      font-size: 0.75rem;
      font-weight: 700;
      color: #475569;
      background: #f1f5f9;
      padding: 2px 8px;
      border-radius: 4px;
    }
    .task-heading {
      margin: 0;
      font-size: 1.6rem;
      font-weight: 700;
      color: #0f172a;
    }
    .btn-edit {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
      color: #1e293b;
    }
    .btn-edit:hover { background: #f8fafc; }
    .transition-bar {
      background: #f8fafc;
      padding: 0.75rem 1.25rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .bar-label {
      font-size: 0.85rem;
      font-weight: 600;
      color: #475569;
    }
    .transition-buttons {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .transition-btn {
      padding: 0.4rem 0.85rem;
      border-radius: 6px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.15s;
    }
    .btn-trans-todo { background: #e0e7ff; color: #3730a3; border-color: #c7d2fe; }
    .btn-trans-inprogress { background: #fef3c7; color: #92400e; border-color: #fde68a; }
    .btn-trans-blocked { background: #fee2e2; color: #991b1b; border-color: #fecaca; }
    .btn-trans-done { background: #dcfce7; color: #166534; border-color: #bbf7d0; }
    .btn-trans-cancelled { background: #e2e8f0; color: #475569; border-color: #cbd5e1; }
    .btn-trans-backlog { background: #f1f5f9; color: #475569; border-color: #cbd5e1; }
    .transition-btn:hover:not(:disabled) { opacity: 0.85; transform: translateY(-1px); }
    .final-state-msg { font-size: 0.85rem; color: #64748b; font-style: italic; }
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
    .comment-form {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      margin-bottom: 1.5rem;
    }
    .comment-form textarea {
      padding: 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-family: inherit;
      font-size: 0.9rem;
    }
    .comment-form-actions {
      display: flex;
      justify-content: flex-end;
    }
    .comments-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .comment-item {
      padding: 0.85rem;
      background: #f8fafc;
      border-radius: 6px;
      border: 1px solid #e2e8f0;
    }
    .comment-header {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.35rem;
      font-size: 0.8rem;
    }
    .author-name { font-weight: 600; color: #1e293b; }
    .comment-time { color: #94a3b8; }
    .comment-body { font-size: 0.9rem; color: #334155; white-space: pre-wrap; }
    .meta-list {
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .meta-item {
      display: flex;
      justify-content: space-between;
      font-size: 0.85rem;
    }
    .meta-item dt { color: #64748b; }
    .meta-item dd { margin: 0; font-weight: 500; color: #1e293b; text-align: right; }
    .history-timeline {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: 1rem;
      position: relative;
    }
    .history-entry {
      display: flex;
      gap: 0.75rem;
      font-size: 0.85rem;
    }
    .timeline-dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: #2563eb;
      margin-top: 4px;
      flex-shrink: 0;
    }
    .history-details { display: flex; flex-direction: column; gap: 0.2rem; }
    .history-change { display: flex; align-items: center; gap: 0.35rem; }
    .status-chip {
      font-size: 0.7rem;
      padding: 1px 6px;
      border-radius: 4px;
      background: #e2e8f0;
      color: #475569;
    }
    .active-chip { font-weight: 700; background: #dbeafe; color: #1e40af; }
    .arrow { font-size: 0.75rem; color: #94a3b8; }
    .history-actor { font-size: 0.75rem; color: #94a3b8; }
    .history-comment { font-size: 0.8rem; color: #475569; font-style: italic; }
    .empty-text { color: #94a3b8; font-size: 0.9rem; margin: 0; }
    .status-pill {
      font-size: 0.75rem;
      padding: 2px 8px;
      border-radius: 12px;
      font-weight: 600;
    }
    .status-backlog { background: #f1f5f9; color: #475569; }
    .status-todo { background: #e0e7ff; color: #3730a3; }
    .status-inprogress { background: #fef3c7; color: #92400e; }
    .status-blocked { background: #fee2e2; color: #991b1b; }
    .status-done { background: #dcfce7; color: #166534; }
    .status-cancelled { background: #e2e8f0; color: #64748b; }
    .priority-tag {
      font-size: 0.75rem;
      font-weight: 600;
      padding: 2px 8px;
      border-radius: 4px;
    }
    .tag-urgent { background: #fee2e2; color: #b91c1c; }
    .tag-high { background: #ffedd5; color: #c2410c; }
    .tag-normal { background: #e0f2fe; color: #0369a1; }
    .tag-low { background: #f1f5f9; color: #475569; }
    .version-tag {
      font-size: 0.75rem;
      color: #64748b;
      font-family: monospace;
      background: #f1f5f9;
      padding: 2px 6px;
      border-radius: 4px;
    }
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
      max-width: 520px;
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
export class TaskDetailComponent implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly projectService = inject(ProjectService);
  private readonly route = inject(ActivatedRoute);

  readonly taskPriorities = TASK_PRIORITIES;

  task = signal<TaskDetail | null>(null);
  projectMembers = signal<ProjectMember[]>([]);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  // Transition Modal
  showTransitionModal = signal<boolean>(false);
  pendingStatus = signal<TaskStatus>('ToDo');
  transitionComment = '';
  isTransitioning = signal<boolean>(false);

  // Edit Modal
  showEditModal = signal<boolean>(false);
  isSavingEdit = signal<boolean>(false);
  editError = signal<string>('');
  editModel: UpdateTaskRequest = {
    title: '',
    description: '',
    priority: 'Normal',
    assigneeId: null,
    estimateMinutes: null,
    version: 1
  };
  editModelDueDate = '';

  // Comment Submission
  newCommentBody = '';
  isSubmittingComment = signal<boolean>(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadTask(id);
    }
  }

  loadTask(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.taskService.getById(id).subscribe({
      next: (data) => {
        this.task.set(data);
        this.isLoading.set(false);
        this.loadProjectMembers(data.projectId);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Task not found or access denied.');
        this.isLoading.set(false);
      }
    });
  }

  loadProjectMembers(projectId: string): void {
    this.projectService.getMembers(projectId).subscribe({
      next: (members) => this.projectMembers.set(members),
      error: () => this.projectMembers.set([])
    });
  }

  getAllowedTransitions(currentStatus: TaskStatus): TaskStatus[] {
    return ALLOWED_TASK_TRANSITIONS[currentStatus] || [];
  }

  promptStatusTransition(target: TaskStatus): void {
    this.pendingStatus.set(target);
    this.transitionComment = '';
    this.showTransitionModal.set(true);
  }

  closeTransitionModal(): void {
    this.showTransitionModal.set(false);
    this.transitionComment = '';
  }

  executeStatusTransition(): void {
    const currentTask = this.task();
    if (!currentTask) return;

    this.isTransitioning.set(true);
    this.errorMessage.set('');

    this.taskService.changeStatus(currentTask.id, {
      status: this.pendingStatus(),
      version: currentTask.version,
      comment: this.transitionComment.trim() || undefined
    }).subscribe({
      next: () => {
        this.successMessage.set(`Task successfully moved to ${this.pendingStatus()}.`);
        this.isTransitioning.set(false);
        this.closeTransitionModal();
        this.loadTask(currentTask.id);
      },
      error: (err) => {
        if (err.status === 409) {
          this.errorMessage.set('Concurrency conflict: The task was modified by another user. Reloading latest data...');
          this.loadTask(currentTask.id);
        } else {
          this.errorMessage.set(err.error?.detail || 'Failed to update task status.');
        }
        this.isTransitioning.set(false);
        this.closeTransitionModal();
      }
    });
  }

  openEditModal(): void {
    const current = this.task();
    if (!current) return;

    this.editError.set('');
    this.editModel = {
      title: current.title,
      description: current.description,
      priority: current.priority,
      assigneeId: current.assigneeId,
      dueAtUtc: current.dueAtUtc,
      estimateMinutes: current.estimateMinutes,
      version: current.version
    };
    this.editModelDueDate = current.dueAtUtc ? current.dueAtUtc.split('T')[0] : '';
    this.showEditModal.set(true);
  }

  closeEditModal(): void {
    this.showEditModal.set(false);
    this.editError.set('');
  }

  onSaveEdit(): void {
    const current = this.task();
    if (!current) return;

    if (!this.editModel.title.trim()) {
      this.editError.set('Title is required.');
      return;
    }

    this.isSavingEdit.set(true);
    this.editError.set('');

    const payload: UpdateTaskRequest = {
      ...this.editModel,
      dueAtUtc: this.editModelDueDate ? new Date(this.editModelDueDate).toISOString() : null,
      version: current.version
    };

    this.taskService.update(current.id, payload).subscribe({
      next: () => {
        this.successMessage.set('Task updated successfully.');
        this.isSavingEdit.set(false);
        this.closeEditModal();
        this.loadTask(current.id);
      },
      error: (err) => {
        if (err.status === 409) {
          this.editError.set('Concurrency conflict: The task was updated by another user. Please reload.');
          this.loadTask(current.id);
        } else {
          this.editError.set(err.error?.detail || 'Failed to update task.');
        }
        this.isSavingEdit.set(false);
      }
    });
  }

  onAddComment(): void {
    const current = this.task();
    if (!current || !this.newCommentBody.trim()) return;

    this.isSubmittingComment.set(true);
    this.errorMessage.set('');

    this.taskService.addComment(current.id, { body: this.newCommentBody.trim() }).subscribe({
      next: (comment) => {
        this.task.update(t => t ? { ...t, comments: [...t.comments, comment] } : null);
        this.newCommentBody = '';
        this.isSubmittingComment.set(false);
        this.successMessage.set('Comment posted.');
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Failed to post comment.');
        this.isSubmittingComment.set(false);
      }
    });
  }
}
