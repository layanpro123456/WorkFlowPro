import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProjectService } from '../../core/projects/project.service';
import { PagedResult, ProjectSummary } from '../../core/projects/project.models';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="projects-page">
      <div class="page-header">
        <div>
          <h2>Projects</h2>
          <p class="subtitle">Manage workspaces, track timelines and collaborate with project teams.</p>
        </div>

        @if (canCreateProject()) {
          <button class="primary-btn" (click)="openCreateModal()">+ New Project</button>
        }
      </div>

      <div class="filter-bar">
        <div class="search-box">
          <input
            type="text"
            placeholder="Search projects by name or code..."
            [formControl]="searchControl"
            (keyup.enter)="loadProjects()"
          />
        </div>

        <div class="status-filters">
          @for (s of statuses; track s) {
            <button
              class="filter-pill"
              [class.active]="selectedStatus() === s"
              (click)="selectStatus(s)"
            >
              {{ s }}
            </button>
          }
        </div>
      </div>

      @if (isLoading()) {
        <div class="loading-box">
          <span class="spinner"></span> Loading projects...
        </div>
      } @else if (errorMessage()) {
        <div class="error-banner">{{ errorMessage() }}</div>
      } @else if (projects().length === 0) {
        <div class="empty-state">
          <div class="empty-icon">📁</div>
          <h3>No projects found</h3>
          <p>Try adjusting your search filters or create a new project.</p>
        </div>
      } @else {
        <div class="table-container">
          <table class="data-table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Project Name</th>
                <th>Status</th>
                <th>Timeline</th>
                <th>Members</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (project of projects(); track project.id) {
                <tr>
                  <td><span class="code-badge">{{ project.projectCode }}</span></td>
                  <td class="name-col">
                    <strong>{{ project.name }}</strong>
                  </td>
                  <td>
                    <span class="status-badge" [ngClass]="project.status.toLowerCase()">
                      {{ project.status }}
                    </span>
                  </td>
                  <td class="timeline-col">
                    @if (project.startDate || project.targetDate) {
                      {{ project.startDate || '—' }} to {{ project.targetDate || '—' }}
                    } @else {
                      <span class="text-muted">Not specified</span>
                    }
                  </td>
                  <td>
                    <span class="member-pill">👥 {{ project.memberCount }}</span>
                  </td>
                  <td class="text-muted">
                    {{ project.createdAtUtc | date:'mediumDate' }}
                  </td>
                  <td>
                    <a [routerLink]="['/projects', project.id]" class="view-link">View Details →</a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="pagination-bar">
          <span class="page-info">
            Showing {{ (page() - 1) * pageSize() + 1 }} to
            {{ Math.min(page() * pageSize(), totalCount()) }} of {{ totalCount() }} projects
          </span>

          <div class="page-buttons">
            <button
              class="page-btn"
              [disabled]="page() <= 1"
              (click)="goToPage(page() - 1)"
            >
              Previous
            </button>
            <span class="current-page">Page {{ page() }} of {{ totalPages() || 1 }}</span>
            <button
              class="page-btn"
              [disabled]="page() >= totalPages()"
              (click)="goToPage(page() + 1)"
            >
              Next
            </button>
          </div>
        </div>
      }

      <!-- Create Project Modal -->
      @if (showCreateModal()) {
        <div class="modal-overlay" (click)="closeCreateModal()">
          <div class="modal-card" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h3>Create New Project</h3>
              <button class="close-btn" (click)="closeCreateModal()">✕</button>
            </div>

            @if (modalError()) {
              <div class="modal-error">{{ modalError() }}</div>
            }

            <form [formGroup]="createForm" (ngSubmit)="submitCreate()">
              <div class="form-row">
                <div class="form-group flex-1">
                  <label for="pCode">Project Code *</label>
                  <input
                    id="pCode"
                    type="text"
                    formControlName="projectCode"
                    placeholder="e.g. WFP-OPS"
                  />
                  @if (createForm.get('projectCode')?.invalid && createForm.get('projectCode')?.touched) {
                    <span class="field-error">Alphanumeric code, max 30 characters.</span>
                  }
                </div>

                <div class="form-group flex-2">
                  <label for="pName">Project Name *</label>
                  <input
                    id="pName"
                    type="text"
                    formControlName="name"
                    placeholder="Project name"
                  />
                </div>
              </div>

              <div class="form-group">
                <label for="pDesc">Description</label>
                <textarea
                  id="pDesc"
                  formControlName="description"
                  rows="3"
                  placeholder="Summary of project goals and scope..."
                ></textarea>
              </div>

              <div class="form-row">
                <div class="form-group flex-1">
                  <label for="pStart">Start Date</label>
                  <input id="pStart" type="date" formControlName="startDate" />
                </div>
                <div class="form-group flex-1">
                  <label for="pTarget">Target Date</label>
                  <input id="pTarget" type="date" formControlName="targetDate" />
                </div>
              </div>

              <div class="modal-actions">
                <button type="button" class="btn-cancel" (click)="closeCreateModal()">Cancel</button>
                <button
                  type="submit"
                  class="primary-btn"
                  [disabled]="createForm.invalid || isSubmittingModal()"
                >
                  @if (isSubmittingModal()) { Creating... } @else { Create Project }
                </button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .projects-page { display: flex; flex-direction: column; gap: 1.5rem; }
    .page-header { display: flex; justify-content: space-between; align-items: center; }
    h2 { margin: 0; font-size: 1.6rem; color: #0f172a; }
    .subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.9rem; }
    .primary-btn {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.65rem 1.25rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
      font-size: 0.9rem;
    }
    .primary-btn:hover:not(:disabled) { background: #1d4ed8; }
    .primary-btn:disabled { opacity: 0.6; cursor: not-allowed; }
    .filter-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      background: #ffffff;
      padding: 1rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    .search-box input {
      padding: 0.5rem 0.85rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      min-width: 300px;
      font-size: 0.9rem;
      outline: none;
    }
    .search-box input:focus { border-color: #2563eb; }
    .status-filters { display: flex; gap: 0.4rem; }
    .filter-pill {
      background: #f1f5f9;
      border: 1px solid #e2e8f0;
      color: #475569;
      padding: 0.4rem 0.85rem;
      border-radius: 20px;
      font-size: 0.8rem;
      font-weight: 500;
      cursor: pointer;
    }
    .filter-pill.active {
      background: #2563eb;
      border-color: #2563eb;
      color: white;
    }
    .table-container {
      background: #ffffff;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      overflow-x: auto;
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
      font-size: 0.9rem;
    }
    .data-table th {
      background: #f8fafc;
      padding: 0.85rem 1rem;
      color: #475569;
      font-weight: 600;
      border-bottom: 1px solid #e2e8f0;
    }
    .data-table td {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      vertical-align: middle;
    }
    .code-badge {
      background: #e2e8f0;
      color: #1e293b;
      padding: 2px 6px;
      border-radius: 4px;
      font-family: monospace;
      font-weight: 600;
      font-size: 0.8rem;
    }
    .status-badge {
      font-size: 0.75rem;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: 12px;
      text-transform: capitalize;
    }
    .status-badge.active { background: #dcfce7; color: #15803d; }
    .status-badge.onhold { background: #fef9c3; color: #a16207; }
    .status-badge.completed { background: #e0f2fe; color: #0369a1; }
    .status-badge.archived { background: #f1f5f9; color: #64748b; }
    .member-pill { font-size: 0.8rem; color: #475569; }
    .text-muted { color: #94a3b8; font-size: 0.85rem; }
    .view-link {
      color: #2563eb;
      text-decoration: none;
      font-weight: 600;
      font-size: 0.85rem;
    }
    .view-link:hover { text-decoration: underline; }
    .pagination-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.5rem 0;
    }
    .page-info { color: #64748b; font-size: 0.85rem; }
    .page-buttons { display: flex; align-items: center; gap: 0.75rem; }
    .page-btn {
      padding: 0.4rem 0.85rem;
      border: 1px solid #cbd5e1;
      background: #ffffff;
      border-radius: 6px;
      font-size: 0.85rem;
      cursor: pointer;
    }
    .page-btn:hover:not(:disabled) { background: #f8fafc; }
    .page-btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .current-page { font-size: 0.85rem; color: #334155; }
    .empty-state, .loading-box {
      text-align: center;
      padding: 3rem;
      background: #ffffff;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    .empty-icon { font-size: 3rem; margin-bottom: 0.5rem; }
    .modal-overlay {
      position: fixed;
      top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(0,0,0,0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
    }
    .modal-card {
      background: white;
      border-radius: 10px;
      width: 100%;
      max-width: 520px;
      padding: 2rem;
      box-shadow: 0 10px 25px rgba(0,0,0,0.2);
    }
    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }
    .close-btn {
      background: none;
      border: none;
      font-size: 1.25rem;
      cursor: pointer;
      color: #94a3b8;
    }
    .form-row { display: flex; gap: 1rem; margin-bottom: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 1rem; }
    .flex-1 { flex: 1; }
    .flex-2 { flex: 2; }
    label { font-size: 0.85rem; font-weight: 600; color: #334155; }
    input, textarea {
      padding: 0.6rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
      outline: none;
    }
    input:focus, textarea:focus { border-color: #2563eb; }
    .field-error { color: #dc2626; font-size: 0.75rem; }
    .modal-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 1.5rem;
    }
    .btn-cancel {
      background: transparent;
      border: 1px solid #cbd5e1;
      padding: 0.65rem 1.25rem;
      border-radius: 6px;
      cursor: pointer;
    }
    .modal-error {
      background: #fef2f2;
      color: #b91c1c;
      padding: 0.65rem 1rem;
      border-radius: 6px;
      font-size: 0.85rem;
      margin-bottom: 1rem;
    }
  `]
})
export class ProjectListComponent implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  readonly Math = Math;

  readonly statuses = ['All', 'Active', 'OnHold', 'Completed', 'Archived'];
  readonly selectedStatus = signal<string>('All');
  readonly searchControl = this.fb.control('');

  readonly projects = signal<ProjectSummary[]>([]);
  readonly page = signal<number>(1);
  readonly pageSize = signal<number>(10);
  readonly totalCount = signal<number>(0);
  readonly totalPages = signal<number>(1);
  readonly isLoading = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);

  readonly showCreateModal = signal<boolean>(false);
  readonly isSubmittingModal = signal<boolean>(false);
  readonly modalError = signal<string | null>(null);

  readonly createForm = this.fb.group({
    projectCode: ['', [Validators.required, Validators.maxLength(30)]],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: [''],
    startDate: [''],
    targetDate: ['']
  });

  canCreateProject(): boolean {
    return this.authService.hasAnyRole(['Administrator', 'Manager']);
  }

  ngOnInit(): void {
    this.loadProjects();
  }

  loadProjects(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.projectService
      .list(this.selectedStatus(), this.searchControl.value ?? '', this.page(), this.pageSize())
      .subscribe({
        next: (result: PagedResult<ProjectSummary>) => {
          this.projects.set(result.items);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMessage.set(err.error?.detail || 'Failed to load projects.');
          this.isLoading.set(false);
        }
      });
  }

  selectStatus(status: string): void {
    this.selectedStatus.set(status);
    this.page.set(1);
    this.loadProjects();
  }

  goToPage(newPage: number): void {
    this.page.set(newPage);
    this.loadProjects();
  }

  openCreateModal(): void {
    this.createForm.reset();
    this.modalError.set(null);
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  submitCreate(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    this.isSubmittingModal.set(true);
    this.modalError.set(null);

    const val = this.createForm.getRawValue();
    this.projectService.create({
      projectCode: val.projectCode!,
      name: val.name!,
      description: val.description || undefined,
      startDate: val.startDate || undefined,
      targetDate: val.targetDate || undefined
    }).subscribe({
      next: () => {
        this.isSubmittingModal.set(false);
        this.closeCreateModal();
        this.loadProjects();
      },
      error: (err) => {
        this.isSubmittingModal.set(false);
        this.modalError.set(err.error?.detail || err.error?.title || 'Failed to create project.');
      }
    });
  }
}
