using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class IssueLink : BaseEntity
{
    public Guid SourceTaskId { get; private set; }
    public Guid TargetTaskId { get; private set; }
    public string LinkType { get; private set; } = IssueLinkTypes.RelatesTo;
    public Guid CreatedById { get; private set; }

    public TaskItem? SourceTask { get; private set; }
    public TaskItem? TargetTask { get; private set; }

    private IssueLink() { } // For EF Core

    public IssueLink(Guid sourceTaskId, Guid targetTaskId, string linkType, Guid createdById)
    {
        if (sourceTaskId == Guid.Empty) throw new ArgumentException("SourceTaskId cannot be empty.", nameof(sourceTaskId));
        if (targetTaskId == Guid.Empty) throw new ArgumentException("TargetTaskId cannot be empty.", nameof(targetTaskId));
        if (createdById == Guid.Empty) throw new ArgumentException("CreatedById cannot be empty.", nameof(createdById));
        if (sourceTaskId == targetTaskId) throw new DomainException("An issue cannot be linked to itself.");
        if (!IssueLinkTypes.IsValid(linkType))
        {
            throw new DomainException($"Invalid issue link type '{linkType}'. Allowed: {string.Join(", ", IssueLinkTypes.All)}");
        }

        Id = Guid.NewGuid();
        SourceTaskId = sourceTaskId;
        TargetTaskId = targetTaskId;
        LinkType = IssueLinkTypes.Normalize(linkType);
        CreatedById = createdById;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
