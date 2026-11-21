import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { AdminService } from '../../core/admin/admin.service';
import {
  AdminUserSummary,
  AuditLog,
  AuditLogFilterParams,
  CreateUserRequest
} from '../../core/admin/admin.models';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="admin-page">
      <header class="page-header">
        <div>
          <h1>Administration & Security Control</h1>
          <p class="page-subtitle">User account provisioning, RBAC role management, and immutable audit logs.</p>
        </div>
      </header>

      @if (!authService.hasRole('Administrator')) {
        <div class="forbidden-card">
          <h2>🚫 Access Restricted</h2>
          <p>You must have the <strong>Administrator</strong> role to access this module.</p>
        </div>
      } @else {
        <!-- Navigation Tabs -->
        <nav class="admin-tabs">
          <button
            type="button"
            class="tab-btn"
            [class.active]="activeTab() === 'users'"
            (click)="setTab('users')">
            👥 User Management
          </button>
          <button
            type="button"
            class="tab-btn"
            [class.active]="activeTab() === 'audit'"
            (click)="setTab('audit')">
            📜 Audit Log Explorer
          </button>
        </nav>

        <!-- Notification Banner -->
        @if (feedbackMessage()) {
          <div class="feedback-banner" [class.error]="isFeedbackError()">
            {{ feedbackMessage() }}
            <button type="button" class="close-btn" (click)="feedbackMessage.set(null)">✕</button>
          </div>
        }

        <!-- USERS TAB -->
        @if (activeTab() === 'users') {
          <section class="admin-section">
            <div class="toolbar-card">
              <div class="filters-row">
                <input
                  type="text"
                  placeholder="Search name or email..."
                  [(ngModel)]="userSearch"
                  (keyup.enter)="loadUsers(1)"
                  class="search-input" />

                <select [(ngModel)]="userRoleFilter" (change)="loadUsers(1)">
                  <option value="All">All Roles</option>
                  @for (role of availableRoles(); track role) {
                    <option [value]="role">{{ role }}</option>
                  }
                </select>

                <select [(ngModel)]="userStatusFilter" (change)="loadUsers(1)">
                  <option value="All">All Statuses</option>
                  <option value="true">Active Only</option>
                  <option value="false">Inactive Only</option>
                </select>

                <button type="button" class="btn btn-secondary" (click)="loadUsers(1)">Search</button>
                <button type="button" class="btn btn-secondary" (click)="resetUserFilters()">Reset</button>

                <button type="button" class="btn btn-primary add-btn" (click)="openCreateModal()">
                  ➕ Create User
                </button>
              </div>
            </div>

            @if (usersLoading()) {
              <div class="loading-state">
                <div class="spinner"></div>
                <p>Loading users...</p>
              </div>
            } @else {
              <div class="table-container">
                <table class="data-table">
                  <thead>
                    <tr>
                      <th>Display Name</th>
                      <th>Email</th>
                      <th>Assigned Roles</th>
                      <th>Status</th>
                      <th>Created</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (user of users(); track user.id) {
                      <tr>
                        <td><strong>{{ user.displayName }}</strong></td>
                        <td>{{ user.email }}</td>
                        <td>
                          <div class="roles-cluster">
                            @for (role of user.roles; track role) {
                              <span class="role-pill" [attr.data-role]="role">{{ role }}</span>
                            }
                          </div>
                        </td>
                        <td>
                          <span class="status-badge" [class.active]="user.isActive" [class.inactive]="!user.isActive">
                            {{ user.isActive ? 'Active' : 'Inactive' }}
                          </span>
                        </td>
                        <td>{{ user.createdAtUtc | date:'shortDate' }}</td>
                        <td>
                          <div class="actions-cluster">
                            <button
                              type="button"
                              class="btn btn-sm btn-secondary"
                              (click)="openRolesModal(user)">
                              Edit Roles
                            </button>
                            <button
                              type="button"
                              class="btn btn-sm"
                              [class.btn-danger]="user.isActive"
                              [class.btn-success]="!user.isActive"
                              (click)="toggleUserStatus(user)">
                              {{ user.isActive ? 'Deactivate' : 'Activate' }}
                            </button>
                          </div>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="6" class="empty-cell">No users found matching the search criteria.</td>
                      </tr>
                    }
                  </tbody>
                </table>

                @if (userTotalPages() > 1) {
                  <div class="pagination-bar">
                    <button
                      class="btn btn-sm btn-secondary"
                      [disabled]="userCurrentPage() <= 1"
                      (click)="loadUsers(userCurrentPage() - 1)">
                      Previous
                    </button>
                    <span class="page-indicator">
                      Page {{ userCurrentPage() }} of {{ userTotalPages() }} ({{ userTotalCount() }} users)
                    </span>
                    <button
                      class="btn btn-sm btn-secondary"
                      [disabled]="userCurrentPage() >= userTotalPages()"
                      (click)="loadUsers(userCurrentPage() + 1)">
                      Next
                    </button>
                  </div>
                }
              </div>
            }
          </section>
        }

        <!-- AUDIT LOG TAB -->
        @if (activeTab() === 'audit') {
          <section class="admin-section">
            <div class="toolbar-card">
              <div class="filters-row">
                <select [(ngModel)]="auditActionFilter" (change)="loadAuditLogs(1)">
                  <option value="All">All Actions</option>
                  <option value="UserCreated">UserCreated</option>
                  <option value="UserActivated">UserActivated</option>
                  <option value="UserDeactivated">UserDeactivated</option>
                  <option value="UserRolesUpdated">UserRolesUpdated</option>
                  <option value="LoginSucceeded">LoginSucceeded</option>
                  <option value="LoginFailed">LoginFailed</option>
                  <option value="ProjectCreated">ProjectCreated</option>
                  <option value="TaskCreated">TaskCreated</option>
                  <option value="TicketCreated">TicketCreated</option>
                  <option value="ApprovalDecided">ApprovalDecided</option>
                </select>

                <select [(ngModel)]="auditEntityTypeFilter" (change)="loadAuditLogs(1)">
                  <option value="All">All Entity Types</option>
                  <option value="User">User</option>
                  <option value="Project">Project</option>
                  <option value="Task">Task</option>
                  <option value="SupportTicket">SupportTicket</option>
                  <option value="ApprovalRequest">ApprovalRequest</option>
                </select>

                <button type="button" class="btn btn-secondary" (click)="loadAuditLogs(1)">Filter</button>
                <button type="button" class="btn btn-secondary" (click)="resetAuditFilters()">Reset</button>
              </div>
            </div>

            @if (auditLoading()) {
              <div class="loading-state">
                <div class="spinner"></div>
                <p>Loading audit records...</p>
              </div>
            } @else {
              <div class="table-container">
                <table class="data-table">
                  <thead>
                    <tr>
                      <th>Timestamp (UTC)</th>
                      <th>Action</th>
                      <th>Entity</th>
                      <th>Actor</th>
                      <th>Correlation ID</th>
                      <th>Metadata</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (log of auditLogs(); track log.id) {
                      <tr>
                        <td class="nowrap">{{ log.occurredAtUtc | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                        <td><span class="action-badge">{{ log.action }}</span></td>
                        <td>
                          <span class="entity-pill">{{ log.entityType }}</span>
                          @if (log.entityId) {
                            <small class="entity-id">{{ log.entityId.substring(0, 8) }}...</small>
                          }
                        </td>
                        <td>
                          @if (log.actorDisplayName) {
                            <strong>{{ log.actorDisplayName }}</strong>
                            <div class="text-muted">{{ log.actorEmail }}</div>
                          } @else {
                            <span class="text-muted">System</span>
                          }
                        </td>
                        <td><code class="corr-code">{{ log.correlationId || '—' }}</code></td>
                        <td>
                          @if (log.metadataJson) {
                            <code class="meta-json" [title]="log.metadataJson">{{ log.metadataJson }}</code>
                          } @else {
                            <span class="text-muted">—</span>
                          }
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="6" class="empty-cell">No audit log records found.</td>
                      </tr>
                    }
                  </tbody>
                </table>

                @if (auditTotalPages() > 1) {
                  <div class="pagination-bar">
                    <button
                      class="btn btn-sm btn-secondary"
                      [disabled]="auditCurrentPage() <= 1"
                      (click)="loadAuditLogs(auditCurrentPage() - 1)">
                      Previous
                    </button>
                    <span class="page-indicator">
                      Page {{ auditCurrentPage() }} of {{ auditTotalPages() }} ({{ auditTotalCount() }} events)
                    </span>
                    <button
                      class="btn btn-sm btn-secondary"
                      [disabled]="auditCurrentPage() >= auditTotalPages()"
                      (click)="loadAuditLogs(auditCurrentPage() + 1)">
                      Next
                    </button>
                  </div>
                }
              </div>
            }
          </section>
        }

        <!-- CREATE USER MODAL -->
        @if (showCreateModal()) {
          <div class="modal-backdrop">
            <div class="modal-card">
              <header class="modal-header">
                <h3>Provision New User</h3>
                <button type="button" class="close-btn" (click)="showCreateModal.set(false)">✕</button>
              </header>

              <form (ngSubmit)="onCreateUser()" class="modal-body">
                <div class="form-group">
                  <label for="newUserEmail">Email Address *</label>
                  <input
                    id="newUserEmail"
                    type="email"
                    [(ngModel)]="createModel.email"
                    name="email"
                    required
                    placeholder="name@company.com" />
                </div>

                <div class="form-group">
                  <label for="newUserDisplayName">Display Name *</label>
                  <input
                    id="newUserDisplayName"
                    type="text"
                    [(ngModel)]="createModel.displayName"
                    name="displayName"
                    required
                    placeholder="Full Name" />
                </div>

                <div class="form-group">
                  <label for="newUserPassword">Initial Password</label>
                  <input
                    id="newUserPassword"
                    type="text"
                    [(ngModel)]="createModel.password"
                    name="password"
                    placeholder="Leave blank for Pass1234!" />
                </div>

                <div class="form-group">
                  <label>Assign Roles</label>
                  <div class="checkbox-group">
                    @for (role of availableRoles(); track role) {
                      <label class="checkbox-label">
                        <input
                          type="checkbox"
                          [checked]="isRoleSelected(role)"
                          (change)="toggleRoleSelection(role)" />
                        {{ role }}
                      </label>
                    }
                  </div>
                </div>

                <div class="modal-footer">
                  <button type="button" class="btn btn-secondary" (click)="showCreateModal.set(false)">Cancel</button>
                  <button type="submit" class="btn btn-primary" [disabled]="submitting()">
                    {{ submitting() ? 'Creating...' : 'Create Account' }}
                  </button>
                </div>
              </form>
            </div>
          </div>
        }

        <!-- EDIT ROLES MODAL -->
        @if (showRolesModal(); as target) {
          <div class="modal-backdrop">
            <div class="modal-card">
              <header class="modal-header">
                <h3>Edit Roles: {{ target.displayName }}</h3>
                <button type="button" class="close-btn" (click)="showRolesModal.set(null)">✕</button>
              </header>

              <form (ngSubmit)="onSaveRoles(target)" class="modal-body">
                <div class="form-group">
                  <label>Assigned Roles</label>
                  <div class="checkbox-group">
                    @for (role of availableRoles(); track role) {
                      <label class="checkbox-label">
                        <input
                          type="checkbox"
                          [checked]="selectedRolesToEdit().includes(role)"
                          (change)="toggleEditRole(role)" />
                        {{ role }}
                      </label>
                    }
                  </div>
                </div>

                <div class="modal-footer">
                  <button type="button" class="btn btn-secondary" (click)="showRolesModal.set(null)">Cancel</button>
                  <button type="submit" class="btn btn-primary" [disabled]="submitting()">
                    {{ submitting() ? 'Saving...' : 'Update Roles' }}
                  </button>
                </div>
              </form>
            </div>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .admin-page {
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
    .forbidden-card {
      background: #fef2f2;
      border: 1px solid #fecaca;
      border-radius: 8px;
      padding: 2rem;
      text-align: center;
      color: #991b1b;
    }
    .admin-tabs {
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
      color: #7c3aed;
      border-bottom-color: #7c3aed;
    }
    .feedback-banner {
      background: #eff6ff;
      border: 1px solid #bfdbfe;
      color: #1e40af;
      padding: 0.75rem 1rem;
      border-radius: 6px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.85rem;
    }
    .feedback-banner.error {
      background: #fef2f2;
      border-color: #fecaca;
      color: #991b1b;
    }
    .toolbar-card {
      background: #ffffff;
      padding: 1rem 1.25rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    .filters-row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
    }
    .search-input {
      padding: 0.45rem 0.65rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.85rem;
      min-width: 220px;
    }
    select {
      padding: 0.45rem 0.65rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.85rem;
      background: white;
    }
    .add-btn { margin-left: auto; }
    .table-container {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      overflow-x: auto;
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.85rem;
    }
    .data-table th {
      text-align: left;
      padding: 0.65rem 0.85rem;
      background: #f8fafc;
      color: #64748b;
      font-weight: 600;
      font-size: 0.75rem;
      border-bottom: 1px solid #e2e8f0;
      text-transform: uppercase;
    }
    .data-table td {
      padding: 0.65rem 0.85rem;
      border-bottom: 1px solid #f1f5f9;
      color: #334155;
    }
    .roles-cluster {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
    }
    .role-pill {
      font-size: 0.7rem;
      padding: 2px 7px;
      border-radius: 10px;
      background: #e2e8f0;
      color: #334155;
      font-weight: 600;
    }
    .role-pill[data-role="Administrator"] {
      background: #f3e8ff;
      color: #7e22ce;
    }
    .role-pill[data-role="Manager"] {
      background: #e0f2fe;
      color: #0369a1;
    }
    .role-pill[data-role="SupportAgent"] {
      background: #fef3c7;
      color: #b45309;
    }
    .status-badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .status-badge.active { background: #dcfce7; color: #16a34a; }
    .status-badge.inactive { background: #fee2e2; color: #dc2626; }
    .action-badge {
      font-size: 0.75rem;
      font-weight: 600;
      background: #f1f5f9;
      padding: 2px 6px;
      border-radius: 4px;
      color: #1e293b;
    }
    .entity-pill {
      font-size: 0.75rem;
      background: #e2e8f0;
      padding: 2px 6px;
      border-radius: 4px;
      margin-right: 4px;
    }
    .entity-id { color: #94a3b8; font-size: 0.7rem; }
    .nowrap { white-space: nowrap; }
    .corr-code { font-size: 0.75rem; background: #f8fafc; padding: 2px 4px; border-radius: 4px; }
    .meta-json {
      max-width: 250px;
      display: inline-block;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-size: 0.75rem;
      color: #475569;
    }
    .actions-cluster { display: flex; gap: 0.5rem; }
    .btn {
      padding: 0.4rem 0.8rem;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
      border: none;
    }
    .btn-primary { background: #7c3aed; color: white; }
    .btn-primary:hover { background: #6d28d9; }
    .btn-secondary { background: #f1f5f9; color: #334155; border: 1px solid #cbd5e1; }
    .btn-secondary:hover { background: #e2e8f0; }
    .btn-danger { background: #ef4444; color: white; }
    .btn-danger:hover { background: #dc2626; }
    .btn-success { background: #10b981; color: white; }
    .btn-sm { padding: 0.25rem 0.6rem; font-size: 0.75rem; }
    .empty-cell { padding: 2rem; text-align: center; color: #94a3b8; }
    .pagination-bar {
      padding: 0.75rem 1rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-top: 1px solid #e2e8f0;
      background: #f8fafc;
    }
    .page-indicator { font-size: 0.8rem; color: #64748b; }
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.45);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 200;
    }
    .modal-card {
      background: white;
      border-radius: 10px;
      width: 480px;
      max-width: 90vw;
      box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.2);
      overflow: hidden;
    }
    .modal-header {
      padding: 1rem 1.25rem;
      background: #f8fafc;
      border-bottom: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .modal-header h3 { margin: 0; font-size: 1.1rem; color: #0f172a; }
    .modal-body { padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.35rem; }
    .form-group label { font-size: 0.75rem; font-weight: 600; color: #475569; text-transform: uppercase; }
    .form-group input {
      padding: 0.5rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.85rem;
    }
    .checkbox-group { display: flex; flex-direction: column; gap: 0.5rem; margin-top: 0.25rem; }
    .checkbox-label { display: flex; align-items: center; gap: 0.5rem; font-size: 0.85rem; cursor: pointer; }
    .modal-footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
      margin-top: 0.75rem;
      padding-top: 0.75rem;
      border-top: 1px solid #e2e8f0;
    }
    .close-btn {
      background: none;
      border: none;
      cursor: pointer;
      color: #94a3b8;
      font-size: 1rem;
    }
    .close-btn:hover { color: #0f172a; }
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
      border-top-color: #7c3aed;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class AdminComponent implements OnInit {
  readonly authService = inject(AuthService);
  private readonly adminService = inject(AdminService);

  activeTab = signal<'users' | 'audit'>('users');
  availableRoles = signal<string[]>(['Administrator', 'Manager', 'Employee', 'SupportAgent']);

  // Feedback banner state
  feedbackMessage = signal<string | null>(null);
  isFeedbackError = signal<boolean>(false);
  submitting = signal<boolean>(false);

  // User tab state
  users = signal<AdminUserSummary[]>([]);
  usersLoading = signal<boolean>(false);
  userSearch = '';
  userRoleFilter = 'All';
  userStatusFilter = 'All';
  userCurrentPage = signal<number>(1);
  userTotalPages = signal<number>(1);
  userTotalCount = signal<number>(0);

  // Create user modal
  showCreateModal = signal<boolean>(false);
  createModel: CreateUserRequest = {
    email: '',
    displayName: '',
    password: '',
    roles: ['Employee']
  };

  // Edit roles modal
  showRolesModal = signal<AdminUserSummary | null>(null);
  selectedRolesToEdit = signal<string[]>([]);

  // Audit tab state
  auditLogs = signal<AuditLog[]>([]);
  auditLoading = signal<boolean>(false);
  auditActionFilter = 'All';
  auditEntityTypeFilter = 'All';
  auditCurrentPage = signal<number>(1);
  auditTotalPages = signal<number>(1);
  auditTotalCount = signal<number>(0);

  ngOnInit(): void {
    if (this.authService.hasRole('Administrator')) {
      this.adminService.getRoles().subscribe({
        next: (roles) => this.availableRoles.set(roles)
      });
      this.loadUsers(1);
    }
  }

  setTab(tab: 'users' | 'audit'): void {
    this.activeTab.set(tab);
    if (tab === 'users') {
      this.loadUsers(1);
    } else {
      this.loadAuditLogs(1);
    }
  }

  loadUsers(page = 1): void {
    this.usersLoading.set(true);
    this.userCurrentPage.set(page);

    let isActive: boolean | undefined = undefined;
    if (this.userStatusFilter === 'true') isActive = true;
    if (this.userStatusFilter === 'false') isActive = false;

    this.adminService.listUsers(this.userSearch, this.userRoleFilter, isActive, page, 20).subscribe({
      next: (res) => {
        this.users.set(res.items);
        this.userTotalCount.set(res.totalCount);
        this.userTotalPages.set(res.totalPages);
        this.usersLoading.set(false);
      },
      error: () => {
        this.setFeedback('Failed to load users list.', true);
        this.usersLoading.set(false);
      }
    });
  }

  resetUserFilters(): void {
    this.userSearch = '';
    this.userRoleFilter = 'All';
    this.userStatusFilter = 'All';
    this.loadUsers(1);
  }

  openCreateModal(): void {
    this.createModel = {
      email: '',
      displayName: '',
      password: '',
      roles: ['Employee']
    };
    this.showCreateModal.set(true);
  }

  isRoleSelected(role: string): boolean {
    return this.createModel.roles.includes(role);
  }

  toggleRoleSelection(role: string): void {
    if (this.createModel.roles.includes(role)) {
      this.createModel.roles = this.createModel.roles.filter(r => r !== role);
    } else {
      this.createModel.roles = [...this.createModel.roles, role];
    }
  }

  onCreateUser(): void {
    if (!this.createModel.email || !this.createModel.displayName) {
      this.setFeedback('Email and display name are required.', true);
      return;
    }

    if (this.createModel.roles.length === 0) {
      this.setFeedback('Select at least one role.', true);
      return;
    }

    this.submitting.set(true);
    this.adminService.createUser(this.createModel).subscribe({
      next: (created) => {
        this.showCreateModal.set(false);
        this.submitting.set(false);
        this.setFeedback(`User ${created.displayName} (${created.email}) created successfully.`);
        this.loadUsers(1);
      },
      error: (err) => {
        this.submitting.set(false);
        this.setFeedback(err.error?.detail || 'Failed to create user.', true);
      }
    });
  }

  openRolesModal(user: AdminUserSummary): void {
    this.selectedRolesToEdit.set([...user.roles]);
    this.showRolesModal.set(user);
  }

  toggleEditRole(role: string): void {
    const current = this.selectedRolesToEdit();
    if (current.includes(role)) {
      this.selectedRolesToEdit.set(current.filter(r => r !== role));
    } else {
      this.selectedRolesToEdit.set([...current, role]);
    }
  }

  onSaveRoles(user: AdminUserSummary): void {
    const roles = this.selectedRolesToEdit();
    if (roles.length === 0) {
      this.setFeedback('At least one role must be assigned.', true);
      return;
    }

    this.submitting.set(true);
    this.adminService.updateUserRoles(user.id, roles).subscribe({
      next: (updated) => {
        this.showRolesModal.set(null);
        this.submitting.set(false);
        this.setFeedback(`Roles updated for ${updated.displayName}.`);
        this.loadUsers(this.userCurrentPage());
      },
      error: (err) => {
        this.submitting.set(false);
        this.setFeedback(err.error?.detail || 'Failed to update user roles.', true);
      }
    });
  }

  toggleUserStatus(user: AdminUserSummary): void {
    const nextStatus = !user.isActive;
    const actionLabel = nextStatus ? 'activate' : 'deactivate';

    this.adminService.updateUserStatus(user.id, nextStatus).subscribe({
      next: (updated) => {
        this.setFeedback(`User ${updated.displayName} has been ${nextStatus ? 'activated' : 'deactivated'}.`);
        this.loadUsers(this.userCurrentPage());
      },
      error: (err) => {
        this.setFeedback(err.error?.detail || `Failed to ${actionLabel} user.`, true);
      }
    });
  }

  loadAuditLogs(page = 1): void {
    this.auditLoading.set(true);
    this.auditCurrentPage.set(page);

    const filters: AuditLogFilterParams = {
      action: this.auditActionFilter,
      entityType: this.auditEntityTypeFilter,
      page,
      pageSize: 20
    };

    this.adminService.getAuditLogs(filters).subscribe({
      next: (res) => {
        this.auditLogs.set(res.items);
        this.auditTotalCount.set(res.totalCount);
        this.auditTotalPages.set(res.totalPages);
        this.auditLoading.set(false);
      },
      error: () => {
        this.setFeedback('Failed to load audit logs.', true);
        this.auditLoading.set(false);
      }
    });
  }

  resetAuditFilters(): void {
    this.auditActionFilter = 'All';
    this.auditEntityTypeFilter = 'All';
    this.loadAuditLogs(1);
  }

  private setFeedback(msg: string, isError = false): void {
    this.feedbackMessage.set(msg);
    this.isFeedbackError.set(isError);
  }
}
