import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/auth/auth.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authService: AuthService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'dashboard', component: class DummyDashboard {} }
        ])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService);
    fixture.detectChanges();
  });

  it('should create login component with invalid form initially', () => {
    expect(component).toBeTruthy();
    expect(component.loginForm.valid).toBe(false);
  });

  it('should validate email format', () => {
    const emailControl = component.loginForm.get('email');
    emailControl?.setValue('invalid-email');
    expect(emailControl?.valid).toBe(false);

    emailControl?.setValue('valid@workflowpro.local');
    expect(emailControl?.valid).toBe(true);
  });

  it('quickFill should populate demo credentials', () => {
    component.quickFill();
    expect(component.loginForm.value.email).toBe('admin@workflowpro.local');
    expect(component.loginForm.value.password).toBe('Admin@123456');
    expect(component.loginForm.valid).toBe(true);
  });

  it('onSubmit should call authService.login when form is valid', () => {
    const loginSpy = vi.spyOn(authService, 'login').mockReturnValue(of({
      accessToken: 'dummy',
      expiresAtUtc: '2026-11-21',
      user: { id: '1', email: 'admin@workflowpro.local', displayName: 'Admin', roles: ['Administrator'] }
    }));

    component.quickFill();
    component.onSubmit();

    expect(loginSpy).toHaveBeenCalledWith({
      email: 'admin@workflowpro.local',
      password: 'Admin@123456'
    });
  });
});
