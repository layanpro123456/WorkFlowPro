namespace WorkFlowPro.Application.Features.Notifications.Dtos;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? EntityType,
    Guid? EntityId,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc
);

public record NotificationListResult(
    IReadOnlyList<NotificationDto> Items,
    int TotalCount,
    int UnreadCount,
    int Page,
    int PageSize,
    int TotalPages
);
