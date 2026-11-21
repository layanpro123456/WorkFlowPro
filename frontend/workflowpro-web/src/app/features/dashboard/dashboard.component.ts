import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="dashboard-page">
      <div class="welcome-banner">
        <div class="welcome-text">
          <h1>Welcome back, {{ authService.currentUser()?.displayName }}!</h1>
          <p class="user-meta">Signed in as <strong>{{ authService.currentUser()?.email }}</strong></p>
        </div>
        <div class="roles-container">
          @for (role of authService.userRoles(); track role) {
            <span class="role-tag">{{ role }}</span>
          }
        </div>
      </div>

      <div class="status-grid">
        <div class="card module-card">
          <div class="card-icon">📁</div>
          <h3>Projects</h3>
          <p>Organize enterprise projects, configure team memberships, and track timelines.</p>
          <span class="status-pill ready">Phase 2 Planned</span>
        </div>

        <div class="card module-card">
          <div class="card-icon">📋</div>
          <h3>Tasks & Kanban</h3>
          <p>Assign tasks, track status transitions with optimistic concurrency, and view boards.</p>
          <span class="status-pill ready">Phase 3 Planned</span>
        </div>

        <div class="card module-card">
          <div class="card-icon">🎫</div>
          <h3>Service Desk</h3>
          <p>Submit tickets, manage assignments, and separate requester notes from private staff notes.</p>
          <span class="status-pill ready">Phase 4 Planned</span>
        </div>

        <div class="card module-card">
          <div class="card-icon">✅</div>
          <h3>Approval Workflows</h3>
          <p>Configurable multi-step approval workflows with audit history and concurrency controls.</p>
          <span class="status-pill ready">Phase 5 Planned</span>
        </div>
      </div>

      <div class="security-card card">
        <h3>🛡️ Identity & Session Status</h3>
        <div class="security-details">
          <div class="security-item">
            <span class="label">Bearer Access Token:</span>
            <span class="value active">Active in memory (15 min expiry)</span>
          </div>
          <div class="security-item">
            <span class="label">Refresh Token:</span>
            <span class="value active">Secured via HttpOnly SameSite cookie</span>
          </div>
          <div class="security-item">
            <span class="label">Authorization:</span>
            <span class="value">Enforced server-side with ASP.NET Core Identity</span>
          </div>
        </div>
      </div>
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
      font-size: 1.6rem;
      color: #0f172a;
    }
    .user-meta {
      margin: 0.25rem 0 0;
      color: #64748b;
      font-size: 0.9rem;
    }
    .roles-container {
      display: flex;
      gap: 0.5rem;
    }
    .role-tag {
      background: #e0f2fe;
      color: #0369a1;
      padding: 4px 10px;
      border-radius: 12px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .status-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 1.25rem;
    }
    .card {
      background: #ffffff;
      padding: 1.5rem;
      border-radius: 10px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
    }
    .card-icon {
      font-size: 1.75rem;
      margin-bottom: 0.5rem;
    }
    h3 {
      margin: 0 0 0.5rem;
      font-size: 1.1rem;
      color: #0f172a;
    }
    p {
      color: #475569;
      font-size: 0.85rem;
      line-height: 1.4;
      margin: 0 0 1rem;
    }
    .status-pill {
      font-size: 0.7rem;
      font-weight: 600;
      text-transform: uppercase;
      padding: 3px 8px;
      border-radius: 4px;
      background: #f1f5f9;
      color: #475569;
    }
    .security-card {
      margin-top: 0.5rem;
    }
    .security-details {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      margin-top: 0.75rem;
    }
    .security-item {
      display: flex;
      font-size: 0.85rem;
      gap: 0.5rem;
    }
    .label {
      font-weight: 600;
      color: #334155;
      min-width: 180px;
    }
    .value.active {
      color: #16a34a;
      font-weight: 500;
    }
  `]
})
export class DashboardComponent {
  readonly authService = inject(AuthService);
}
