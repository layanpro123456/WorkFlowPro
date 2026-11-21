using WorkFlowPro.Domain.Common;

namespace WorkFlowPro.Domain.Entities;

public class InAppNotification : BaseEntity
{
    public Guid RecipientId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc.HasValue;

    // Private constructor for EF Core
    private InAppNotification() { }

    public InAppNotification(
        Guid recipientId,
        string type,
        string title,
        string message,
        string? entityType = null,
        Guid? entityId = null)
    {
        if (recipientId == Guid.Empty)
            throw new ArgumentException("RecipientId cannot be empty.", nameof(recipientId));

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Notification type cannot be empty.", nameof(type));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        RecipientId = recipientId;
        Type = type.Trim();
        Title = title.Trim();
        Message = message.Trim();
        EntityType = string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim();
        EntityId = entityId.HasValue && entityId.Value != Guid.Empty ? entityId : null;
    }

    public void MarkAsRead()
    {
        if (!ReadAtUtc.HasValue)
        {
            ReadAtUtc = DateTime.UtcNow;
        }
    }
}
