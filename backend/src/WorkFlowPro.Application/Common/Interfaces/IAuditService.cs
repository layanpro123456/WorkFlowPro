namespace WorkFlowPro.Application.Common.Interfaces;

public interface IAuditService
{
    Task RecordAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
