using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class ProjectMember
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public string ProjectRole { get; private set; } = ProjectRoles.Member;
    public DateTime JoinedAtUtc { get; private set; }

    public Project? Project { get; private set; }

    private ProjectMember() { } // For EF Core

    public ProjectMember(Guid projectId, Guid userId, string projectRole)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (!ProjectRoles.All.Contains(projectRole))
        {
            throw new DomainException($"Invalid project role: '{projectRole}'. Allowed roles: {string.Join(", ", ProjectRoles.All)}");
        }

        ProjectId = projectId;
        UserId = userId;
        ProjectRole = projectRole;
        JoinedAtUtc = DateTime.UtcNow;
    }

    public void UpdateRole(string newRole)
    {
        if (!ProjectRoles.All.Contains(newRole))
        {
            throw new DomainException($"Invalid project role: '{newRole}'. Allowed roles: {string.Join(", ", ProjectRoles.All)}");
        }
        ProjectRole = newRole;
    }
}
