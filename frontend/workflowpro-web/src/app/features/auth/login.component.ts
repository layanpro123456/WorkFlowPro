import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="login-wrapper">
      <div class="login-card">
        <div class="card-header">
          <div class="brand-icon">⚡</div>
          <h2>Sign in to Jiva</h2>
          <p class="subtitle">Projects, Git-Linked Kanban &amp; Workflow Operations</p>
        </div>

        @if (errorMessage()) {
          <div class="error-banner" role="alert">
            <span class="error-icon">⚠️</span>
            <div class="error-text">{{ errorMessage() }}</div>
          </div>
        }

        <form [formGroup]="loginForm" (ngSubmit)="onSubmit()" novalidate>
          <div class="form-group">
            <label for="email">Work Email</label>
            <input
              id="email"
              type="email"
              formControlName="email"
              placeholder="you@company.com"
              [class.invalid]="isFieldInvalid('email')"
              autocomplete="email"
            />
            @if (isFieldInvalid('email')) {
              <span class="field-error">Please enter a valid work email.</span>
            }
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              id="password"
              type="password"
              formControlName="password"
              placeholder="••••••••"
              [class.invalid]="isFieldInvalid('password')"
              autocomplete="current-password"
            />
            @if (isFieldInvalid('password')) {
              <span class="field-error">Password is required.</span>
            }
          </div>

          <button
            type="submit"
            class="submit-btn"
            [disabled]="loginForm.invalid || authService.isLoading()"
          >
            @if (authService.isLoading()) {
              <span class="spinner"></span> Signing in...
            } @else {
              Sign in
            }
          </button>
        </form>

        <div class="demo-box">
          <span class="demo-badge">Demo Account</span>
          <p>Email: <code>admin&#64;workflowpro.local</code></p>
          <p>Password: <code>Admin&#64;123456</code></p>
          <button type="button" class="quick-fill-btn" (click)="quickFill()">
            Use Demo Account
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-wrapper {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
      padding: 1.5rem;
      box-sizing: border-box;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .login-card {
      background: #ffffff;
      border-radius: 12px;
      padding: 2.5rem;
      width: 100%;
      max-width: 420px;
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.3), 0 8px 10px -6px rgba(0, 0, 0, 0.2);
    }
    .card-header {
      text-align: center;
      margin-bottom: 2rem;
    }
    .brand-icon {
      font-size: 2.5rem;
      margin-bottom: 0.5rem;
    }
    h2 {
      margin: 0;
      color: #0f172a;
      font-size: 1.5rem;
      font-weight: 700;
    }
    .subtitle {
      color: #64748b;
      font-size: 0.85rem;
      margin-top: 0.25rem;
    }
    .error-banner {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #b91c1c;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      font-size: 0.85rem;
      display: flex;
      align-items: flex-start;
      gap: 0.5rem;
      margin-bottom: 1.5rem;
    }
    .form-group {
      margin-bottom: 1.25rem;
    }
    label {
      display: block;
      font-size: 0.85rem;
      font-weight: 600;
      color: #334155;
      margin-bottom: 0.4rem;
    }
    input {
      width: 100%;
      box-sizing: border-box;
      padding: 0.65rem 0.85rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.95rem;
      color: #0f172a;
      outline: none;
      transition: border-color 0.15s ease-in-out;
    }
    input:focus {
      border-color: #2563eb;
      box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
    }
    input.invalid {
      border-color: #ef4444;
    }
    .field-error {
      color: #dc2626;
      font-size: 0.75rem;
      margin-top: 0.25rem;
      display: block;
    }
    .submit-btn {
      width: 100%;
      padding: 0.75rem;
      background: #2563eb;
      color: white;
      border: none;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.95rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      transition: background 0.15s;
    }
    .submit-btn:hover:not(:disabled) {
      background: #1d4ed8;
    }
    .submit-btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .spinner {
      width: 16px;
      height: 16px;
      border: 2px solid white;
      border-top-color: transparent;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    .demo-box {
      margin-top: 2rem;
      padding: 1rem;
      background: #f1f5f9;
      border-radius: 8px;
      border: 1px dashed #cbd5e1;
      font-size: 0.8rem;
      color: #475569;
    }
    .demo-box p {
      margin: 0.25rem 0;
    }
    .demo-badge {
      font-size: 0.7rem;
      font-weight: 700;
      text-transform: uppercase;
      background: #e2e8f0;
      padding: 2px 6px;
      border-radius: 4px;
      color: #334155;
    }
    code {
      background: #e2e8f0;
      padding: 1px 4px;
      border-radius: 3px;
      color: #0f172a;
    }
    .quick-fill-btn {
      margin-top: 0.5rem;
      background: #ffffff;
      border: 1px solid #94a3b8;
      color: #1e293b;
      padding: 0.35rem 0.65rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
    }
    .quick-fill-btn:hover {
      background: #f8fafc;
      border-color: #475569;
    }
  `]
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly errorMessage = signal<string | null>(null);

  readonly loginForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  isFieldInvalid(field: 'email' | 'password'): boolean {
    const control = this.loginForm.get(field);
    return !!(control && control.invalid && (control.dirty || control.touched));
  }

  quickFill(): void {
    this.loginForm.patchValue({
      email: 'admin@workflowpro.local',
      password: 'Admin@123456'
    });
    this.errorMessage.set(null);
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    const credentials = this.loginForm.getRawValue() as { email: string; password: string };

    this.authService.login(credentials).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
        this.router.navigateByUrl(returnUrl);
      },
      error: (err) => {
        const detail = err.error?.detail || err.error?.title || 'Failed to sign in. Please check your credentials.';
        this.errorMessage.set(detail);
      }
    });
  }
}
