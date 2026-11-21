using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class Project : BaseEntity
{
    private readonly List<ProjectMember> _members = new();

    public string ProjectCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Status { get; private set; } = ProjectStatuses.Active;
    public DateOnly? StartDate { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }

    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    public bool IsArchived => Status == ProjectStatuses.Archived;

    private Project() { } // For EF Core

    public Project(
        string projectCode,
        string name,
        string? description,
        Guid createdById,
        DateOnly? startDate = null,
        DateOnly? targetDate = null)
    {
        if (string.IsNullOrWhiteSpace(projectCode)) throw new DomainException("Project code is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Project name is required.");
        if (createdById == Guid.Empty) throw new ArgumentException("CreatedById cannot be empty.", nameof(createdById));

        ValidateDates(startDate, targetDate);

        Id = Guid.NewGuid();
        ProjectCode = projectCode.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        CreatedById = createdById;
        StartDate = startDate;
        TargetDate = targetDate;
        Status = ProjectStatuses.Active;
        CreatedAtUtc = DateTime.UtcNow;

        // Automatically assign creator as Owner
        _members.Add(new ProjectMember(Id, createdById, ProjectRoles.Owner));
    }

    public void UpdateDetails(
        string name,
        string? description,
        DateOnly? startDate,
        DateOnly? targetDate,
        string? status = null)
    {
        if (IsArchived)
        {
            throw new DomainException("Archived projects cannot be modified.");
        }

        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Project name is required.");
        ValidateDates(startDate, targetDate);

        Name = name.Trim();
        Description = description?.Trim();
        StartDate = startDate;
        TargetDate = targetDate;
        UpdatedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!ProjectStatuses.All.Contains(status))
            {
                throw new DomainException($"Invalid status: '{status}'. Allowed statuses: {string.Join(", ", ProjectStatuses.All)}");
            }
            if (status == ProjectStatuses.Archived)
            {
                Archive(DateTime.UtcNow);
            }
            else
            {
                Status = status;
            }
        }
    }

    public void Archive(DateTime utcNow)
    {
        if (IsArchived) return;
        Status = ProjectStatuses.Archived;
        ArchivedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void AddMember(Guid userId, string projectRole)
    {
        if (IsArchived)
        {
            throw new DomainException("Cannot add members to an archived project.");
        }

        if (_members.Any(m => m.UserId == userId))
        {
            throw new DomainException("User is already a member of this project.");
        }

        _members.Add(new ProjectMember(Id, userId, projectRole));
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RemoveMember(Guid userId)
    {
        if (IsArchived)
        {
            throw new DomainException("Cannot remove members from an archived project.");
        }

        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
        {
            throw new DomainException("Member not found in this project.");
        }

        if (member.ProjectRole == ProjectRoles.Owner && _members.Count(m => m.ProjectRole == ProjectRoles.Owner) <= 1)
        {
            throw new DomainException("Cannot remove the only project owner.");
        }

        _members.Remove(member);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ValidateDates(DateOnly? startDate, DateOnly? targetDate)
    {
        if (startDate.HasValue && targetDate.HasValue && targetDate.Value < startDate.Value)
        {
            throw new DomainException("Target date cannot be earlier than start date.");
        }
    }
}
