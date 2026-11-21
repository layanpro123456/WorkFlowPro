using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Features.Notifications.Dtos;
using WorkFlowPro.Domain.Entities;

namespace WorkFlowPro.Application.Features.Notifications.Services;

public class NotificationAppService : INotificationAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public NotificationAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private Guid RequireCallerId() =>
        _currentUserService.UserId ?? throw new UnauthorizedAccessException("Authentication required.");

    public async Task<NotificationListResult> GetNotificationsAsync(
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == callerId);

        var unreadCount = await query
            .Where(n => n.ReadAtUtc == null)
            .CountAsync(cancellationToken);

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.EntityType,
                n.EntityId,
                n.ReadAtUtc.HasValue,
                n.ReadAtUtc,
                n.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return new NotificationListResult(items, totalCount, unreadCount, page, pageSize, totalPages);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == callerId && n.ReadAtUtc == null)
            .CountAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == callerId, cancellationToken);

        if (notification == null)
        {
            // Resource concealment: Return 404 if notification belongs to another user or doesn't exist
            throw new KeyNotFoundException($"Notification '{notificationId}' not found.");
        }

        notification.MarkAsRead();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();

        var unreadNotifications = await _context.Notifications
            .Where(n => n.RecipientId == callerId && n.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.MarkAsRead();
        }

        if (unreadNotifications.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CreateNotificationAsync(
        Guid recipientId,
        string type,
        string title,
        string message,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new InAppNotification(
            recipientId,
            type,
            title,
            message,
            entityType,
            entityId
        );

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
