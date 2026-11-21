using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class InAppNotificationEntityTests
{
    [Fact]
    public void CreateNotification_SetsInitialProperties_IsReadFalse()
    {
        var recipientId = Guid.NewGuid();
        var notification = new InAppNotification(
            recipientId,
            NotificationTypes.TaskAssigned,
            "Task Assigned",
            "You were assigned to task #10",
            "Task",
            Guid.NewGuid()
        );

        Assert.Equal(recipientId, notification.RecipientId);
        Assert.Equal(NotificationTypes.TaskAssigned, notification.Type);
        Assert.Equal("Task Assigned", notification.Title);
        Assert.Equal("You were assigned to task #10", notification.Message);
        Assert.Equal("Task", notification.EntityType);
        Assert.NotNull(notification.EntityId);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAtUtc);
    }

    [Fact]
    public void MarkAsRead_SetsReadAtUtcAndIsReadTrue()
    {
        var notification = new InAppNotification(
            Guid.NewGuid(),
            NotificationTypes.TicketStatusChanged,
            "Ticket Updated",
            "Your ticket is InProgress"
        );

        Assert.False(notification.IsRead);
        notification.MarkAsRead();

        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAtUtc);
    }

    [Fact]
    public void EmptyRecipient_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new InAppNotification(Guid.Empty, NotificationTypes.TaskAssigned, "Title", "Message"));
    }
}
