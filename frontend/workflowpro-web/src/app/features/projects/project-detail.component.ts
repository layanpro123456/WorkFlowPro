import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectService } from '../../core/projects/project.service';
import { ProjectDetail, ProjectMember } from '../../core/projects/project.models';
import { AuthService } from '../../core/auth/auth.service';
import { GitService } from '../../core/git/git.service';
import { ProjectGitRepository, ProjectPullRequestSummary } from '../../core/git/git.models';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="project-detail-page">
      <nav class="breadcrumb">
        <a routerLink="/projects">← Back to Projects</a>
      </nav>

      @if (isLoading()) {
        <div class="loading-state">
          <span class="spinner"></span> Loading project details...
        </div>
      } @else if (errorMessage()) {
        <div class="error-banner">{{ errorMessage() }}</div>
      } @else if (project(); as p) {
        <div class="project-header">
          <div class="header-main">
            <div class="code-row">
              <span class="code-badge">{{ p.projectCode }}</span>
              <span class="status-badge" [ngClass]="p.status.toLowerCase()">{{ p.status }}</span>
              @if (p.archivedAtUtc) {
                <span class="archived-tag">Archived on {{ p.archivedAtUtc | date:'shortDate' }}</span>
              }
            </div>
            <h1>{{ p.name }}</h1>
            <p class="meta-row">
              Created by <strong>{{ p.createdByName }}</strong> on {{ p.createdAtUtc | date:'mediumDate' }}
            </p>
          </div>

          <div class="header-actions">
            <a [routerLink]="['/projects', p.id, 'backlog']" class="btn-secondary">Backlog &amp; Sprints</a>
            <a [routerLink]="['/projects', p.id, 'tasks']" class="btn-secondary">Kanban Board</a>
            @if (canEdit()) {
              <button class="btn-secondary" (click)="openEditModal()">Edit Project</button>
            }
            @if (canArchive() && !p.archivedAtUtc) {
              <button class="btn-danger" (click)="onArchive()">Archive</button>
            }
          </div>
        </div>

        <div class="detail-grid">
          <div class="detail-card info-section">
            <h3>Overview</h3>
            <p class="description-text">{{ p.description || 'No description provided.' }}</p>

            <div class="dates-box">
              <div class="date-item">
                <span class="date-label">Start Date:</span>
                <span class="date-value">{{ p.startDate || 'Not set' }}</span>
              </div>
              <div class="date-item">
                <span class="date-label">Target Date:</span>
                <span class="date-value">{{ p.targetDate || 'Not set' }}</span>
              </div>
            </div>
          </div>

          <div class="detail-card members-section">
            <div class="section-header">
              <h3>Project Team ({{ p.members.length }})</h3>
              @if (canManageMembers() && !p.archivedAtUtc) {
                <button class="btn-sm-primary" (click)="openAddMemberModal()">+ Add Member</button>
              }
            </div>

            <table class="members-table">
              <thead>
                <tr>
                  <th>Member</th>
                  <th>Role</th>
                  <th>Joined</th>
                  @if (canManageMembers() && !p.archivedAtUtc) {
                    <th>Action</th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (m of p.members; track m.userId) {
                  <tr>
                    <td>
                      <strong>{{ m.displayName }}</strong>
                      <span class="sub-email">{{ m.email }}</span>
                    </td>
                    <td>
                      <span class="role-badge" [ngClass]="m.projectRole.toLowerCase()">
                        {{ m.projectRole }}
                      </span>
                    </td>
                    <td class="text-muted">{{ m.joinedAtUtc | date:'shortDate' }}</td>
                    @if (canManageMembers() && !p.archivedAtUtc) {
                      <td>
                        @if (m.projectRole !== 'Owner') {
                          <button class="btn-remove" (click)="removeMember(m.userId)">Remove</button>
                        }
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <!-- Git Repositories & Pull Requests Section -->
        <section class="detail-card git-section">
          <div class="section-header">
            <div>
              <h3>Git Repositories & Pull Requests</h3>
              <p class="sub-hint">Connect GitHub repositories for branch creation, PR syncing, and Kanban auto-transitions.</p>
            </div>
            @if (canEdit()) {
              <button class="btn-sm-primary" (click)="openConnectGitModal()">+ Connect GitHub Repo</button>
            }
          </div>

          @if (gitMessage()) {
            <div class="git-alert-success">{{ gitMessage() }}</div>
          }
          @if (gitError()) {
            <div class="modal-error">{{ gitError() }}</div>
          }
          @if (generatedSecret()) {
            <div class="secret-callout">
              <strong>Copy your GitHub Webhook Secret now (shown once):</strong>
              <code>{{ generatedSecret() }}</code>
              <button type="button" class="btn-copy-xs" (click)="copyText(generatedSecret()!)">Copy Secret</button>
            </div>
          }

          @if (repositories().length > 0) {
            <div class="repo-list">
              @for (repo of repositories(); track repo.id) {
                <div class="repo-card">
                  <div class="repo-main">
                    <div class="repo-title-row">
                      <span class="provider-badge">{{ repo.provider }}</span>
                      <a [href]="repo.webBaseUrl + '/' + repo.repositoryFullName" target="_blank" rel="noopener noreferrer" class="repo-link">
                        {{ repo.repositoryFullName }}
                      </a>
                      @if (repo.isDefault) {
                        <span class="default-badge">Default</span>
                      }
                      @if (!repo.isActive) {
                        <span class="inactive-badge">Inactive</span>
                      }
                    </div>
                    <div class="repo-meta">
                      <span>Base branch: <code>{{ repo.defaultBranch }}</code></span>
                      <span>Token: {{ repo.hasAccessToken ? 'Configured' : 'Not set' }}</span>
                      <span>Auto-link: {{ repo.autoLinkEnabled ? 'On' : 'Off' }}</span>
                      <span>Auto-move: {{ repo.autoTransitionEnabled ? 'On' : 'Off' }}</span>
                      <span>InReview col: {{ repo.useInReviewColumn ? 'On' : 'Off' }}</span>
                    </div>
                    <div class="webhook-row">
                      <span>Webhook URL: <code>{{ buildFullWebhookUrl(repo.webhookPath) }}</code></span>
                      <button type="button" class="btn-copy-xs" (click)="copyText(buildFullWebhookUrl(repo.webhookPath))">Copy URL</button>
                    </div>
                    @if (repo.lastSyncError) {
                      <div class="sync-err">Last sync error: {{ repo.lastSyncError }}</div>
                    }
                  </div>

                  @if (canEdit()) {
                    <div class="repo-actions">
                      <button type="button" class="btn-xs" [disabled]="testingRepoId() === repo.id" (click)="testRepo(repo)">
                        {{ testingRepoId() === repo.id ? 'Testing...' : 'Test' }}
                      </button>
                      <button type="button" class="btn-xs" [disabled]="isSyncingRepo() === repo.id" (click)="syncRepo(repo)">
                        {{ isSyncingRepo() === repo.id ? 'Syncing...' : 'Sync PRs' }}
                      </button>
                      <button type="button" class="btn-xs" (click)="openEditGitModal(repo)">Edit</button>
                      <button type="button" class="btn-remove" (click)="disconnectRepo(repo)">Disconnect</button>
                    </div>
                  }
                </div>
              }
            </div>
          } @else {
            <p class="hint-text">No Git repositories connected to this project yet.</p>
          }

          @if (pullRequests().length > 0) {
            <div class="pr-table-wrap">
              <h4>Synced Pull Requests ({{ pullRequests().length }})</h4>
              <table class="members-table">
                <thead>
                  <tr>
                    <th>PR</th>
                    <th>Branches</th>
                    <th>State</th>
                    <th>Review / CI</th>
                    <th>Linked Cards</th>
                  </tr>
                </thead>
                <tbody>
                  @for (pr of pullRequests(); track pr.id) {
                    <tr>
                      <td>
                        <a [href]="pr.htmlUrl" target="_blank" rel="noopener noreferrer" class="repo-link">
                          #{{ pr.prNumber }} {{ pr.title }}
                        </a>
                        <span class="sub-email">by @{{ pr.authorLogin }} • {{ pr.repositoryFullName }}</span>
                      </td>
                      <td><code>{{ pr.sourceBranch }} → {{ pr.targetBranch }}</code></td>
                      <td><span class="role-badge">{{ pr.state }}</span></td>
                      <td>{{ pr.reviewStatus }} / CI: {{ pr.checkStatus }}</td>
                      <td>
                        @if (pr.linkedTaskKeys.length > 0) {
                          {{ pr.linkedTaskKeys.join(', ') }}
                        } @else {
                          <span class="text-muted">Unlinked</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>

        <!-- Connect / Edit Git Repository Modal -->
        @if (showGitModal()) {
          <div class="modal-overlay" (click)="closeGitModal()">
            <div class="modal-card" (click)="$event.stopPropagation()">
              <div class="modal-header">
                <h3>{{ editingRepoId() ? 'Edit Git Repository' : 'Connect GitHub Repository' }}</h3>
                <button class="close-btn" (click)="closeGitModal()">✕</button>
              </div>

              @if (gitModalError()) {
                <div class="modal-error">{{ gitModalError() }}</div>
              }

              <form (ngSubmit)="submitGitRepo()" class="git-modal-form">
                @if (!editingRepoId()) {
                  <div class="form-row">
                    <div class="form-group flex-1">
                      <label for="gOwner">Owner / Org *</label>
                      <input id="gOwner" type="text" [(ngModel)]="gitForm.repositoryOwner" name="gOwner" required placeholder="e.g. octocat" />
                    </div>
                    <div class="form-group flex-1">
                      <label for="gName">Repository Name *</label>
                      <input id="gName" type="text" [(ngModel)]="gitForm.repositoryName" name="gName" required placeholder="e.g. hello-world" />
                    </div>
                  </div>
                }

                <div class="form-row">
                  <div class="form-group flex-1">
                    <label for="gBranch">Default Branch *</label>
                    <input id="gBranch" type="text" [(ngModel)]="gitForm.defaultBranch" name="gBranch" required />
                  </div>
                  <div class="form-group flex-1">
                    <label for="gApiUrl">API Base URL</label>
                    <input id="gApiUrl" type="text" [(ngModel)]="gitForm.apiBaseUrl" name="gApiUrl" placeholder="https://api.github.com" />
                  </div>
                </div>

                <div class="form-group">
                  <label for="gToken">Personal Access Token {{ editingRepoId() ? '(leave blank to keep existing)' : '' }}</label>
                  <input id="gToken" type="password" [(ngModel)]="gitForm.accessToken" name="gToken" placeholder="ghp_..." />
                </div>

                <div class="form-group">
                  <label for="gSecret">Webhook Secret {{ editingRepoId() ? '(leave blank to keep existing)' : '(optional - auto-generated if blank)' }}</label>
                  <input id="gSecret" type="password" [(ngModel)]="gitForm.webhookSecret" name="gSecret" />
                </div>

                <div class="git-toggles">
                  <label class="toggle-check"><input type="checkbox" [(ngModel)]="gitForm.isDefault" name="gDef" /> Default repo for branch creation</label>
                  <label class="toggle-check"><input type="checkbox" [(ngModel)]="gitForm.autoLinkEnabled" name="gAutoLink" /> Auto-link branches & PRs by Task Key</label>
                  <label class="toggle-check"><input type="checkbox" [(ngModel)]="gitForm.autoTransitionEnabled" name="gAutoMove" /> Auto-transition Kanban cards on PR events</label>
                  <label class="toggle-check"><input type="checkbox" [(ngModel)]="gitForm.useInReviewColumn" name="gInRev" /> Move opened PRs to InReview column</label>
                  @if (editingRepoId()) {
                    <label class="toggle-check"><input type="checkbox" [(ngModel)]="gitForm.isActive" name="gActive" /> Active</label>
                  }
                </div>

                <div class="modal-actions">
                  <button type="button" class="btn-cancel" (click)="closeGitModal()">Cancel</button>
                  <button type="submit" class="btn-primary">{{ editingRepoId() ? 'Save Settings' : 'Connect Repository' }}</button>
                </div>
              </form>
            </div>
          </div>
        }

        <!-- Edit Modal -->
        @if (showEditModal()) {
          <div class="modal-overlay" (click)="closeEditModal()">
            <div class="modal-card" (click)="$event.stopPropagation()">
              <div class="modal-header">
                <h3>Edit Project</h3>
                <button class="close-btn" (click)="closeEditModal()">✕</button>
              </div>

              @if (modalError()) {
                <div class="modal-error">{{ modalError() }}</div>
              }

              <form [formGroup]="editForm" (ngSubmit)="submitEdit()">
                <div class="form-group">
                  <label for="eName">Project Name *</label>
                  <input id="eName" type="text" formControlName="name" />
                </div>

                <div class="form-group">
                  <label for="eDesc">Description</label>
                  <textarea id="eDesc" rows="3" formControlName="description"></textarea>
                </div>

                <div class="form-group">
                  <label for="eStatus">Status</label>
                  <select id="eStatus" formControlName="status">
                    <option value="Active">Active</option>
                    <option value="OnHold">OnHold</option>
                    <option value="Completed">Completed</option>
                  </select>
                </div>

                <div class="form-row">
                  <div class="form-group flex-1">
                    <label for="eStart">Start Date</label>
                    <input id="eStart" type="date" formControlName="startDate" />
                  </div>
                  <div class="form-group flex-1">
                    <label for="eTarget">Target Date</label>
                    <input id="eTarget" type="date" formControlName="targetDate" />
                  </div>
                </div>

                <div class="modal-actions">
                  <button type="button" class="btn-cancel" (click)="closeEditModal()">Cancel</button>
                  <button type="submit" class="btn-primary" [disabled]="editForm.invalid">Save Changes</button>
                </div>
              </form>
            </div>
          </div>
        }

        <!-- Add Member Modal -->
        @if (showAddMemberModal()) {
          <div class="modal-overlay" (click)="closeAddMemberModal()">
            <div class="modal-card" (click)="$event.stopPropagation()">
              <div class="modal-header">
                <h3>Add Team Member</h3>
                <button class="close-btn" (click)="closeAddMemberModal()">✕</button>
              </div>

              @if (memberModalError()) {
                <div class="modal-error">{{ memberModalError() }}</div>
              }

              <form [formGroup]="addMemberForm" (ngSubmit)="submitAddMember()">
                <div class="form-group">
                  <label for="mUserId">User ID (UUID) *</label>
                  <input id="mUserId" type="text" formControlName="userId" placeholder="00000000-0000-0000-0000-000000000000" />
                  <span class="hint-text">Enter the internal UUID of an active employee or manager.</span>
                </div>

                <div class="form-group">
                  <label for="mRole">Project Role *</label>
                  <select id="mRole" formControlName="projectRole">
                    <option value="Member">Member</option>
                    <option value="Manager">Manager</option>
                    <option value="Owner">Owner</option>
                  </select>
                </div>

                <div class="modal-actions">
                  <button type="button" class="btn-cancel" (click)="closeAddMemberModal()">Cancel</button>
                  <button type="submit" class="btn-primary" [disabled]="addMemberForm.invalid">Add Member</button>
                </div>
              </form>
            </div>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .project-detail-page { display: flex; flex-direction: column; gap: 1.5rem; }
    .breadcrumb a { color: #2563eb; text-decoration: none; font-size: 0.85rem; font-weight: 500; }
    .breadcrumb a:hover { text-decoration: underline; }
    .project-header {
      background: white;
      padding: 1.5rem 2rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
    }
    .code-row { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.5rem; }
    .code-badge {
      background: #e2e8f0;
      color: #0f172a;
      padding: 2px 8px;
      border-radius: 4px;
      font-family: monospace;
      font-weight: 700;
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
    .archived-tag { font-size: 0.8rem; color: #ef4444; font-weight: 600; }
    h1 { margin: 0; font-size: 1.8rem; color: #0f172a; }
    .meta-row { margin: 0.35rem 0 0; color: #64748b; font-size: 0.85rem; }
    .header-actions { display: flex; gap: 0.5rem; }
    .btn-secondary {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-danger {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #b91c1c;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-danger:hover { background: #dc2626; color: white; }
    .detail-grid {
      display: grid;
      grid-template-columns: 1fr 1.25fr;
      gap: 1.5rem;
    }
    .detail-card {
      background: white;
      padding: 1.5rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    h3 { margin: 0 0 1rem; font-size: 1.15rem; color: #0f172a; }
    .description-text { color: #334155; line-height: 1.5; font-size: 0.9rem; }
    .dates-box {
      margin-top: 1.5rem;
      border-top: 1px solid #f1f5f9;
      padding-top: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .date-item { display: flex; font-size: 0.85rem; }
    .date-label { color: #64748b; width: 100px; font-weight: 500; }
    .date-value { color: #0f172a; font-weight: 600; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .btn-sm-primary {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.35rem 0.75rem;
      border-radius: 4px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
    }
    .members-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .members-table th { text-align: left; padding: 0.5rem; background: #f8fafc; border-bottom: 1px solid #e2e8f0; }
    .members-table td { padding: 0.65rem 0.5rem; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .sub-email { display: block; font-size: 0.75rem; color: #64748b; font-weight: normal; }
    .role-badge {
      font-size: 0.7rem;
      padding: 2px 6px;
      border-radius: 10px;
      font-weight: 600;
      text-transform: capitalize;
    }
    .role-badge.owner { background: #fef3c7; color: #92400e; }
    .role-badge.manager { background: #e0f2fe; color: #0369a1; }
    .role-badge.member { background: #f1f5f9; color: #475569; }
    .btn-remove {
      background: none;
      border: 1px solid #fecaca;
      color: #dc2626;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 0.7rem;
      cursor: pointer;
    }
    .btn-remove:hover { background: #dc2626; color: white; }
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
      max-width: 480px;
      padding: 1.75rem;
      box-shadow: 0 10px 25px rgba(0,0,0,0.2);
    }
    .modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.25rem; }
    .close-btn { background: none; border: none; font-size: 1.25rem; cursor: pointer; color: #94a3b8; }
    .form-group { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 1rem; }
    .form-row { display: flex; gap: 1rem; }
    .flex-1 { flex: 1; }
    label { font-size: 0.85rem; font-weight: 600; color: #334155; }
    input, textarea, select {
      padding: 0.55rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
      outline: none;
    }
    .hint-text { font-size: 0.75rem; color: #64748b; }
    .modal-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.25rem; }
    .btn-cancel { background: transparent; border: 1px solid #cbd5e1; padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; }
    .btn-primary { background: #2563eb; color: white; border: none; padding: 0.5rem 1rem; border-radius: 6px; font-weight: 600; cursor: pointer; }
    .modal-error { background: #fef2f2; color: #b91c1c; padding: 0.5rem 0.75rem; border-radius: 6px; font-size: 0.8rem; margin-bottom: 1rem; }
    .sub-hint { margin: 0.2rem 0 0; font-size: 0.8rem; color: #64748b; }
    .git-alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; padding: 0.6rem 0.85rem; border-radius: 6px; font-size: 0.85rem; margin-bottom: 1rem; }
    .secret-callout { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; padding: 0.75rem; border-radius: 6px; font-size: 0.82rem; display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; margin-bottom: 1rem; }
    .repo-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .repo-card { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; padding: 1rem; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; flex-wrap: wrap; }
    .repo-title-row { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.4rem; }
    .provider-badge { font-size: 0.7rem; font-weight: 700; background: #0f172a; color: #ffffff; padding: 2px 7px; border-radius: 4px; }
    .default-badge { font-size: 0.7rem; font-weight: 600; background: #dbeafe; color: #1e40af; padding: 2px 6px; border-radius: 4px; }
    .inactive-badge { font-size: 0.7rem; font-weight: 600; background: #fee2e2; color: #991b1b; padding: 2px 6px; border-radius: 4px; }
    .repo-link { font-weight: 600; color: #2563eb; text-decoration: none; }
    .repo-link:hover { text-decoration: underline; }
    .repo-meta { display: flex; flex-wrap: wrap; gap: 0.75rem; font-size: 0.78rem; color: #475569; margin-bottom: 0.4rem; }
    .webhook-row { display: flex; align-items: center; gap: 0.5rem; font-size: 0.75rem; color: #64748b; }
    .sync-err { color: #b91c1c; font-size: 0.78rem; margin-top: 0.35rem; }
    .repo-actions { display: flex; gap: 0.4rem; align-items: center; }
    .btn-xs, .btn-copy-xs { background: #ffffff; border: 1px solid #cbd5e1; border-radius: 4px; padding: 3px 8px; font-size: 0.75rem; cursor: pointer; color: #1e293b; font-weight: 500; }
    .btn-xs:hover:not(:disabled), .btn-copy-xs:hover { background: #f1f5f9; }
    .pr-table-wrap { margin-top: 1.5rem; }
    .pr-table-wrap h4 { margin: 0 0 0.75rem; font-size: 0.95rem; color: #1e293b; }
    .git-toggles { display: flex; flex-direction: column; gap: 0.45rem; margin: 0.5rem 0; }
    .toggle-check { display: flex; align-items: center; gap: 0.5rem; font-size: 0.85rem; font-weight: 500; color: #334155; cursor: pointer; }
  `]
})
export class ProjectDetailComponent implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly gitService = inject(GitService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly project = signal<ProjectDetail | null>(null);
  readonly repositories = signal<ProjectGitRepository[]>([]);
  readonly pullRequests = signal<ProjectPullRequestSummary[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);

  readonly showEditModal = signal<boolean>(false);
  readonly showAddMemberModal = signal<boolean>(false);
  readonly showGitModal = signal<boolean>(false);
  readonly editingRepoId = signal<string | null>(null);
  readonly testingRepoId = signal<string | null>(null);
  readonly isSyncingRepo = signal<string | null>(null);
  readonly modalError = signal<string | null>(null);
  readonly memberModalError = signal<string | null>(null);
  readonly gitModalError = signal<string | null>(null);
  readonly gitMessage = signal<string | null>(null);
  readonly gitError = signal<string | null>(null);
  readonly generatedSecret = signal<string | null>(null);

  gitForm = {
    repositoryOwner: '',
    repositoryName: '',
    defaultBranch: 'main',
    apiBaseUrl: 'https://api.github.com',
    webBaseUrl: 'https://github.com',
    accessToken: '',
    webhookSecret: '',
    isDefault: true,
    isActive: true,
    autoLinkEnabled: true,
    autoTransitionEnabled: true,
    useInReviewColumn: true
  };

  readonly editForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: [''],
    status: ['Active'],
    startDate: [''],
    targetDate: ['']
  });

  readonly addMemberForm = this.fb.group({
    userId: ['', [Validators.required]],
    projectRole: ['Member', [Validators.required]]
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadProject(id);
    }
  }

  loadProject(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.projectService.getById(id).subscribe({
      next: (detail: ProjectDetail) => {
        this.project.set(detail);
        this.isLoading.set(false);
        this.loadGitData(detail.id);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.detail || 'Project not found or you do not have permission.');
        this.isLoading.set(false);
      }
    });
  }

  loadGitData(projectId: string): void {
    this.gitService.listProjectRepositories(projectId).subscribe({
      next: (repos) => this.repositories.set(repos),
      error: () => this.repositories.set([])
    });
    this.gitService.listProjectPullRequests(projectId).subscribe({
      next: (prs) => this.pullRequests.set(prs),
      error: () => this.pullRequests.set([])
    });
  }

  canEdit(): boolean {
    const p = this.project();
    if (!p) return false;
    if (p.archivedAtUtc) return false;
    if (this.authService.hasRole('Administrator')) return true;

    const currentUserId = this.authService.currentUser()?.id;
    const member = p.members.find(m => m.userId === currentUserId);
    return member?.projectRole === 'Owner' || member?.projectRole === 'Manager';
  }

  canArchive(): boolean {
    const p = this.project();
    if (!p) return false;
    if (this.authService.hasRole('Administrator')) return true;

    const currentUserId = this.authService.currentUser()?.id;
    const member = p.members.find(m => m.userId === currentUserId);
    return member?.projectRole === 'Owner';
  }

  canManageMembers(): boolean {
    return this.canEdit();
  }

  onArchive(): void {
    const p = this.project();
    if (!p) return;

    if (confirm(`Are you sure you want to archive "${p.name}"?`)) {
      this.projectService.archive(p.id).subscribe({
        next: () => this.loadProject(p.id),
        error: (err) => alert(err.error?.detail || 'Failed to archive project.')
      });
    }
  }

  openEditModal(): void {
    const p = this.project();
    if (!p) return;

    this.editForm.patchValue({
      name: p.name,
      description: p.description || '',
      status: p.status,
      startDate: p.startDate || '',
      targetDate: p.targetDate || ''
    });

    this.modalError.set(null);
    this.showEditModal.set(true);
  }

  closeEditModal(): void {
    this.showEditModal.set(false);
  }

  submitEdit(): void {
    const p = this.project();
    if (!p || this.editForm.invalid) return;

    const val = this.editForm.getRawValue();
    this.projectService.update(p.id, {
      name: val.name!,
      description: val.description || undefined,
      status: val.status || undefined,
      startDate: val.startDate || undefined,
      targetDate: val.targetDate || undefined
    }).subscribe({
      next: (updated: ProjectDetail) => {
        this.project.set(updated);
        this.closeEditModal();
      },
      error: (err) => {
        this.modalError.set(err.error?.detail || 'Failed to update project.');
      }
    });
  }

  openAddMemberModal(): void {
    this.addMemberForm.reset({ projectRole: 'Member' });
    this.memberModalError.set(null);
    this.showAddMemberModal.set(true);
  }

  closeAddMemberModal(): void {
    this.showAddMemberModal.set(false);
  }

  submitAddMember(): void {
    const p = this.project();
    if (!p || this.addMemberForm.invalid) return;

    const val = this.addMemberForm.getRawValue();
    this.projectService.addMember(p.id, {
      userId: val.userId!,
      projectRole: val.projectRole!
    }).subscribe({
      next: (newMember: ProjectMember) => {
        this.project.update(current => current ? {
          ...current,
          members: [...current.members, newMember]
        } : null);
        this.closeAddMemberModal();
      },
      error: (err) => {
        this.memberModalError.set(err.error?.detail || 'Failed to add member.');
      }
    });
  }

  removeMember(userId: string): void {
    const p = this.project();
    if (!p) return;

    if (confirm('Are you sure you want to remove this member?')) {
      this.projectService.removeMember(p.id, userId).subscribe({
        next: () => {
          this.project.update(current => current ? {
            ...current,
            members: current.members.filter(m => m.userId !== userId)
          } : null);
        },
        error: (err) => alert(err.error?.detail || 'Failed to remove member.')
      });
    }
  }

  openConnectGitModal(): void {
    this.editingRepoId.set(null);
    this.gitModalError.set(null);
    this.gitForm = {
      repositoryOwner: '',
      repositoryName: '',
      defaultBranch: 'main',
      apiBaseUrl: 'https://api.github.com',
      webBaseUrl: 'https://github.com',
      accessToken: '',
      webhookSecret: '',
      isDefault: true,
      isActive: true,
      autoLinkEnabled: true,
      autoTransitionEnabled: true,
      useInReviewColumn: true
    };
    this.showGitModal.set(true);
  }

  openEditGitModal(repo: ProjectGitRepository): void {
    this.editingRepoId.set(repo.id);
    this.gitModalError.set(null);
    this.gitForm = {
      repositoryOwner: repo.repositoryOwner,
      repositoryName: repo.repositoryName,
      defaultBranch: repo.defaultBranch,
      apiBaseUrl: repo.apiBaseUrl,
      webBaseUrl: repo.webBaseUrl,
      accessToken: '',
      webhookSecret: '',
      isDefault: repo.isDefault,
      isActive: repo.isActive,
      autoLinkEnabled: repo.autoLinkEnabled,
      autoTransitionEnabled: repo.autoTransitionEnabled,
      useInReviewColumn: repo.useInReviewColumn
    };
    this.showGitModal.set(true);
  }

  closeGitModal(): void {
    this.showGitModal.set(false);
    this.gitModalError.set(null);
  }

  submitGitRepo(): void {
    const p = this.project();
    if (!p) return;

    this.gitModalError.set(null);
    const repoId = this.editingRepoId();

    if (!repoId) {
      this.gitService.connectRepository(p.id, {
        provider: 'GitHub',
        repositoryOwner: this.gitForm.repositoryOwner.trim(),
        repositoryName: this.gitForm.repositoryName.trim(),
        defaultBranch: this.gitForm.defaultBranch.trim() || 'main',
        apiBaseUrl: this.gitForm.apiBaseUrl.trim() || null,
        webBaseUrl: this.gitForm.webBaseUrl.trim() || null,
        accessToken: this.gitForm.accessToken.trim() || null,
        webhookSecret: this.gitForm.webhookSecret.trim() || null,
        isDefault: this.gitForm.isDefault,
        autoLinkEnabled: this.gitForm.autoLinkEnabled,
        autoTransitionEnabled: this.gitForm.autoTransitionEnabled,
        useInReviewColumn: this.gitForm.useInReviewColumn
      }).subscribe({
        next: (res) => {
          this.generatedSecret.set(res.generatedWebhookSecret ?? null);
          this.gitMessage.set(`Connected ${res.repository.repositoryFullName}.`);
          this.closeGitModal();
          this.loadGitData(p.id);
        },
        error: (err) => {
          this.gitModalError.set(err.error?.detail || 'Failed to connect Git repository.');
        }
      });
    } else {
      this.gitService.updateRepository(p.id, repoId, {
        defaultBranch: this.gitForm.defaultBranch.trim() || 'main',
        apiBaseUrl: this.gitForm.apiBaseUrl.trim() || null,
        webBaseUrl: this.gitForm.webBaseUrl.trim() || null,
        accessToken: this.gitForm.accessToken.trim() || null,
        webhookSecret: this.gitForm.webhookSecret.trim() || null,
        isDefault: this.gitForm.isDefault,
        isActive: this.gitForm.isActive,
        autoLinkEnabled: this.gitForm.autoLinkEnabled,
        autoTransitionEnabled: this.gitForm.autoTransitionEnabled,
        useInReviewColumn: this.gitForm.useInReviewColumn
      }).subscribe({
        next: (updated) => {
          this.gitMessage.set(`Updated ${updated.repositoryFullName}.`);
          this.closeGitModal();
          this.loadGitData(p.id);
        },
        error: (err) => {
          this.gitModalError.set(err.error?.detail || 'Failed to update Git repository.');
        }
      });
    }
  }

  testRepo(repo: ProjectGitRepository): void {
    const p = this.project();
    if (!p) return;

    this.testingRepoId.set(repo.id);
    this.gitError.set(null);
    this.gitMessage.set(null);

    this.gitService.testRepositoryConnection(p.id, repo.id).subscribe({
      next: (res) => {
        this.testingRepoId.set(null);
        if (res.success) {
          this.gitMessage.set(`${repo.repositoryFullName}: ${res.message} (default branch: ${res.detectedDefaultBranch || repo.defaultBranch})`);
        } else {
          this.gitError.set(`${repo.repositoryFullName}: ${res.message}`);
        }
        this.loadGitData(p.id);
      },
      error: (err) => {
        this.testingRepoId.set(null);
        this.gitError.set(err.error?.detail || 'Connection test failed.');
      }
    });
  }

  syncRepo(repo: ProjectGitRepository): void {
    const p = this.project();
    if (!p) return;

    this.isSyncingRepo.set(repo.id);
    this.gitError.set(null);
    this.gitMessage.set(null);

    this.gitService.syncPullRequests(p.id, repo.id).subscribe({
      next: (res) => {
        this.isSyncingRepo.set(null);
        this.gitMessage.set(`Synced ${res.pullRequestsSynced} PR(s): ${res.tasksLinked} task link(s) added, ${res.tasksTransitioned} card(s) transitioned.`);
        this.loadGitData(p.id);
      },
      error: (err) => {
        this.isSyncingRepo.set(null);
        this.gitError.set(err.error?.detail || 'PR sync failed.');
      }
    });
  }

  disconnectRepo(repo: ProjectGitRepository): void {
    const p = this.project();
    if (!p) return;

    if (confirm(`Disconnect ${repo.repositoryFullName} from this project?`)) {
      this.gitService.disconnectRepository(p.id, repo.id).subscribe({
        next: () => {
          this.gitMessage.set(`Disconnected ${repo.repositoryFullName}.`);
          this.loadGitData(p.id);
        },
        error: (err) => {
          this.gitError.set(err.error?.detail || 'Failed to disconnect repository.');
        }
      });
    }
  }

  buildFullWebhookUrl(path: string): string {
    return typeof window !== 'undefined' ? `${window.location.origin}${path}` : path;
  }

  copyText(val: string): void {
    navigator.clipboard?.writeText(val);
    this.gitMessage.set('Copied to clipboard.');
  }
}
