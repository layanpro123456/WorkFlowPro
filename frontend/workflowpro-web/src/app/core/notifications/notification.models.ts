export interface InAppNotification {
  id: string;
  type: string;
  title: string;
  message: string;
  entityType?: string | null;
  entityId?: string | null;
  isRead: boolean;
  readAtUtc?: string | null;
  createdAtUtc: string;
}

export interface NotificationListResult {
  items: InAppNotification[];
  totalCount: number;
  unreadCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
