using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Domain.Entities;

namespace WorkFlowPro.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public AuditService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task RecordAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var actorId = _currentUserService.UserId;
            var correlationId = _httpContextAccessor.HttpContext?.TraceIdentifier;

            string? metadataJson = null;
            if (metadata != null)
            {
                if (metadata is string str)
                {
                    metadataJson = str;
                }
                else
                {
                    metadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
                }
            }

            var auditLog = new AuditLog(
                actorId: actorId,
                action: action,
                entityType: entityType,
                entityId: entityId,
                metadataJson: metadataJson,
                correlationId: correlationId);

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit log for action {Action} on {EntityType} {EntityId}",
                action, entityType, entityId);
            // Do not throw to avoid failing main transaction unless strict compliance mode requires it
        }
    }
}
