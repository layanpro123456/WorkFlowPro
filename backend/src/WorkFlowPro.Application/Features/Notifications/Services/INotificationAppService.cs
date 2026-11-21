using WorkFlowPro.Application.Features.Notifications.Dtos;

namespace WorkFlowPro.Application.Features.Notifications.Services;

public interface INotificationAppService
{
    Task<NotificationListResult> GetNotificationsAsync(bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken);
    Task MarkAllAsReadAsync(CancellationToken cancellationToken);
    Task CreateNotificationAsync(
        Guid recipientId,
        string type,
        string title,
        string message,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default);
}
