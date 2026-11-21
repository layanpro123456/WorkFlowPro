using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class ProjectEntityTests
{
    [Fact]
    public void Project_WhenCreated_HasOwnerMemberAndUppercaseCode()
    {
        var creatorId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 1, 1);
        var targetDate = new DateOnly(2026, 6, 30);

        var project = new Project("wfp-core", "Core Platform", "Initial core platform", creatorId, startDate, targetDate);

        Assert.Equal("WFP-CORE", project.ProjectCode);
        Assert.Equal("Core Platform", project.Name);
        Assert.Equal(ProjectStatuses.Active, project.Status);
        Assert.Equal(startDate, project.StartDate);
        Assert.Equal(targetDate, project.TargetDate);
        Assert.Equal(creatorId, project.CreatedById);
        Assert.False(project.IsArchived);

        Assert.Single(project.Members);
        var owner = project.Members.First();
        Assert.Equal(creatorId, owner.UserId);
        Assert.Equal(ProjectRoles.Owner, owner.ProjectRole);
    }

    [Fact]
    public void Project_TargetDateBeforeStartDate_ThrowsDomainException()
    {
        var creatorId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 6, 1);
        var targetDate = new DateOnly(2026, 1, 1);

        var ex = Assert.Throws<DomainException>(() =>
            new Project("PROJ-1", "Test", null, creatorId, startDate, targetDate));

        Assert.Contains("earlier than start date", ex.Message);
    }

    [Fact]
    public void Project_Archive_SetsStatusAndArchivedTimestamp()
    {
        var project = new Project("PROJ-ARCH", "Archive Me", null, Guid.NewGuid());
        var now = DateTime.UtcNow;

        project.Archive(now);

        Assert.True(project.IsArchived);
        Assert.Equal(ProjectStatuses.Archived, project.Status);
        Assert.Equal(now, project.ArchivedAtUtc);
    }

    [Fact]
    public void Project_UpdateWhenArchived_ThrowsDomainException()
    {
        var project = new Project("PROJ-ARCH", "Archive Me", null, Guid.NewGuid());
        project.Archive(DateTime.UtcNow);

        var ex = Assert.Throws<DomainException>(() =>
            project.UpdateDetails("New Name", null, null, null));

        Assert.Contains("Archived projects cannot be modified", ex.Message);
    }

    [Fact]
    public void Project_CannotRemoveOnlyOwner()
    {
        var ownerId = Guid.NewGuid();
        var project = new Project("PROJ-1", "Owner Test", null, ownerId);

        var ex = Assert.Throws<DomainException>(() => project.RemoveMember(ownerId));

        Assert.Contains("Cannot remove the only project owner", ex.Message);
    }

    [Fact]
    public void Project_AddDuplicateMember_ThrowsDomainException()
    {
        var ownerId = Guid.NewGuid();
        var project = new Project("PROJ-1", "Dup Test", null, ownerId);

        var ex = Assert.Throws<DomainException>(() => project.AddMember(ownerId, ProjectRoles.Member));

        Assert.Contains("already a member", ex.Message);
    }
}
