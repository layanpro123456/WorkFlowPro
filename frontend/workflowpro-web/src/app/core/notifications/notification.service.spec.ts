import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NotificationService } from './notification.service';
import { InAppNotification, NotificationListResult } from './notification.models';

describe('NotificationService', () => {
  let service: NotificationService;
  let httpTesting: HttpTestingController;

  const mockNotifs: InAppNotification[] = [
    {
      id: 'n-1',
      type: 'TaskAssigned',
      title: 'Task Assigned',
      message: 'You were assigned to task TASK-101',
      entityType: 'Task',
      entityId: 't-1',
      isRead: false,
      readAtUtc: null,
      createdAtUtc: '2026-11-21T10:00:00Z'
    }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(NotificationService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('fetchUnreadCount updates unreadCount signal', () => {
    service.fetchUnreadCount().subscribe(res => {
      expect(res.count).toBe(3);
      expect(service.unreadCount()).toBe(3);
    });

    const req = httpTesting.expectOne('/api/v1/notifications/unread-count');
    expect(req.request.method).toBe('GET');
    req.flush({ count: 3 });
  });

  it('list queries notifications and caches unreadCount', () => {
    const mockResponse: NotificationListResult = {
      items: mockNotifs,
      unreadCount: 1,
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1
    };

    service.list(false, 1, 20).subscribe(res => {
      expect(res.items.length).toBe(1);
      expect(service.unreadCount()).toBe(1);
    });

    const req = httpTesting.expectOne('/api/v1/notifications?unreadOnly=false&page=1&pageSize=20');
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('markAsRead calls endpoint and updates unread count', () => {
    service.unreadCount.set(2);

    service.markAsRead('n-1').subscribe();

    const req = httpTesting.expectOne('/api/v1/notifications/n-1/read');
    expect(req.request.method).toBe('PATCH');
    req.flush({});

    expect(service.unreadCount()).toBe(1);
  });

  it('markAllAsRead calls endpoint and sets unreadCount to 0', () => {
    service.unreadCount.set(5);

    service.markAllAsRead().subscribe();

    const req = httpTesting.expectOne('/api/v1/notifications/read-all');
    expect(req.request.method).toBe('POST');
    req.flush({});

    expect(service.unreadCount()).toBe(0);
  });
});
