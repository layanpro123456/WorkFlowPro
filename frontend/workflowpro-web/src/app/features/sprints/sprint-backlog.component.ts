import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { ProjectService } from '../../core/projects/project.service';
import { ProjectDetail } from '../../core/projects/project.models';
import { SprintService } from '../../core/sprints/sprint.service';
import {
  CreateSprintRequest,
  ISSUE_TYPES,
  IssueType,
  Sprint,
  SprintBurndown,
  VelocityChart
} from '../../core/sprints/sprint.models';
import { TaskService } from '../../core/tasks/task.service';
import { TASK_PRIORITIES, TaskPriority, TaskSummary } from '../../core/tasks/task.models';

@Component({
  selector: 'app-sprint-backlog',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DragDropModule],
  template: `
    <div class="backlog-page">
      <nav class="breadcrumb">
        <a [routerLink]="['/projects', projectId()]">← Back to Project</a>
        <span>/</span>
        <a [routerLink]="['/projects', projectId(), 'tasks']">Kanban Board</a>
      </nav>

      <header class="page-header">
        <div>
          <h1>{{ project()?.projectCode || 'Project' }} — Sprints, Backlog &amp; Charts</h1>
          <p class="subtitle">Plan sprints, drag issues from the product backlog, and monitor burndown &amp; velocity</p>
        </div>
        <div class="header-actions">
          <button type="button" class="btn-secondary" (click)="openNewIssueModal()">+ New Issue</button>
          <button type="button" class="primary-btn" (click)="openCreateSprintModal()">+ Create Sprint</button>
        </div>
      </header>

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

      @if (isLoading()) {
        <div class="loading-state">Loading backlog and sprints...</div>
      } @else {
        <!-- Agile Analytics Row: Burndown & Velocity -->
        <section class="charts-grid">
          <article class="panel-card">
            <div class="panel-head">
              <h2>Sprint Burndown</h2>
              @if (sprints().length > 0) {
                <select [ngModel]="selectedBurndownSprintId()" (ngModelChange)="loadBurndown($event)">
                  @for (sp of sprints(); track sp.id) {
                    <option [value]="sp.id">{{ sp.name }} ({{ sp.status }})</option>
                  }
                </select>
              }
            </div>
            @if (burndown(); as bd) {
              <div class="chart-meta">
                <span>Total Scope: <strong>{{ bd.totalPoints }} pts</strong></span>
                <span>Status: <strong>{{ bd.status }}</strong></span>
              </div>
              <svg viewBox="0 0 420 160" class="burndown-svg" aria-label="Sprint burndown chart">
                <line x1="32" y1="15" x2="32" y2="135" stroke="#cbd5e1" stroke-width="1" />
                <line x1="32" y1="135" x2="405" y2="135" stroke="#cbd5e1" stroke-width="1" />
                @if (idealPolyline()) {
                  <polyline [attr.points]="idealPolyline()" fill="none" stroke="#94a3b8" stroke-width="2" stroke-dasharray="5,4" />
                }
                @if (actualPolyline()) {
                  <polyline [attr.points]="actualPolyline()" fill="none" stroke="#2563eb" stroke-width="2.5" />
                }
                <text x="36" y="25" font-size="10" fill="#64748b">{{ bd.totalPoints }} pts</text>
                <text x="36" y="132" font-size="10" fill="#64748b">0</text>
              </svg>
              <div class="chart-legend">
                <span><i class="dot-ideal"></i> Ideal</span>
                <span><i class="dot-actual"></i> Actual Remaining</span>
              </div>
            } @else {
              <p class="empty-hint">Start or select a sprint to view burndown telemetry.</p>
            }
          </article>

          <article class="panel-card">
            <div class="panel-head">
              <h2>Team Velocity</h2>
              @if (velocity(); as vel) {
                <span class="vel-avg">Avg: <strong>{{ vel.averageCompletedPoints }} pts</strong> / sprint</span>
              }
            </div>
            @if (velocity(); as vel) {
              @if (vel.sprints.length > 0) {
                <div class="velocity-bars">
                  @for (bar of vel.sprints; track bar.sprintId) {
                    <div class="vel-col">
                      <div class="vel-bar-pair">
                        <div class="vel-bar committed" [style.height.%]="getBarHeight(bar.committedPoints)" [title]="'Committed: ' + bar.committedPoints + ' pts'">
                          <span>{{ bar.committedPoints }}</span>
                        </div>
                        <div class="vel-bar completed" [style.height.%]="getBarHeight(bar.completedPoints)" [title]="'Completed: ' + bar.completedPoints + ' pts'">
                          <span>{{ bar.completedPoints }}</span>
                        </div>
                      </div>
                      <span class="vel-label" [title]="bar.sprintName">{{ bar.sprintName }}</span>
                    </div>
                  }
                </div>
                <div class="chart-legend">
                  <span><i class="dot-committed"></i> Committed</span>
                  <span><i class="dot-completed"></i> Completed</span>
                </div>
              } @else {
                <p class="empty-hint">Complete a sprint to build historical velocity metrics.</p>
              }
            }
          </article>
        </section>

        <!-- Epics Strip -->
        @if (epics().length > 0) {
          <section class="panel-card epics-strip">
            <div class="panel-head">
              <h2>Epics ({{ epics().length }})</h2>
            </div>
            <div class="epic-pills">
              @for (ep of epics(); track ep.id) {
                <a [routerLink]="['/tasks', ep.id]" class="epic-card-pill">
                  <span class="issue-type-badge type-epic">⚡ Epic</span>
                  <strong>{{ ep.taskKey }}</strong>
                  <span>{{ ep.title }}</span>
                  <span class="status-mini">{{ ep.status }}</span>
                </a>
              }
            </div>
          </section>
        }

        <!-- Drag & Drop Sprint + Backlog Workspace -->
        <div class="planner-stack" cdkDropListGroup>
          @for (sprint of openSprints(); track sprint.id) {
            <section class="panel-card sprint-box" [class.sprint-active]="sprint.status === 'Active'">
              <header class="sprint-header">
                <div class="sprint-title-row">
                  <span class="sprint-badge" [class]="'sp-' + sprint.status.toLowerCase()">{{ sprint.status }}</span>
                  <h3>{{ sprint.name }}</h3>
                  @if (sprint.goal) {
                    <span class="sprint-goal">— {{ sprint.goal }}</span>
                  }
                </div>
                <div class="sprint-controls">
                  <span class="pts-summary">
                    {{ getSprintTasks(sprint.id).length }} issues • {{ getSprintPoints(sprint.id) }} pts
                  </span>
                  @if (sprint.status === 'Planned') {
                    <button type="button" class="primary-btn btn-sm" (click)="openStartSprintModal(sprint)">
                      Start Sprint
                    </button>
                  } @else if (sprint.status === 'Active') {
                    <button type="button" class="btn-complete btn-sm" (click)="openCompleteSprintModal(sprint)">
                      Complete Sprint
                    </button>
                  }
                </div>
              </header>

              <div
                cdkDropList
                [cdkDropListData]="sprint.id"
                (cdkDropListDropped)="onDropToContainer($event, sprint.id)"
                class="issue-drop-list">
                @for (task of getSprintTasks(sprint.id); track task.id) {
                  <div class="issue-row" cdkDrag [cdkDragData]="task">
                    <div class="issue-left">
                      <span class="issue-type-badge" [class]="'type-' + (task.issueType || 'Task').toLowerCase()">
                        {{ getIssueIcon(task.issueType) }} {{ task.issueType || 'Task' }}
                      </span>
                      <a [routerLink]="['/tasks', task.id]" class="issue-key">{{ task.taskKey }}</a>
                      <a [routerLink]="['/tasks', task.id]" class="issue-title">{{ task.title }}</a>
                      @if (task.epicKey) {
                        <span class="epic-chip">{{ task.epicKey }}</span>
                      }
                      @if (task.isBlockedByOpenIssue) {
                        <span class="blocked-pill">⛔ Blocked</span>
                      }
                    </div>
                    <div class="issue-right">
                      <span class="status-mini">{{ task.status }}</span>
                      @if (task.storyPoints != null) {
                        <span class="points-pill">{{ task.storyPoints }} pts</span>
                      }
                      <span class="assignee-mini">{{ task.assigneeName || 'Unassigned' }}</span>
                    </div>
                  </div>
                } @empty {
                  <div class="drop-empty">Drag issues here from the Product Backlog to plan this sprint.</div>
                }
              </div>
            </section>
          }

          <!-- Product Backlog -->
          <section class="panel-card backlog-box">
            <header class="sprint-header">
              <div class="sprint-title-row">
                <h3>Product Backlog</h3>
                <span class="pts-summary">{{ backlogTasks().length }} issues • {{ backlogPoints() }} pts</span>
              </div>
            </header>

            <div
              cdkDropList
              [cdkDropListData]="null"
              (cdkDropListDropped)="onDropToContainer($event, null)"
              class="issue-drop-list">
              @for (task of backlogTasks(); track task.id) {
                <div class="issue-row" cdkDrag [cdkDragData]="task">
                  <div class="issue-left">
                    <span class="issue-type-badge" [class]="'type-' + (task.issueType || 'Task').toLowerCase()">
                      {{ getIssueIcon(task.issueType) }} {{ task.issueType || 'Task' }}
                    </span>
                    <a [routerLink]="['/tasks', task.id]" class="issue-key">{{ task.taskKey }}</a>
                    <a [routerLink]="['/tasks', task.id]" class="issue-title">{{ task.title }}</a>
                    @if (task.epicKey) {
                      <span class="epic-chip">{{ task.epicKey }}</span>
                    }
                  </div>
                  <div class="issue-right">
                    <span class="status-mini">{{ task.status }}</span>
                    @if (task.storyPoints != null) {
                      <span class="points-pill">{{ task.storyPoints }} pts</span>
                    }
                    <span class="assignee-mini">{{ task.assigneeName || 'Unassigned' }}</span>
                  </div>
                </div>
              } @empty {
                <div class="drop-empty">Backlog is empty. Create a new Epic, Story, Task, or Bug above.</div>
              }
            </div>
          </section>
        </div>
      }

      <!-- Create Sprint Modal -->
      @if (showCreateSprintModal()) {
        <div class="modal-backdrop" (click)="showCreateSprintModal.set(false)">
          <div class="modal-card" (click)="$event.stopPropagation()">
            <header class="modal-header">
              <h2>Create Sprint</h2>
              <button class="modal-close" (click)="showCreateSprintModal.set(false)">✕</button>
            </header>
            <form (ngSubmit)="onCreateSprint()" class="modal-form">
              <div class="form-group">
                <label for="sp-name">Sprint Name *</label>
                <input id="sp-name" type="text" [(ngModel)]="createSprintModel.name" name="spName" required placeholder="e.g. Sprint 3 — Search & Polish" />
              </div>
              <div class="form-group">
                <label for="sp-goal">Sprint Goal</label>
                <textarea id="sp-goal" rows="2" [(ngModel)]="createSprintModel.goal" name="spGoal"></textarea>
              </div>
              <footer class="modal-footer">
                <button type="button" class="btn-cancel" (click)="showCreateSprintModal.set(false)">Cancel</button>
                <button type="submit" class="primary-btn">Create Sprint</button>
              </footer>
            </form>
          </div>
        </div>
      }

      <!-- Start Sprint Modal -->
      @if (startingSprint(); as sp) {
        <div class="modal-backdrop" (click)="startingSprint.set(null)">
          <div class="modal-card" (click)="$event.stopPropagation()">
            <header class="modal-header">
              <h2>Start {{ sp.name }}</h2>
              <button class="modal-close" (click)="startingSprint.set(null)">✕</button>
            </header>
            <form (ngSubmit)="onConfirmStartSprint()" class="modal-form">
              <div class="form-row">
                <div class="form-group">
                  <label for="st-start">Start Date *</label>
                  <input id="st-start" type="date" [(ngModel)]="startSprintDates.start" name="stStart" required />
                </div>
                <div class="form-group">
                  <label for="st-end">End Date *</label>
                  <input id="st-end" type="date" [(ngModel)]="startSprintDates.end" name="stEnd" required />
                </div>
              </div>
              <footer class="modal-footer">
                <button type="button" class="btn-cancel" (click)="startingSprint.set(null)">Cancel</button>
                <button type="submit" class="primary-btn">Start Sprint</button>
              </footer>
            </form>
          </div>
        </div>
      }

      <!-- Complete Sprint Modal -->
      @if (completingSprint(); as sp) {
        <div class="modal-backdrop" (click)="completingSprint.set(null)">
          <div class="modal-card" (click)="$event.stopPropagation()">
            <header class="modal-header">
              <h2>Complete {{ sp.name }}</h2>
              <button class="modal-close" (click)="completingSprint.set(null)">✕</button>
            </header>
            <form (ngSubmit)="onConfirmCompleteSprint()" class="modal-form">
              <p class="modal-info">
                Any incomplete issues (not <strong>Done</strong> or <strong>Cancelled</strong>) will be rolled over:
              </p>
              <div class="form-group">
                <label for="roll-target">Move incomplete issues to</label>
                <select id="roll-target" [(ngModel)]="rolloverTargetSprintId" name="rollTarget">
                  <option [ngValue]="null">Product Backlog</option>
                  @for (planned of plannedSprints(); track planned.id) {
                    <option [value]="planned.id">{{ planned.name }}</option>
                  }
                </select>
              </div>
              <footer class="modal-footer">
                <button type="button" class="btn-cancel" (click)="completingSprint.set(null)">Cancel</button>
                <button type="submit" class="primary-btn">Complete Sprint</button>
              </footer>
            </form>
          </div>
        </div>
      }

      <!-- New Issue Modal -->
      @if (showNewIssueModal()) {
        <div class="modal-backdrop" (click)="showNewIssueModal.set(false)">
          <div class="modal-card" (click)="$event.stopPropagation()">
            <header class="modal-header">
              <h2>Create Issue</h2>
              <button class="modal-close" (click)="showNewIssueModal.set(false)">✕</button>
            </header>
            <form (ngSubmit)="onCreateIssue()" class="modal-form">
              <div class="form-row">
                <div class="form-group">
                  <label for="ni-type">Issue Type</label>
                  <select id="ni-type" [(ngModel)]="newIssueModel.issueType" name="niType">
                    @for (t of creatableIssueTypes; track t) {
                      <option [value]="t">{{ t }}</option>
                    }
                  </select>
                </div>
                <div class="form-group">
                  <label for="ni-prio">Priority</label>
                  <select id="ni-prio" [(ngModel)]="newIssueModel.priority" name="niPrio">
                    @for (p of taskPriorities; track p) {
                      <option [value]="p">{{ p }}</option>
                    }
                  </select>
                </div>
                <div class="form-group">
                  <label for="ni-pts">Story Points</label>
                  <input id="ni-pts" type="number" min="0" [(ngModel)]="newIssueModel.storyPoints" name="niPts" />
                </div>
              </div>
              <div class="form-group">
                <label for="ni-title">Summary *</label>
                <input id="ni-title" type="text" [(ngModel)]="newIssueModel.title" name="niTitle" required maxlength="200" />
              </div>
              <div class="form-row">
                <div class="form-group">
                  <label for="ni-epic">Epic</label>
                  <select id="ni-epic" [(ngModel)]="newIssueModel.epicId" name="niEpic" [disabled]="newIssueModel.issueType === 'Epic'">
                    <option [ngValue]="null">None</option>
                    @for (ep of epics(); track ep.id) {
                      <option [value]="ep.id">{{ ep.taskKey }} — {{ ep.title }}</option>
                    }
                  </select>
                </div>
                <div class="form-group">
                  <label for="ni-sprint">Sprint</label>
                  <select id="ni-sprint" [(ngModel)]="newIssueModel.sprintId" name="niSprint" [disabled]="newIssueModel.issueType === 'Epic'">
                    <option [ngValue]="null">Product Backlog</option>
                    @for (sp of openSprints(); track sp.id) {
                      <option [value]="sp.id">{{ sp.name }} ({{ sp.status }})</option>
                    }
                  </select>
                </div>
              </div>
              <footer class="modal-footer">
                <button type="button" class="btn-cancel" (click)="showNewIssueModal.set(false)">Cancel</button>
                <button type="submit" class="primary-btn">Create Issue</button>
              </footer>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .backlog-page { display: flex; flex-direction: column; gap: 1.25rem; }
    .breadcrumb { display: flex; gap: 0.5rem; font-size: 0.85rem; color: #64748b; }
    .breadcrumb a { color: #2563eb; text-decoration: none; font-weight: 500; }
    .page-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; }
    .page-header h1 { margin: 0; font-size: 1.6rem; color: #0f172a; }
    .subtitle { margin: 0.2rem 0 0; color: #64748b; font-size: 0.9rem; }
    .header-actions { display: flex; gap: 0.6rem; }
    .primary-btn { background: #2563eb; color: #fff; border: none; padding: 0.5rem 1rem; border-radius: 6px; font-weight: 600; cursor: pointer; }
    .btn-secondary { background: #fff; color: #1e293b; border: 1px solid #cbd5e1; padding: 0.5rem 1rem; border-radius: 6px; font-weight: 600; cursor: pointer; }
    .btn-complete { background: #0f172a; color: #fff; border: none; padding: 0.35rem 0.75rem; border-radius: 6px; font-weight: 600; cursor: pointer; }
    .btn-sm { padding: 0.35rem 0.75rem; font-size: 0.8rem; }
    .charts-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1rem; }
    .panel-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 1rem 1.25rem; }
    .panel-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem; gap: 0.5rem; }
    .panel-head h2 { margin: 0; font-size: 1rem; color: #0f172a; }
    .chart-meta { display: flex; gap: 1.25rem; font-size: 0.8rem; color: #475569; margin-bottom: 0.5rem; }
    .burndown-svg { width: 100%; height: 150px; background: #f8fafc; border-radius: 6px; }
    .chart-legend { display: flex; gap: 1rem; font-size: 0.75rem; color: #64748b; margin-top: 0.4rem; }
    .chart-legend i { display: inline-block; width: 10px; height: 10px; border-radius: 2px; margin-right: 4px; }
    .dot-ideal { background: #94a3b8; }
    .dot-actual { background: #2563eb; }
    .dot-committed { background: #cbd5e1; }
    .dot-completed { background: #10b981; }
    .vel-avg { font-size: 0.82rem; color: #166534; background: #dcfce7; padding: 2px 8px; border-radius: 10px; }
    .velocity-bars { display: flex; align-items: flex-end; gap: 1rem; height: 150px; padding: 0.5rem; background: #f8fafc; border-radius: 6px; }
    .vel-col { flex: 1; display: flex; flex-direction: column; align-items: center; height: 100%; justify-content: flex-end; gap: 0.3rem; }
    .vel-bar-pair { display: flex; align-items: flex-end; gap: 4px; height: 115px; width: 100%; justify-content: center; }
    .vel-bar { width: 18px; border-radius: 3px 3px 0 0; display: flex; align-items: flex-start; justify-content: center; font-size: 0.65rem; font-weight: 700; min-height: 14px; }
    .vel-bar.committed { background: #cbd5e1; color: #334155; }
    .vel-bar.completed { background: #10b981; color: #fff; }
    .vel-label { font-size: 0.7rem; color: #475569; max-width: 80px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .epic-pills { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .epic-card-pill { display: inline-flex; align-items: center; gap: 0.45rem; padding: 0.4rem 0.7rem; border: 1px solid #e9d5ff; background: #faf5ff; border-radius: 6px; text-decoration: none; color: #0f172a; font-size: 0.82rem; }
    .planner-stack { display: flex; flex-direction: column; gap: 1rem; }
    .sprint-active { border-color: #93c5fd; box-shadow: 0 2px 8px rgba(37, 99, 235, 0.08); }
    .sprint-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.75rem; margin-bottom: 0.75rem; }
    .sprint-title-row { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .sprint-title-row h3 { margin: 0; font-size: 1.05rem; color: #0f172a; }
    .sprint-goal { font-size: 0.85rem; color: #64748b; }
    .sprint-badge { font-size: 0.7rem; font-weight: 700; padding: 2px 8px; border-radius: 10px; text-transform: uppercase; }
    .sp-active { background: #dbeafe; color: #1d4ed8; }
    .sp-planned { background: #f1f5f9; color: #475569; }
    .sprint-controls { display: flex; align-items: center; gap: 0.75rem; }
    .pts-summary { font-size: 0.8rem; color: #64748b; font-weight: 600; }
    .issue-drop-list { display: flex; flex-direction: column; gap: 0.45rem; min-height: 48px; }
    .issue-row { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; padding: 0.6rem 0.8rem; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; cursor: grab; flex-wrap: wrap; }
    .issue-row:hover { border-color: #cbd5e1; background: #fff; }
    .issue-left, .issue-right { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .issue-key { font-family: monospace; font-size: 0.78rem; font-weight: 700; color: #475569; text-decoration: none; }
    .issue-title { font-weight: 600; color: #0f172a; text-decoration: none; font-size: 0.9rem; }
    .issue-title:hover { color: #2563eb; text-decoration: underline; }
    .status-mini { font-size: 0.72rem; padding: 1px 6px; border-radius: 4px; background: #e2e8f0; color: #334155; font-weight: 600; }
    .assignee-mini { font-size: 0.78rem; color: #64748b; }
    .drop-empty, .empty-hint { padding: 1rem; text-align: center; color: #94a3b8; font-size: 0.85rem; border: 1px dashed #cbd5e1; border-radius: 6px; }
    .alert { padding: 0.75rem 1rem; border-radius: 6px; display: flex; justify-content: space-between; align-items: center; font-size: 0.9rem; }
    .alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
    .alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
    .alert-close { background: none; border: none; cursor: pointer; }
    .modal-backdrop { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.6); display: flex; align-items: center; justify-content: center; z-index: 1000; padding: 1rem; }
    .modal-card { background: #fff; border-radius: 10px; width: 100%; max-width: 520px; box-shadow: 0 10px 25px rgba(0,0,0,0.2); }
    .modal-header { padding: 1rem 1.25rem; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; }
    .modal-header h2 { margin: 0; font-size: 1.15rem; }
    .modal-close { background: none; border: none; font-size: 1.1rem; cursor: pointer; }
    .modal-form { padding: 1.25rem; display: flex; flex-direction: column; gap: 0.9rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.3rem; flex: 1; }
    .form-row { display: flex; gap: 0.75rem; }
    .form-group label { font-size: 0.8rem; font-weight: 600; color: #475569; }
    .form-group input, .form-group select, .form-group textarea { padding: 0.45rem 0.65rem; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 0.88rem; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 0.6rem; margin-top: 0.4rem; }
    .btn-cancel { background: #f1f5f9; border: 1px solid #cbd5e1; padding: 0.45rem 0.9rem; border-radius: 6px; cursor: pointer; }
  `]
})
export class SprintBacklogComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly projectService = inject(ProjectService);
  private readonly sprintService = inject(SprintService);
  private readonly taskService = inject(TaskService);

  readonly creatableIssueTypes: IssueType[] = ISSUE_TYPES.filter(t => t !== 'Subtask');
  readonly taskPriorities = TASK_PRIORITIES;

  projectId = signal<string>('');
  project = signal<ProjectDetail | null>(null);
  sprints = signal<Sprint[]>([]);
  tasks = signal<TaskSummary[]>([]);
  burndown = signal<SprintBurndown | null>(null);
  velocity = signal<VelocityChart | null>(null);
  selectedBurndownSprintId = signal<string>('');

  isLoading = signal<boolean>(true);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  openSprints = computed(() => this.sprints().filter(s => s.status !== 'Closed'));
  plannedSprints = computed(() => this.sprints().filter(s => s.status === 'Planned'));
  epics = computed(() => this.tasks().filter(t => t.issueType === 'Epic'));
  backlogTasks = computed(() => this.tasks().filter(t => !t.sprintId && t.issueType !== 'Epic'));
  backlogPoints = computed(() => this.backlogTasks().reduce((acc, t) => acc + (t.storyPoints || 0), 0));

  showCreateSprintModal = signal<boolean>(false);
  createSprintModel: CreateSprintRequest = { name: '', goal: '' };

  startingSprint = signal<Sprint | null>(null);
  startSprintDates = { start: '', end: '' };

  completingSprint = signal<Sprint | null>(null);
  rolloverTargetSprintId: string | null = null;

  showNewIssueModal = signal<boolean>(false);
  newIssueModel: {
    title: string;
    issueType: IssueType;
    priority: TaskPriority;
    storyPoints: number | null;
    epicId: string | null;
    sprintId: string | null;
  } = {
    title: '',
    issueType: 'Story',
    priority: 'Normal',
    storyPoints: 3,
    epicId: null,
    sprintId: null
  };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.projectId.set(id);
      this.loadAll(id);
    }
  }

  loadAll(projectId: string): void {
    this.isLoading.set(true);
    this.projectService.getById(projectId).subscribe({
      next: (p) => this.project.set(p),
      error: () => {}
    });

    this.sprintService.listProjectSprints(projectId).subscribe({
      next: (sprints) => {
        this.sprints.set(sprints);
        const preferred = sprints.find(s => s.status === 'Active') ?? sprints[0];
        if (preferred) {
          this.selectedBurndownSprintId.set(preferred.id);
          this.loadBurndown(preferred.id);
        }
      }
    });

    this.sprintService.getVelocity(projectId).subscribe({
      next: (v) => this.velocity.set(v),
      error: () => {}
    });

    this.taskService.listProjectTasks(projectId, { page: 1, pageSize: 100 }).subscribe({
      next: (res) => {
        this.tasks.set(res.items);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Failed to load backlog.');
        this.isLoading.set(false);
      }
    });
  }

  loadBurndown(sprintId: string): void {
    this.selectedBurndownSprintId.set(sprintId);
    this.sprintService.getBurndown(sprintId).subscribe({
      next: (bd) => this.burndown.set(bd),
      error: () => this.burndown.set(null)
    });
  }

  getSprintTasks(sprintId: string): TaskSummary[] {
    return this.tasks().filter(t => t.sprintId === sprintId);
  }

  getSprintPoints(sprintId: string): number {
    return this.getSprintTasks(sprintId).reduce((acc, t) => acc + (t.storyPoints || 0), 0);
  }

  onDropToContainer(event: CdkDragDrop<any>, targetSprintId: string | null): void {
    const task = event.item.data as TaskSummary;
    if (!task) return;
    if ((task.sprintId ?? null) === targetSprintId && event.previousIndex === event.currentIndex) return;

    const newRank = (event.currentIndex + 1) * 100;
    this.taskService.assignSprint(task.id, { sprintId: targetSprintId, boardRank: newRank }).subscribe({
      next: (updated) => {
        this.tasks.update(list => list.map(t => t.id === updated.id ? updated : t));
        this.successMessage.set(`Moved ${updated.taskKey} to ${targetSprintId ? 'sprint' : 'Product Backlog'}.`);
        if (this.selectedBurndownSprintId()) {
          this.loadBurndown(this.selectedBurndownSprintId());
        }
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Failed to move issue.');
      }
    });
  }

  openCreateSprintModal(): void {
    this.createSprintModel = { name: '', goal: '' };
    this.showCreateSprintModal.set(true);
  }

  onCreateSprint(): void {
    if (!this.createSprintModel.name.trim()) return;
    this.sprintService.createSprint(this.projectId(), this.createSprintModel).subscribe({
      next: () => {
        this.showCreateSprintModal.set(false);
        this.successMessage.set('Sprint created.');
        this.loadAll(this.projectId());
      },
      error: (err) => this.errorMessage.set(err.error?.detail || 'Failed to create sprint.')
    });
  }

  openStartSprintModal(sprint: Sprint): void {
    const today = new Date();
    const twoWeeks = new Date(today.getTime() + 14 * 86400000);
    this.startSprintDates = {
      start: today.toISOString().split('T')[0],
      end: twoWeeks.toISOString().split('T')[0]
    };
    this.startingSprint.set(sprint);
  }

  onConfirmStartSprint(): void {
    const sp = this.startingSprint();
    if (!sp) return;
    this.sprintService.startSprint(sp.id, {
      startDateUtc: new Date(this.startSprintDates.start).toISOString(),
      endDateUtc: new Date(this.startSprintDates.end).toISOString()
    }).subscribe({
      next: () => {
        this.startingSprint.set(null);
        this.successMessage.set(`${sp.name} started.`);
        this.loadAll(this.projectId());
      },
      error: (err) => {
        this.startingSprint.set(null);
        this.errorMessage.set(err.error?.detail || 'Failed to start sprint.');
      }
    });
  }

  openCompleteSprintModal(sprint: Sprint): void {
    this.rolloverTargetSprintId = null;
    this.completingSprint.set(sprint);
  }

  onConfirmCompleteSprint(): void {
    const sp = this.completingSprint();
    if (!sp) return;
    this.sprintService.completeSprint(sp.id, {
      moveIncompleteToSprintId: this.rolloverTargetSprintId
    }).subscribe({
      next: (res) => {
        this.completingSprint.set(null);
        this.successMessage.set(`Completed ${sp.name}: ${res.completedTasksCount} done, ${res.rolledOverTasksCount} rolled over.`);
        this.loadAll(this.projectId());
      },
      error: (err) => {
        this.completingSprint.set(null);
        this.errorMessage.set(err.error?.detail || 'Failed to complete sprint.');
      }
    });
  }

  openNewIssueModal(): void {
    this.newIssueModel = {
      title: '',
      issueType: 'Story',
      priority: 'Normal',
      storyPoints: 3,
      epicId: null,
      sprintId: null
    };
    this.showNewIssueModal.set(true);
  }

  onCreateIssue(): void {
    if (!this.newIssueModel.title.trim()) return;
    this.taskService.create(this.projectId(), {
      title: this.newIssueModel.title.trim(),
      priority: this.newIssueModel.priority,
      issueType: this.newIssueModel.issueType,
      storyPoints: this.newIssueModel.storyPoints,
      epicId: this.newIssueModel.issueType === 'Epic' ? null : this.newIssueModel.epicId,
      sprintId: this.newIssueModel.issueType === 'Epic' ? null : this.newIssueModel.sprintId
    }).subscribe({
      next: (created) => {
        this.showNewIssueModal.set(false);
        this.tasks.update(list => [created, ...list]);
        this.successMessage.set(`Created ${created.taskKey}.`);
      },
      error: (err) => this.errorMessage.set(err.error?.detail || 'Failed to create issue.')
    });
  }

  getIssueIcon(type?: string): string {
    switch (type) {
      case 'Epic': return '⚡';
      case 'Story': return '📘';
      case 'Bug': return '🐞';
      case 'Subtask': return '↳';
      default: return '☑️';
    }
  }

  idealPolyline(): string {
    const bd = this.burndown();
    if (!bd || bd.points.length === 0) return '';
    return this.buildSvgPolyline(bd.points.map(p => p.idealRemainingPoints), Math.max(1, bd.totalPoints));
  }

  actualPolyline(): string {
    const bd = this.burndown();
    if (!bd || bd.points.length === 0) return '';
    return this.buildSvgPolyline(bd.points.map(p => p.actualRemainingPoints), Math.max(1, bd.totalPoints));
  }

  private buildSvgPolyline(values: number[], maxVal: number): string {
    const n = values.length;
    if (n === 0) return '';
    const left = 32;
    const width = 370;
    const top = 18;
    const height = 115;
    return values.map((v, i) => {
      const x = n === 1 ? left : left + (i / (n - 1)) * width;
      const y = top + height - (Math.min(maxVal, Math.max(0, v)) / maxVal) * height;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    }).join(' ');
  }

  getBarHeight(points: number): number {
    const vel = this.velocity();
    if (!vel || vel.sprints.length === 0) return 10;
    const maxPts = Math.max(1, ...vel.sprints.map(s => Math.max(s.committedPoints, s.completedPoints)));
    return Math.max(12, Math.round((points / maxPts) * 100));
  }
}
