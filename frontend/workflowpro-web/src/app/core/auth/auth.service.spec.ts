import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { AuthResponse, UserProfile } from './auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;

  const mockUser: UserProfile = {
    id: '11111111-1111-1111-1111-111111111111',
    email: 'admin@workflowpro.local',
    displayName: 'System Admin',
    roles: ['Administrator', 'Manager']
  };

  const mockAuthResponse: AuthResponse = {
    accessToken: 'sample-jwt-access-token',
    expiresAtUtc: '2026-11-21T16:00:00Z',
    user: mockUser
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'login', component: class DummyLogin {} },
          { path: 'dashboard', component: class DummyDashboard {} }
        ])
      ]
    });
    service = TestBed.inject(AuthService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created and initial state should be unauthenticated', () => {
    expect(service).toBeTruthy();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(service.accessToken()).toBeNull();
  });

  it('should authenticate user and set state on successful login', () => {
    service.login({ email: 'admin@workflowpro.local', password: 'Password123' }).subscribe((res) => {
      expect(res).toEqual(mockAuthResponse);
    });

    const req = httpTesting.expectOne('/api/v1/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush(mockAuthResponse);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.accessToken()).toBe('sample-jwt-access-token');
    expect(service.currentUser()?.displayName).toBe('System Admin');
    expect(service.hasRole('Administrator')).toBe(true);
    expect(service.hasRole('SupportAgent')).toBe(false);
  });

  it('should clear state on logout', () => {
    // Set initial logged in state
    service.currentUser.set(mockUser);
    service.accessToken.set('mock-token');

    service.logout().subscribe();

    const req = httpTesting.expectOne('/api/v1/auth/logout');
    expect(req.request.method).toBe('POST');
    req.flush(null);

    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(service.accessToken()).toBeNull();
  });

  it('should rotate token and update session on refresh', () => {
    service.refresh().subscribe((res) => {
      expect(res).toEqual(mockAuthResponse);
    });

    const req = httpTesting.expectOne('/api/v1/auth/refresh');
    expect(req.request.method).toBe('POST');
    req.flush(mockAuthResponse);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.accessToken()).toBe('sample-jwt-access-token');
  });
});
