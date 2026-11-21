using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class AuditLogEntityTests
{
    [Fact]
    public void CreateAuditLog_SetsPropertiesProperly()
    {
        var actorId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var correlationId = "corr-123";
        var metadata = "{\"role\":\"Manager\"}";

        var log = new AuditLog(
            actorId: actorId,
            action: AuditActions.UserRolesUpdated,
            entityType: "User",
            entityId: entityId,
            metadataJson: metadata,
            correlationId: correlationId
        );

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(actorId, log.ActorId);
        Assert.Equal(AuditActions.UserRolesUpdated, log.Action);
        Assert.Equal("User", log.EntityType);
        Assert.Equal(entityId, log.EntityId);
        Assert.Equal(metadata, log.MetadataJson);
        Assert.Equal(correlationId, log.CorrelationId);
        Assert.True(log.OccurredAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void CreateSystemAuditLog_AllowsNullActorId()
    {
        var log = new AuditLog(
            actorId: null,
            action: "SystemMaintenance",
            entityType: "System"
        );

        Assert.Null(log.ActorId);
        Assert.Equal("SystemMaintenance", log.Action);
        Assert.Equal("System", log.EntityType);
        Assert.Null(log.EntityId);
        Assert.Null(log.MetadataJson);
    }
}
