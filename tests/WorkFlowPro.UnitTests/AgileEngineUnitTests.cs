using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class AgileEngineUnitTests
{
    [Fact]
    public void TaskItem_EpicWithParentOrEpic_ThrowsDomainException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            new TaskItem(projectId, "Epic 1", null, userId, issueType: IssueTypes.Epic, epicId: Guid.NewGuid()));

        Assert.Throws<DomainException>(() =>
            new TaskItem(projectId, "Epic 2", null, userId, issueType: IssueTypes.Epic, parentTaskId: Guid.NewGuid()));
    }

    [Fact]
    public void TaskItem_SubtaskWithoutParent_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new TaskItem(Guid.NewGuid(), "Orphan Subtask", null, Guid.NewGuid(), issueType: IssueTypes.Subtask));
    }

    [Fact]
    public void TaskItem_NegativeStoryPoints_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new TaskItem(Guid.NewGuid(), "Story", null, Guid.NewGuid(), issueType: IssueTypes.Story, storyPoints: -5));
    }

    [Fact]
    public void TaskItem_MoveOnBoard_SameColumnUpdatesRank_CrossColumnTransitionsStatus()
    {
        var userId = Guid.NewGuid();
        var task = new TaskItem(Guid.NewGuid(), "Board Card", null, userId, status: TaskStatuses.ToDo, boardRank: 100);

        var sameColHistory = task.MoveOnBoard(TaskStatuses.ToDo, 300, userId, expectedVersion: 1);
        Assert.Null(sameColHistory);
        Assert.Equal(300, task.BoardRank);
        Assert.Equal(2, task.Version);

        var crossColHistory = task.MoveOnBoard(TaskStatuses.InProgress, 100, userId, expectedVersion: 2, comment: "Dragged");
        Assert.NotNull(crossColHistory);
        Assert.Equal(TaskStatuses.InProgress, task.Status);
        Assert.Equal(100, task.BoardRank);
        Assert.Equal(3, task.Version);
    }

    [Fact]
    public void Sprint_LifecycleInvariants_AreEnforced()
    {
        var sprint = new Sprint(Guid.NewGuid(), "Sprint 1", Guid.NewGuid(), "Deliver Agile");
        Assert.Equal(SprintStatuses.Planned, sprint.Status);

        var now = DateTime.UtcNow;
        Assert.Throws<DomainException>(() => sprint.Start(now, now.AddDays(-1), now));
        Assert.Throws<DomainException>(() => sprint.Complete(now));

        sprint.Start(now, now.AddDays(14), now);
        Assert.Equal(SprintStatuses.Active, sprint.Status);
        Assert.Throws<DomainException>(() => sprint.Start(now, now.AddDays(14), now));

        sprint.Complete(now.AddDays(14));
        Assert.Equal(SprintStatuses.Closed, sprint.Status);
        Assert.Throws<DomainException>(() => sprint.UpdateDetails("Renamed", null, null, null));
    }

    [Fact]
    public void IssueLink_And_WipConfig_EnforceInvariants()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        Assert.Throws<DomainException>(() => new IssueLink(id, id, IssueLinkTypes.Blocks, userId));
        Assert.Throws<DomainException>(() => new IssueLink(id, Guid.NewGuid(), "InvalidType", userId));
        Assert.Throws<DomainException>(() => new ProjectBoardColumnConfig(Guid.NewGuid(), TaskStatuses.InProgress, 2, wipLimit: 0));
    }
}
