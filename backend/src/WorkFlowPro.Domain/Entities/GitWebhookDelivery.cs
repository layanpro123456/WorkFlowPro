using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Domain.Entities;

public class GitWebhookDelivery : BaseEntity
{
    public Guid? RepositoryId { get; private set; }
    public string Provider { get; private set; } = GitProviders.GitHub;
    public string DeliveryId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = GitWebhookDeliveryStatuses.Processed;
    public string? ErrorMessage { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    public ProjectGitRepository? Repository { get; private set; }

    private GitWebhookDelivery() { } // For EF Core

    public GitWebhookDelivery(
        Guid? repositoryId,
        string provider,
        string deliveryId,
        string eventType,
        string payloadHash)
    {
        Id = Guid.NewGuid();
        RepositoryId = repositoryId;
        Provider = provider;
        DeliveryId = deliveryId;
        EventType = eventType;
        PayloadHash = payloadHash;
        Status = GitWebhookDeliveryStatuses.Processed;
        ReceivedAtUtc = DateTime.UtcNow;
        CreatedAtUtc = ReceivedAtUtc;
    }

    public void MarkProcessed(string status, string? errorMessage = null)
    {
        Status = status;
        ErrorMessage = errorMessage != null && errorMessage.Length > 1000
            ? errorMessage[..1000]
            : errorMessage;
        ProcessedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = ProcessedAtUtc;
    }
}
