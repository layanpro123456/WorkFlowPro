import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="shell-layout">
      <header class="app-header">
        <div class="header-brand">
          <span class="brand-logo">⚡</span>
          <span class="brand-name">WorkFlowPro</span>
          <span class="brand-env">v1.0</span>
        </div>

        <nav class="header-nav">
          <a routerLink="/dashboard" routerLinkActive="active" class="nav-item">Dashboard</a>
          <a routerLink="/projects" routerLinkActive="active" class="nav-item">Projects</a>
          <a routerLink="/tasks" routerLinkActive="active" class="nav-item">Tasks</a>
          <a routerLink="/tickets" routerLinkActive="active" class="nav-item">Service Desk</a>
          <a routerLink="/approvals" routerLinkActive="active" class="nav-item">Approvals</a>
          @if (authService.hasRole('Administrator')) {
            <a routerLink="/admin" routerLinkActive="active" class="nav-item admin-badge">Administration</a>
          }
        </nav>

        <div class="header-user">
          @if (authService.currentUser(); as user) {
            <div class="user-info">
              <span class="user-name">{{ user.displayName }}</span>
              <div class="user-roles">
                @for (role of user.roles; track role) {
                  <span class="role-pill">{{ role }}</span>
                }
              </div>
            </div>
            <button class="logout-btn" (click)="onLogout()" title="Log out">Sign out</button>
          }
        </div>
      </header>

      <main class="shell-content">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .shell-layout {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      background: #f8fafc;
      color: #0f172a;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .app-header {
      background: #1e293b;
      color: #f8fafc;
      padding: 0 1.5rem;
      height: 64px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }
    .header-brand {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-weight: 700;
      font-size: 1.15rem;
    }
    .brand-logo { font-size: 1.25rem; }
    .brand-env {
      font-size: 0.7rem;
      background: #334155;
      padding: 2px 6px;
      border-radius: 4px;
      color: #94a3b8;
    }
    .header-nav {
      display: flex;
      gap: 0.5rem;
    }
    .nav-item {
      color: #cbd5e1;
      text-decoration: none;
      padding: 0.5rem 0.85rem;
      border-radius: 6px;
      font-size: 0.9rem;
      transition: all 0.15s ease-in-out;
    }
    .nav-item:hover {
      color: #ffffff;
      background: #334155;
    }
    .nav-item.active {
      color: #ffffff;
      background: #2563eb;
      font-weight: 500;
    }
    .admin-badge {
      border: 1px solid #7c3aed;
      color: #c4b5fd;
    }
    .header-user {
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .user-info {
      text-align: right;
    }
    .user-name {
      display: block;
      font-size: 0.85rem;
      font-weight: 600;
    }
    .user-roles {
      display: flex;
      gap: 4px;
      justify-content: flex-end;
    }
    .role-pill {
      font-size: 0.65rem;
      background: #0284c7;
      color: white;
      padding: 1px 6px;
      border-radius: 10px;
    }
    .logout-btn {
      background: transparent;
      border: 1px solid #475569;
      color: #cbd5e1;
      padding: 0.4rem 0.75rem;
      border-radius: 6px;
      cursor: pointer;
      font-size: 0.8rem;
    }
    .logout-btn:hover {
      background: #dc2626;
      border-color: #dc2626;
      color: white;
    }
    .shell-content {
      flex: 1;
      padding: 2rem;
      max-width: 1280px;
      margin: 0 auto;
      width: 100%;
      box-sizing: border-box;
    }
  `]
})
export class ShellComponent {
  readonly authService = inject(AuthService);

  onLogout(): void {
    this.authService.logout().subscribe();
  }
}
