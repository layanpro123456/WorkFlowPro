import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = authService.accessToken();

  let authReq = req.clone({
    withCredentials: true
  });

  if (token && !req.headers.has('Authorization')) {
    authReq = authReq.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // Avoid loop on login or refresh failures
      const isAuthEndpoint = req.url.includes('/api/v1/auth/login') || req.url.includes('/api/v1/auth/refresh');

      if (error.status === 401 && !isAuthEndpoint && token) {
        return authService.refresh().pipe(
          switchMap((refreshed) => {
            if (refreshed && refreshed.accessToken) {
              const retryReq = req.clone({
                withCredentials: true,
                setHeaders: {
                  Authorization: `Bearer ${refreshed.accessToken}`
                }
              });
              return next(retryReq);
            }
            authService.clearAuthSession();
            router.navigate(['/login']);
            return throwError(() => error);
          }),
          catchError((refreshErr) => {
            authService.clearAuthSession();
            router.navigate(['/login']);
            return throwError(() => refreshErr);
          })
        );
      }

      return throwError(() => error);
    })
  );
};
