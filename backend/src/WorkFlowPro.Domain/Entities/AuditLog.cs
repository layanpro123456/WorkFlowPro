namespace WorkFlowPro.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }

    public AuditLog()
    {
        Id = Guid.NewGuid();
        OccurredAtUtc = DateTime.UtcNow;
    }

    public AuditLog(
        Guid? actorId,
        string action,
        string entityType,
        Guid? entityId = null,
        string? metadataJson = null,
        string? correlationId = null)
    {
        Id = Guid.NewGuid();
        ActorId = actorId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OccurredAtUtc = DateTime.UtcNow;
        MetadataJson = metadataJson;
        CorrelationId = correlationId;
    }
}
