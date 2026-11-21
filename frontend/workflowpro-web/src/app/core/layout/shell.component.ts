import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { NotificationService } from '../notifications/notification.service';
import { InAppNotification } from '../notifications/notification.models';

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
          <a routerLink="/reports" routerLinkActive="active" class="nav-item">Reports</a>
          @if (authService.hasRole('Administrator')) {
            <a routerLink="/admin" routerLinkActive="active" class="nav-item admin-badge">Administration</a>
          }
        </nav>

        <div class="header-user">
          <!-- Notification Bell -->
          <div class="notification-container">
            <button class="notif-btn" (click)="toggleNotifications()" title="Notifications">
              <span class="bell-icon">🔔</span>
              @if (notificationService.unreadCount() > 0) {
                <span class="notif-badge">{{ notificationService.unreadCount() }}</span>
              }
            </button>

            @if (showNotifications()) {
              <div class="notif-dropdown">
                <header class="notif-header">
                  <span class="notif-heading">Notifications</span>
                  @if (notificationService.unreadCount() > 0) {
                    <button class="notif-action-btn" (click)="onMarkAllRead()">Mark all as read</button>
                  }
                </header>

                <div class="notif-list">
                  @for (n of notifications(); track n.id) {
                    <div class="notif-item" [class.unread]="!n.isRead" (click)="onNotificationClick(n)">
                      <div class="notif-body">
                        <strong class="notif-title">{{ n.title }}</strong>
                        <p class="notif-msg">{{ n.message }}</p>
                        <span class="notif-date">{{ n.createdAtUtc | date:'short' }}</span>
                      </div>
                      @if (!n.isRead) {
                        <span class="unread-dot">●</span>
                      }
                    </div>
                  } @empty {
                    <div class="notif-empty">No notifications yet.</div>
                  }
                </div>
              </div>
            }
          </div>

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
      position: relative;
      z-index: 50;
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
      gap: 1.25rem;
    }
    .notification-container {
      position: relative;
    }
    .notif-btn {
      background: #334155;
      border: none;
      width: 36px;
      height: 36px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      position: relative;
      color: #f8fafc;
    }
    .notif-btn:hover { background: #475569; }
    .bell-icon { font-size: 1.1rem; }
    .notif-badge {
      position: absolute;
      top: -4px;
      right: -4px;
      background: #ef4444;
      color: white;
      font-size: 0.65rem;
      font-weight: 700;
      padding: 1px 5px;
      border-radius: 10px;
      border: 2px solid #1e293b;
    }
    .notif-dropdown {
      position: absolute;
      top: 48px;
      right: 0;
      width: 340px;
      background: white;
      border-radius: 8px;
      box-shadow: 0 10px 25px -5px rgba(0,0,0,0.2);
      border: 1px solid #e2e8f0;
      color: #0f172a;
      overflow: hidden;
      z-index: 100;
    }
    .notif-header {
      padding: 0.75rem 1rem;
      background: #f8fafc;
      border-bottom: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .notif-heading { font-size: 0.85rem; font-weight: 700; color: #1e293b; }
    .notif-action-btn {
      background: none;
      border: none;
      color: #2563eb;
      font-size: 0.75rem;
      cursor: pointer;
      font-weight: 600;
    }
    .notif-list {
      max-height: 380px;
      overflow-y: auto;
    }
    .notif-item {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      cursor: pointer;
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 0.5rem;
      transition: background 0.15s;
    }
    .notif-item:hover { background: #f8fafc; }
    .notif-item.unread { background: #eff6ff; }
    .notif-body { flex: 1; }
    .notif-title { font-size: 0.85rem; color: #0f172a; display: block; margin-bottom: 2px; }
    .notif-msg { margin: 0 0 4px; font-size: 0.8rem; color: #475569; line-height: 1.4; }
    .notif-date { font-size: 0.7rem; color: #94a3b8; }
    .unread-dot { color: #2563eb; font-size: 0.75rem; }
    .notif-empty { padding: 2rem; text-align: center; color: #94a3b8; font-size: 0.85rem; }
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
export class ShellComponent implements OnInit {
  readonly authService = inject(AuthService);
  readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  showNotifications = signal<boolean>(false);
  notifications = signal<InAppNotification[]>([]);

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      this.notificationService.fetchUnreadCount().subscribe();
    }
  }

  toggleNotifications(): void {
    const nextState = !this.showNotifications();
    this.showNotifications.set(nextState);
    if (nextState) {
      this.loadNotifications();
    }
  }

  loadNotifications(): void {
    this.notificationService.list(false, 1, 10).subscribe({
      next: (res) => this.notifications.set(res.items),
      error: () => this.notifications.set([])
    });
  }

  onMarkAllRead(): void {
    this.notificationService.markAllAsRead().subscribe({
      next: () => {
        this.notifications.update(list => list.map(n => ({ ...n, isRead: true })));
      }
    });
  }

  onNotificationClick(notif: InAppNotification): void {
    if (!notif.isRead) {
      this.notificationService.markAsRead(notif.id).subscribe();
      this.notifications.update(list =>
        list.map(n => n.id === notif.id ? { ...n, isRead: true } : n)
      );
    }

    this.showNotifications.set(false);

    if (notif.entityType === 'Task' && notif.entityId) {
      this.router.navigate(['/tasks', notif.entityId]);
    } else if (notif.entityType === 'Ticket' && notif.entityId) {
      this.router.navigate(['/tickets', notif.entityId]);
    } else if (notif.entityType === 'Approval' && notif.entityId) {
      this.router.navigate(['/approvals', notif.entityId]);
    }
  }

  onLogout(): void {
    this.authService.logout().subscribe();
  }
}
