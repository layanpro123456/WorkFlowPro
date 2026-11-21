import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { InAppNotification, NotificationListResult } from './notification.models';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/notifications';

  readonly unreadCount = signal<number>(0);

  list(unreadOnly = false, page = 1, pageSize = 20): Observable<NotificationListResult> {
    const params = new HttpParams()
      .set('unreadOnly', unreadOnly.toString())
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<NotificationListResult>(this.baseUrl, { params }).pipe(
      tap(res => this.unreadCount.set(res.unreadCount))
    );
  }

  fetchUnreadCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.baseUrl}/unread-count`).pipe(
      tap(res => this.unreadCount.set(res.count))
    );
  }

  markAsRead(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/read`, null).pipe(
      tap(() => this.unreadCount.update(c => Math.max(0, c - 1)))
    );
  }

  markAllAsRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/read-all`, null).pipe(
      tap(() => this.unreadCount.set(0))
    );
  }
}
