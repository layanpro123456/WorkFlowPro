using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class Sprint : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Goal { get; private set; }
    public string Status { get; private set; } = SprintStatuses.Planned;
    public DateTime? StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid CreatedById { get; private set; }

    public Project? Project { get; private set; }

    private Sprint() { } // For EF Core

    public Sprint(
        Guid projectId,
        string name,
        Guid createdById,
        string? goal = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        if (createdById == Guid.Empty) throw new ArgumentException("CreatedById cannot be empty.", nameof(createdById));
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Sprint name is required.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > 120) throw new DomainException("Sprint name cannot exceed 120 characters.");

        if (!string.IsNullOrWhiteSpace(goal) && goal.Trim().Length > 500)
        {
            throw new DomainException("Sprint goal cannot exceed 500 characters.");
        }

        ValidateDates(startDateUtc, endDateUtc);

        Id = Guid.NewGuid();
        ProjectId = projectId;
        Name = trimmedName;
        Goal = goal?.Trim();
        Status = SprintStatuses.Planned;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        CreatedById = createdById;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? goal, DateTime? startDateUtc, DateTime? endDateUtc)
    {
        if (Status == SprintStatuses.Closed)
        {
            throw new DomainException("Closed sprints cannot be modified.");
        }
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Sprint name is required.");
        if (name.Trim().Length > 120) throw new DomainException("Sprint name cannot exceed 120 characters.");
        if (!string.IsNullOrWhiteSpace(goal) && goal.Trim().Length > 500)
        {
            throw new DomainException("Sprint goal cannot exceed 500 characters.");
        }

        ValidateDates(startDateUtc, endDateUtc);

        Name = name.Trim();
        Goal = goal?.Trim();
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Start(DateTime startDateUtc, DateTime endDateUtc, DateTime utcNow)
    {
        if (Status != SprintStatuses.Planned)
        {
            throw new DomainException($"Only planned sprints can be started (current status: {Status}).");
        }
        if (endDateUtc <= startDateUtc)
        {
            throw new DomainException("Sprint end date must be after start date.");
        }

        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        StartedAtUtc = utcNow;
        Status = SprintStatuses.Active;
        UpdatedAtUtc = utcNow;
    }

    public void Complete(DateTime utcNow)
    {
        if (Status != SprintStatuses.Active)
        {
            throw new DomainException($"Only active sprints can be completed (current status: {Status}).");
        }

        Status = SprintStatuses.Closed;
        CompletedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    private static void ValidateDates(DateTime? startDateUtc, DateTime? endDateUtc)
    {
        if (startDateUtc.HasValue && endDateUtc.HasValue && endDateUtc.Value <= startDateUtc.Value)
        {
            throw new DomainException("Sprint end date must be after start date.");
        }
    }
}
