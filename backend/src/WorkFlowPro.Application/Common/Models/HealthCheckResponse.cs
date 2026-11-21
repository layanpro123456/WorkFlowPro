namespace WorkFlowPro.Application.Common.Models;

public record HealthCheckResponse(
    string Status,
    DateTime TimestampUtc,
    string Version
);
