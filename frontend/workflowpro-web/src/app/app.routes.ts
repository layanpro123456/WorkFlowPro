import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./core/layout/shell.component').then(m => m.ShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'projects',
        loadComponent: () => import('./features/projects/project-list.component').then(m => m.ProjectListComponent)
      },
      {
        path: 'projects/:id',
        loadComponent: () => import('./features/projects/project-detail.component').then(m => m.ProjectDetailComponent)
      },
      {
        path: 'projects/:id/tasks',
        loadComponent: () => import('./features/tasks/task-board.component').then(m => m.TaskBoardComponent)
      },
      {
        path: 'projects/:id/backlog',
        loadComponent: () => import('./features/sprints/sprint-backlog.component').then(m => m.SprintBacklogComponent)
      },
      {
        path: 'tasks',
        loadComponent: () => import('./features/tasks/task-board.component').then(m => m.TaskBoardComponent)
      },
      {
        path: 'tasks/:id',
        loadComponent: () => import('./features/tasks/task-detail.component').then(m => m.TaskDetailComponent)
      },
      {
        path: 'tickets',
        loadComponent: () => import('./features/tickets/ticket-list.component').then(m => m.TicketListComponent)
      },
      {
        path: 'tickets/:id',
        loadComponent: () => import('./features/tickets/ticket-detail.component').then(m => m.TicketDetailComponent)
      },
      {
        path: 'approvals',
        loadComponent: () => import('./features/approvals/approval-list.component').then(m => m.ApprovalListComponent)
      },
      {
        path: 'approvals/:id',
        loadComponent: () => import('./features/approvals/approval-detail.component').then(m => m.ApprovalDetailComponent)
      },
      {
        path: 'reports',
        loadComponent: () => import('./features/reports/reports.component').then(m => m.ReportsComponent)
      },
      {
        path: 'admin',
        loadComponent: () => import('./features/admin/admin.component').then(m => m.AdminComponent)
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
