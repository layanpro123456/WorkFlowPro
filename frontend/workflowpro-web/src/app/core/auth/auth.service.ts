import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, tap, throwError } from 'rxjs';
import { AuthResponse, LoginCredentials, UserProfile } from './auth.models';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly baseUrl = '/api/v1/auth';

  // State signals
  readonly currentUser = signal<UserProfile | null>(null);
  readonly accessToken = signal<string | null>(null);
  readonly isLoading = signal<boolean>(false);

  readonly isAuthenticated = computed(() => !!this.currentUser());
  readonly userRoles = computed(() => this.currentUser()?.roles ?? []);

  login(credentials: LoginCredentials): Observable<AuthResponse> {
    this.isLoading.set(true);
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, credentials, { withCredentials: true }).pipe(
      tap((response) => {
        this.setAuthSession(response);
        this.isLoading.set(false);
      }),
      catchError((error) => {
        this.isLoading.set(false);
        return throwError(() => error);
      })
    );
  }

  refresh(): Observable<AuthResponse | null> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/refresh`, {}, { withCredentials: true }).pipe(
      tap((response) => {
        this.setAuthSession(response);
      }),
      catchError((error) => {
        this.clearAuthSession();
        return of(null);
      })
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => {
        this.clearAuthSession();
        this.router.navigate(['/login']);
      }),
      catchError(() => {
        this.clearAuthSession();
        this.router.navigate(['/login']);
        return of(undefined);
      })
    );
  }

  getMe(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.baseUrl}/me`).pipe(
      tap((profile) => {
        this.currentUser.set(profile);
      })
    );
  }

  hasRole(role: string): boolean {
    return this.userRoles().includes(role);
  }

  hasAnyRole(roles: string[]): boolean {
    const current = this.userRoles();
    return roles.some((r) => current.includes(r));
  }

  private setAuthSession(response: AuthResponse): void {
    this.accessToken.set(response.accessToken);
    this.currentUser.set(response.user);
  }

  clearAuthSession(): void {
    this.accessToken.set(null);
    this.currentUser.set(null);
  }
}
