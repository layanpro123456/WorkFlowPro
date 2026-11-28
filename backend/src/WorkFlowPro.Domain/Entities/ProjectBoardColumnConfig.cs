using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class ProjectBoardColumnConfig : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public string Status { get; private set; } = TaskStatuses.Backlog;
    public int DisplayOrder { get; private set; }
    public int? WipLimit { get; private set; }
    public bool IsStrictWip { get; private set; }
    public bool IsVisible { get; private set; } = true;

    public Project? Project { get; private set; }

    private ProjectBoardColumnConfig() { } // For EF Core

    public ProjectBoardColumnConfig(
        Guid projectId,
        string status,
        int displayOrder,
        int? wipLimit = null,
        bool isStrictWip = false,
        bool isVisible = true)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        if (!TaskStatuses.IsValid(status))
        {
            throw new DomainException($"Invalid column status '{status}'.");
        }
        if (wipLimit.HasValue && wipLimit.Value < 1)
        {
            throw new DomainException("WIP limit must be at least 1 when configured.");
        }

        Id = Guid.NewGuid();
        ProjectId = projectId;
        Status = TaskStatuses.All.First(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        DisplayOrder = displayOrder;
        WipLimit = wipLimit;
        IsStrictWip = isStrictWip;
        IsVisible = isVisible;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(int displayOrder, int? wipLimit, bool isStrictWip, bool isVisible)
    {
        if (wipLimit.HasValue && wipLimit.Value < 1)
        {
            throw new DomainException("WIP limit must be at least 1 when configured.");
        }

        DisplayOrder = displayOrder;
        WipLimit = wipLimit;
        IsStrictWip = isStrictWip;
        IsVisible = isVisible;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
