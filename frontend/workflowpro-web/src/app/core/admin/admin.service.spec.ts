import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminService } from './admin.service';
import { AdminUserSummary, AuditLog, CreateUserRequest } from './admin.models';
import { PagedResult } from '../projects/project.models';

describe('AdminService', () => {
  let service: AdminService;
  let httpTesting: HttpTestingController;

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
        correlationId: 'corr-abc',
        metadataJson: '{"roles":["Employee"]}'
      }
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(AdminService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('listUsers queries admin users endpoint with params', () => {
    service.listUsers('john', 'Employee', true, 1, 20).subscribe(res => {
      expect(res).toEqual(mockUsers);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/admin/users' &&
      r.params.get('search') === 'john' &&
      r.params.get('role') === 'Employee' &&
      r.params.get('isActive') === 'true' &&
      r.params.get('page') === '1' &&
      r.params.get('pageSize') === '20'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockUsers);
  });

  it('createUser sends POST request with payload', () => {
    const payload: CreateUserRequest = {
      email: 'newuser@test.local',
      displayName: 'New User',
      password: 'Pass1234!',
      roles: ['Employee']
    };

    service.createUser(payload).subscribe(res => {
      expect(res.email).toBe(payload.email);
    });

    const req = httpTesting.expectOne('/api/v1/admin/users');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ ...payload, id: 'u-new', isActive: true, createdAtUtc: '2026-11-21' });
  });

  it('updateUserStatus sends PATCH with new active state', () => {
    service.updateUserStatus('u-1', false).subscribe(res => {
      expect(res.isActive).toBe(false);
    });

    const req = httpTesting.expectOne('/api/v1/admin/users/u-1/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ isActive: false });
    req.flush({ id: 'u-1', email: 'admin@test.local', displayName: 'Admin', isActive: false, roles: [] });
  });

  it('updateUserRoles sends PUT with new role list', () => {
    service.updateUserRoles('u-1', ['Manager', 'Employee']).subscribe(res => {
      expect(res.roles).toContain('Manager');
    });

    const req = httpTesting.expectOne('/api/v1/admin/users/u-1/roles');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ roles: ['Manager', 'Employee'] });
    req.flush({ id: 'u-1', email: 'admin@test.local', displayName: 'Admin', isActive: true, roles: ['Manager', 'Employee'] });
  });

  it('getAuditLogs queries audit logs endpoint with filters', () => {
    service.getAuditLogs({ action: 'UserCreated', page: 1, pageSize: 20 }).subscribe(res => {
      expect(res).toEqual(mockLogs);
    });

    const req = httpTesting.expectOne(r =>
      r.url === '/api/v1/admin/audit-logs' &&
      r.params.get('action') === 'UserCreated' &&
      r.params.get('page') === '1'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockLogs);
  });
});
