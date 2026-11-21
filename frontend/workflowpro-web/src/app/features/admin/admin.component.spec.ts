import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AdminComponent } from './admin.component';
import { AdminService } from '../../core/admin/admin.service';
import { AuthService } from '../../core/auth/auth.service';
import { AdminUserSummary, AuditLog } from '../../core/admin/admin.models';
import { PagedResult } from '../../core/projects/project.models';

describe('AdminComponent', () => {
  let component: AdminComponent;
  let fixture: ComponentFixture<AdminComponent>;
  let adminService: AdminService;
  let authService: AuthService;

  const mockUsers: PagedResult<AdminUserSummary> = {
    items: [
      {
        id: 'u-1',
        email: 'admin@test.local',
        displayName: 'Admin User',
        isActive: true,
        roles: ['Administrator'],
        createdAtUtc: '2026-11-21T00:00:00Z',
        updatedAtUtc: null
      }
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false
  };

  const mockLogs: PagedResult<AuditLog> = {
    items: [
      {
        id: 'log-1',
        actorId: 'u-1',
        actorDisplayName: 'Admin User',
        actorEmail: 'admin@test.local',
        action: 'UserCreated',
        entityType: 'User',
        entityId: 'u-2',
        occurredAtUtc: '2026-11-21T01:00:00Z',
        correlationId: 'corr-1',
        metadataJson: null
      }
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminComponent);
    component = fixture.componentInstance;
    adminService = TestBed.inject(AdminService);
    authService = TestBed.inject(AuthService);
  });

  it('loads users and roles on init for Administrator', () => {
    vi.spyOn(authService, 'hasRole').mockReturnValue(true);
    const usersSpy = vi.spyOn(adminService, 'listUsers').mockReturnValue(of(mockUsers));
    const rolesSpy = vi.spyOn(adminService, 'getRoles').mockReturnValue(of(['Administrator', 'Manager', 'Employee', 'SupportAgent']));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(usersSpy).toHaveBeenCalled();
    expect(rolesSpy).toHaveBeenCalled();
    expect(component.users().length).toBe(1);
    expect(component.users()[0].email).toBe('admin@test.local');
  });

  it('switches to audit tab and loads audit records', () => {
    vi.spyOn(authService, 'hasRole').mockReturnValue(true);
    vi.spyOn(adminService, 'listUsers').mockReturnValue(of(mockUsers));
    vi.spyOn(adminService, 'getRoles').mockReturnValue(of(['Administrator']));
    const auditSpy = vi.spyOn(adminService, 'getAuditLogs').mockReturnValue(of(mockLogs));

    fixture.detectChanges();

    component.setTab('audit');

    expect(component.activeTab()).toBe('audit');
    expect(auditSpy).toHaveBeenCalled();
    expect(component.auditLogs().length).toBe(1);
    expect(component.auditLogs()[0].action).toBe('UserCreated');
  });

  it('toggles user active status successfully', () => {
    vi.spyOn(authService, 'hasRole').mockReturnValue(true);
    vi.spyOn(adminService, 'listUsers').mockReturnValue(of(mockUsers));
    vi.spyOn(adminService, 'getRoles').mockReturnValue(of(['Administrator']));
    const statusSpy = vi.spyOn(adminService, 'updateUserStatus').mockReturnValue(of({
      ...mockUsers.items[0],
      isActive: false
    }));

    fixture.detectChanges();

    component.toggleUserStatus(mockUsers.items[0]);

    expect(statusSpy).toHaveBeenCalledWith('u-1', false);
    expect(component.feedbackMessage()).toContain('deactivated');
  });

  it('creates user and displays success feedback', () => {
    vi.spyOn(authService, 'hasRole').mockReturnValue(true);
    vi.spyOn(adminService, 'listUsers').mockReturnValue(of(mockUsers));
    vi.spyOn(adminService, 'getRoles').mockReturnValue(of(['Administrator']));
    const createSpy = vi.spyOn(adminService, 'createUser').mockReturnValue(of({
      id: 'u-2',
      email: 'jane@test.local',
      displayName: 'Jane',
      isActive: true,
      roles: ['Employee'],
      createdAtUtc: '2026-11-21'
    }));

    fixture.detectChanges();

    component.createModel = {
      email: 'jane@test.local',
      displayName: 'Jane',
      password: '',
      roles: ['Employee']
    };

    component.onCreateUser();

    expect(createSpy).toHaveBeenCalled();
    expect(component.showCreateModal()).toBe(false);
    expect(component.feedbackMessage()).toContain('created successfully');
  });
});
