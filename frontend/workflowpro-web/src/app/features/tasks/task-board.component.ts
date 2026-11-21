import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectService } from '../../core/projects/project.service';
import { ProjectMember, ProjectSummary } from '../../core/projects/project.models';
import { TaskService } from '../../core/tasks/task.service';
import {
  ALLOWED_TASK_TRANSITIONS,
  CreateTaskRequest,
  TASK_PRIORITIES,
  TASK_STATUSES,
  TaskPriority,
  TaskStatus,
  TaskSummary
} from '../../core/tasks/task.models';

@Component({
  selector: 'app-task-board',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="tasks-page">
      <header class="page-header">
        <div class="header-titles">
          <h1>Tasks & Workflow</h1>
          <p class="subtitle">Track tasks, update progress with transition guardrails, and manage board flow</p>
        </div>

        <div class="header-actions">
          <div class="view-toggle" role="group" aria-label="View toggle">
            <button
              type="button"
              class="toggle-btn"
              [class.active]="activeView() === 'board'"
              (click)="activeView.set('board')">
              📋 Kanban Board
            </button>
            <button
              type="button"
              class="toggle-btn"
              [class.active]="activeView() === 'list'"
              (click)="activeView.set('list')">
              ☰ List View
            </button>
          </div>

          <button class="primary-btn" (click)="openCreateModal()" [disabled]="!selectedProjectId()">
            + New Task
          </button>
        </div>
      </header>

      <!-- Filter Bar -->
      <section class="filters-card">
        <div class="filter-group">
          <label for="project-select">Project</label>
          <select
            id="project-select"
            [ngModel]="selectedProjectId()"
            (ngModelChange)="onProjectSelected($event)">
            @for (p of projects(); track p.id) {
              <option [value]="p.id">[{{ p.projectCode }}] {{ p.name }}</option>
            }
          </select>
        </div>

        <div class="filter-group">
          <label for="search-tasks">Search</label>
          <input
            id="search-tasks"
            type="text"
            placeholder="Search tasks..."
            [(ngModel)]="searchQuery"
            (keyup.enter)="loadTasks()" />
        </div>

        <div class="filter-group">
          <label for="status-filter">Status</label>
          <select id="status-filter" [(ngModel)]="statusFilter" (change)="loadTasks()">
            <option value="All">All Statuses</option>
            @for (status of taskStatuses; track status) {
              <option [value]="status">{{ status }}</option>
            }
          </select>
        </div>

        <div class="filter-group">
          <label for="priority-filter">Priority</label>
          <select id="priority-filter" [(ngModel)]="priorityFilter" (change)="loadTasks()">
            <option value="All">All Priorities</option>
            @for (p of taskPriorities; track p) {
              <option [value]="p">{{ p }}</option>
            }
          </select>
        </div>

        <div class="filter-actions">
          <button class="filter-btn" (click)="loadTasks()">Filter</button>
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
          <p>Loading tasks...</p>
        </div>
      } @else if (projects().length === 0) {
        <div class="empty-card">
          <p>No projects available. Create a project first to manage tasks.</p>
          <a routerLink="/projects" class="primary-btn">Go to Projects</a>
        </div>
      } @else if (activeView() === 'board') {
        <!-- Kanban Board View -->
        <div class="kanban-board">
          @for (column of boardColumns; track column) {
            <div class="kanban-column" [class]="'col-' + column.toLowerCase()">
              <div class="column-header">
                <span class="column-title">{{ column }}</span>
                <span class="badge count-badge">{{ getTasksForStatus(column).length }}</span>
              </div>

              <div class="column-body">
                @for (task of getTasksForStatus(column); track task.id) {
                  <article class="task-card" [class]="'priority-' + task.priority.toLowerCase()">
                    <div class="card-meta">
                      <span class="project-tag">{{ task.projectCode }}</span>
                      <span class="priority-tag" [class]="'tag-' + task.priority.toLowerCase()">
                        {{ task.priority }}
                      </span>
                    </div>

                    <h3 class="task-title">
                      <a [routerLink]="['/tasks', task.id]">{{ task.title }}</a>
                    </h3>

                    <div class="task-info">
                      <div class="info-row">
                        <span class="info-label">Assignee:</span>
                        <span class="info-val">{{ task.assigneeName || 'Unassigned' }}</span>
                      </div>
                      @if (task.dueAtUtc) {
                        <div class="info-row">
                          <span class="info-label">Due:</span>
                          <span class="info-val date-val">{{ task.dueAtUtc | date:'mediumDate' }}</span>
                        </div>
                      }
                      @if (task.estimateMinutes) {
                        <div class="info-row">
                          <span class="info-label">Estimate:</span>
                          <span class="info-val">{{ task.estimateMinutes }}m</span>
                        </div>
                      }
                    </div>

                    <!-- Keyboard-accessible Move To dropdown -->
                    <div class="card-actions">
                      @if (getAllowedTransitions(task.status).length > 0) {
                        <div class="move-control">
                          <label [for]="'move-' + task.id" class="sr-only">
                            Move task {{ task.title }} to new status
                          </label>
                          <select
                            [id]="'move-' + task.id"
                            class="move-select"
                            [disabled]="isTransitioning() === task.id"
                            (change)="onQuickTransition(task, $event)">
                            <option value="" disabled selected>Move to...</option>
                            @for (target of getAllowedTransitions(task.status); track target) {
                              <option [value]="target">➔ {{ target }}</option>
                            }
                          </select>
                        </div>
                      } @else {
                        <span class="terminal-status">Final State</span>
                      }
                      <span class="version-tag" title="Optimistic concurrency version">v{{ task.version }}</span>
                    </div>
                  </article>
                } @empty {
                  <div class="column-empty">No tasks in {{ column }}</div>
                }
              </div>
            </div>
          }
        </div>
      } @else {
        <!-- List View -->
        <div class="table-card">
          <table class="data-table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Title</th>
                <th>Status</th>
                <th>Priority</th>
                <th>Assignee</th>
                <th>Due Date</th>
                <th>Estimate</th>
                <th>Version</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (task of tasks(); track task.id) {
                <tr>
                  <td><span class="project-tag">{{ task.projectCode }}</span></td>
                  <td class="task-title-cell">
                    <a [routerLink]="['/tasks', task.id]" class="table-title-link">{{ task.title }}</a>
                  </td>
                  <td>
                    <span class="status-pill" [class]="'status-' + task.status.toLowerCase()">
                      {{ task.status }}
                    </span>
                  </td>
                  <td>
                    <span class="priority-tag" [class]="'tag-' + task.priority.toLowerCase()">
                      {{ task.priority }}
                    </span>
                  </td>
                  <td>{{ task.assigneeName || 'Unassigned' }}</td>
                  <td>{{ task.dueAtUtc ? (task.dueAtUtc | date:'mediumDate') : '—' }}</td>
                  <td>{{ task.estimateMinutes ? task.estimateMinutes + 'm' : '—' }}</td>
                  <td><span class="version-tag">v{{ task.version }}</span></td>
                  <td>
                    <div class="table-actions">
                      <a [routerLink]="['/tasks', task.id]" class="action-btn">View</a>
                      @if (getAllowedTransitions(task.status).length > 0) {
                        <select
                          class="move-select compact"
                          [disabled]="isTransitioning() === task.id"
                          (change)="onQuickTransition(task, $event)">
                          <option value="" disabled selected>Move</option>
                          @for (target of getAllowedTransitions(task.status); track target) {
                            <option [value]="target">{{ target }}</option>
                          }
                        </select>
                      }
                    </div>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="9" class="empty-cell">No tasks match the active filters.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      <!-- Create Task Modal -->
      @if (showCreateModal()) {
        <div class="modal-backdrop" (click)="closeCreateModal()">
          <div class="modal-card" (click)="$event.stopPropagation()" role="dialog" aria-modal="true" aria-labelledby="modal-title">
            <header class="modal-header">
              <h2 id="modal-title">Create New Task</h2>
              <button class="modal-close" (click)="closeCreateModal()">✕</button>
            </header>

            <form (ngSubmit)="onCreateTask()" class="modal-form">
              <div class="form-group">
                <label for="new-title">Title *</label>
                <input
                  id="new-title"
                  type="text"
                  [(ngModel)]="createModel.title"
                  name="title"
                  maxlength="200"
                  required
                  placeholder="Task title (max 200 chars)" />
              </div>

              <div class="form-group">
                <label for="new-description">Description</label>
                <textarea
                  id="new-description"
                  rows="3"
                  [(ngModel)]="createModel.description"
                  name="description"
                  placeholder="Task details and acceptance criteria"></textarea>
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label for="new-priority">Priority *</label>
                  <select id="new-priority" [(ngModel)]="createModel.priority" name="priority" required>
                    @for (p of taskPriorities; track p) {
                      <option [value]="p">{{ p }}</option>
                    }
                  </select>
                </div>

                <div class="form-group">
                  <label for="new-assignee">Assignee (Project Members)</label>
                  <select id="new-assignee" [(ngModel)]="createModel.assigneeId" name="assigneeId">
                    <option [ngValue]="null">Unassigned</option>
                    @for (member of projectMembers(); track member.userId) {
                      <option [value]="member.userId">{{ member.displayName }} ({{ member.projectRole }})</option>
                    }
                  </select>
                </div>
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label for="new-due">Due Date</label>
                  <input
                    id="new-due"
                    type="date"
                    [(ngModel)]="createModelDueDate"
                    name="dueDate" />
                </div>

                <div class="form-group">
                  <label for="new-estimate">Estimate (minutes)</label>
                  <input
                    id="new-estimate"
                    type="number"
                    min="0"
                    [(ngModel)]="createModel.estimateMinutes"
                    name="estimateMinutes"
                    placeholder="e.g. 60" />
                </div>
              </div>

              @if (modalError()) {
                <div class="modal-error">{{ modalError() }}</div>
              }

              <footer class="modal-footer">
                <button type="button" class="btn-cancel" (click)="closeCreateModal()">Cancel</button>
                <button type="submit" class="primary-btn" [disabled]="isSubmitting()">
                  {{ isSubmitting() ? 'Creating...' : 'Create Task' }}
                </button>
              </footer>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .tasks-page {
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
    .header-actions {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .view-toggle {
      display: flex;
      background: #e2e8f0;
      padding: 3px;
      border-radius: 8px;
    }
    .toggle-btn {
      border: none;
      background: transparent;
      padding: 0.4rem 0.75rem;
      border-radius: 6px;
      font-size: 0.85rem;
      cursor: pointer;
      color: #475569;
      font-weight: 500;
    }
    .toggle-btn.active {
      background: #ffffff;
      color: #0f172a;
      box-shadow: 0 1px 3px rgba(0,0,0,0.1);
    }
    .primary-btn {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.55rem 1.1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.15s;
    }
    .primary-btn:hover:not(:disabled) { background: #1d4ed8; }
    .primary-btn:disabled { opacity: 0.5; cursor: not-allowed; }
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
      min-width: 160px;
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
      background: #ffffff;
    }
    .filter-actions {
      display: flex;
      gap: 0.5rem;
    }
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
    .loading-state {
      padding: 3rem;
      text-align: center;
      color: #64748b;
    }
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
    .empty-card {
      background: #ffffff;
      padding: 3rem;
      text-align: center;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      color: #64748b;
    }
    .kanban-board {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 1rem;
      align-items: start;
      overflow-x: auto;
      padding-bottom: 1rem;
    }
    .kanban-column {
      background: #f1f5f9;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      display: flex;
      flex-direction: column;
      max-height: calc(100vh - 280px);
    }
    .column-header {
      padding: 0.75rem 1rem;
      border-bottom: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-weight: 700;
      font-size: 0.9rem;
      color: #1e293b;
    }
    .count-badge {
      background: #cbd5e1;
      color: #0f172a;
      padding: 2px 7px;
      border-radius: 12px;
      font-size: 0.75rem;
    }
    .column-body {
      padding: 0.75rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      overflow-y: auto;
    }
    .task-card {
      background: #ffffff;
      border-radius: 6px;
      padding: 0.85rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
      border: 1px solid #e2e8f0;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      transition: transform 0.1s ease, box-shadow 0.1s ease;
    }
    .task-card:hover {
      box-shadow: 0 3px 6px rgba(0,0,0,0.08);
      transform: translateY(-1px);
    }
    .card-meta {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .project-tag {
      font-size: 0.7rem;
      font-weight: 700;
      color: #64748b;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      padding: 1px 5px;
      border-radius: 4px;
    }
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
    .task-title {
      margin: 0;
      font-size: 0.95rem;
      font-weight: 600;
      line-height: 1.35;
    }
    .task-title a {
      color: #0f172a;
      text-decoration: none;
    }
    .task-title a:hover { color: #2563eb; text-decoration: underline; }
    .task-info {
      font-size: 0.8rem;
      color: #64748b;
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
    }
    .info-row { display: flex; justify-content: space-between; }
    .info-label { color: #94a3b8; }
    .card-actions {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: 0.35rem;
      padding-top: 0.5rem;
      border-top: 1px dashed #e2e8f0;
    }
    .move-select {
      font-size: 0.75rem;
      padding: 3px 6px;
      border-radius: 4px;
      border: 1px solid #cbd5e1;
      background: #f8fafc;
      color: #1e293b;
      cursor: pointer;
    }
    .move-select.compact { padding: 2px 4px; font-size: 0.75rem; }
    .version-tag {
      font-size: 0.7rem;
      color: #94a3b8;
      font-family: monospace;
    }
    .terminal-status {
      font-size: 0.7rem;
      color: #94a3b8;
      font-style: italic;
    }
    .column-empty {
      padding: 1.5rem;
      text-align: center;
      color: #94a3b8;
      font-size: 0.8rem;
      border: 1px dashed #cbd5e1;
      border-radius: 6px;
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
    .table-title-link {
      font-weight: 600;
      color: #0f172a;
      text-decoration: none;
    }
    .table-title-link:hover { color: #2563eb; text-decoration: underline; }
    .status-pill {
      display: inline-block;
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
    .table-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .action-btn {
      color: #2563eb;
      text-decoration: none;
      font-weight: 500;
    }
    .empty-cell {
      text-align: center;
      color: #94a3b8;
      padding: 3rem !important;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      border: 0;
    }
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
      overflow: hidden;
    }
    .modal-header {
      padding: 1rem 1.25rem;
      border-bottom: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .modal-header h2 { margin: 0; font-size: 1.2rem; }
    .modal-close {
      background: none;
      border: none;
      font-size: 1.1rem;
      cursor: pointer;
      color: #64748b;
    }
    .modal-form {
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      flex: 1;
    }
    .form-row {
      display: flex;
      gap: 1rem;
    }
    .form-group label {
      font-size: 0.8rem;
      font-weight: 600;
      color: #475569;
    }
    .form-group input, .form-group textarea, .form-group select {
      padding: 0.5rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
    }
    .modal-error {
      background: #fee2e2;
      border: 1px solid #fecaca;
      color: #991b1b;
      padding: 0.5rem 0.75rem;
      border-radius: 6px;
      font-size: 0.85rem;
    }
    .modal-footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 0.5rem;
    }
    .btn-cancel {
      background: #f1f5f9;
      border: 1px solid #cbd5e1;
      color: #475569;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      cursor: pointer;
    }
  `]
})
export class TaskBoardComponent implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly taskService = inject(TaskService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly taskStatuses = TASK_STATUSES;
  readonly taskPriorities = TASK_PRIORITIES;
  readonly boardColumns: TaskStatus[] = ['Backlog', 'ToDo', 'InProgress', 'Blocked', 'Done', 'Cancelled'];

  activeView = signal<'board' | 'list'>('board');
  projects = signal<ProjectSummary[]>([]);
  selectedProjectId = signal<string>('');
  projectMembers = signal<ProjectMember[]>([]);
  tasks = signal<TaskSummary[]>([]);
  isLoading = signal<boolean>(false);
  isTransitioning = signal<string | null>(null);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  searchQuery = '';
  statusFilter = 'All';
  priorityFilter = 'All';

  // Modal State
  showCreateModal = signal<boolean>(false);
  isSubmitting = signal<boolean>(false);
  modalError = signal<string>('');
  createModel: CreateTaskRequest = {
    title: '',
    description: '',
    priority: 'Normal',
    assigneeId: null,
    estimateMinutes: null
  };
  createModelDueDate = '';

  ngOnInit(): void {
    this.loadProjects();
  }

  loadProjects(): void {
    this.projectService.list('All', undefined, 1, 100).subscribe({
      next: (res) => {
        this.projects.set(res.items);
        const routeProjectId = this.route.snapshot.paramMap.get('id');
        if (routeProjectId && res.items.some(p => p.id === routeProjectId)) {
          this.selectedProjectId.set(routeProjectId);
        } else if (res.items.length > 0) {
          this.selectedProjectId.set(res.items[0].id);
        }

        if (this.selectedProjectId()) {
          this.loadProjectMembers(this.selectedProjectId());
          this.loadTasks();
        }
      },
      error: (err) => {
        this.errorMessage.set('Failed to load projects: ' + (err.error?.detail || err.message));
      }
    });
  }

  onProjectSelected(projectId: string): void {
    this.selectedProjectId.set(projectId);
    this.loadProjectMembers(projectId);
    this.loadTasks();
  }

  loadProjectMembers(projectId: string): void {
    this.projectService.getMembers(projectId).subscribe({
      next: (members) => this.projectMembers.set(members),
      error: () => this.projectMembers.set([])
    });
  }

  loadTasks(): void {
    const projectId = this.selectedProjectId();
    if (!projectId) return;

    this.isLoading.set(true);
    this.errorMessage.set('');

    this.taskService.listProjectTasks(projectId, {
      status: this.statusFilter !== 'All' ? this.statusFilter : undefined,
      priority: this.priorityFilter !== 'All' ? this.priorityFilter : undefined,
      search: this.searchQuery.trim() || undefined,
      page: 1,
      pageSize: 100
    }).subscribe({
      next: (res) => {
        this.tasks.set(res.items);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Failed to load tasks: ' + (err.error?.detail || err.message));
        this.isLoading.set(false);
      }
    });
  }

  resetFilters(): void {
    this.searchQuery = '';
    this.statusFilter = 'All';
    this.priorityFilter = 'All';
    this.loadTasks();
  }

  getTasksForStatus(status: TaskStatus): TaskSummary[] {
    return this.tasks().filter(t => t.status === status);
  }

  getAllowedTransitions(currentStatus: TaskStatus): TaskStatus[] {
    return ALLOWED_TASK_TRANSITIONS[currentStatus] || [];
  }

  onQuickTransition(task: TaskSummary, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const nextStatus = select.value as TaskStatus;
    if (!nextStatus) return;

    this.isTransitioning.set(task.id);
    this.errorMessage.set('');

    this.taskService.changeStatus(task.id, {
      status: nextStatus,
      version: task.version
    }).subscribe({
      next: (updated) => {
        this.tasks.update(list => list.map(t => t.id === updated.id ? updated : t));
        this.successMessage.set(`Task "${task.title}" moved to ${nextStatus}.`);
        this.isTransitioning.set(null);
        select.value = '';
      },
      error: (err) => {
        if (err.status === 409) {
          this.errorMessage.set(`Conflict: Task "${task.title}" was modified by another operation. Refreshing...`);
          this.loadTasks();
        } else {
          this.errorMessage.set(err.error?.detail || 'Failed to update task status.');
        }
        this.isTransitioning.set(null);
        select.value = '';
      }
    });
  }

  openCreateModal(): void {
    this.modalError.set('');
    this.createModel = {
      title: '',
      description: '',
      priority: 'Normal',
      assigneeId: null,
      estimateMinutes: null
    };
    this.createModelDueDate = '';
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
    this.modalError.set('');
  }

  onCreateTask(): void {
    if (!this.createModel.title.trim()) {
      this.modalError.set('Title is required.');
      return;
    }

    const projectId = this.selectedProjectId();
    if (!projectId) return;

    this.isSubmitting.set(true);
    this.modalError.set('');

    const payload: CreateTaskRequest = {
      ...this.createModel,
      dueAtUtc: this.createModelDueDate ? new Date(this.createModelDueDate).toISOString() : null
    };

    this.taskService.create(projectId, payload).subscribe({
      next: (created) => {
        this.tasks.update(list => [created, ...list]);
        this.successMessage.set(`Task "${created.title}" created successfully.`);
        this.isSubmitting.set(false);
        this.closeCreateModal();
      },
      error: (err) => {
        this.modalError.set(err.error?.detail || 'Failed to create task.');
        this.isSubmitting.set(false);
      }
    });
  }
}
