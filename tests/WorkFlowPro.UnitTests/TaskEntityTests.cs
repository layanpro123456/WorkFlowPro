using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class TaskEntityTests
{
    [Fact]
    public void TaskItem_WhenCreated_HasVersion1AndInitialStatusHistory()
    {
        var projectId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var task = new TaskItem(
            projectId: projectId,
            title: "Implement Auth Flow",
            description: "Build JWT and refresh tokens",
            createdById: creatorId,
            priority: TaskPriorities.High,
            estimateMinutes: 120,
            status: TaskStatuses.Backlog
        );

        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal("Implement Auth Flow", task.Title);
        Assert.Equal("Build JWT and refresh tokens", task.Description);
        Assert.Equal(creatorId, task.CreatedById);
        Assert.Equal(TaskPriorities.High, task.Priority);
        Assert.Equal(TaskStatuses.Backlog, task.Status);
        Assert.Equal(120, task.EstimateMinutes);
        Assert.Equal(1, task.Version);
        Assert.Null(task.AssigneeId);
        Assert.Null(task.UpdatedAtUtc);
        Assert.Empty(task.Comments);

        Assert.Single(task.StatusHistory);
        var initialHistory = task.StatusHistory.First();
        Assert.Null(initialHistory.FromStatus);
        Assert.Equal(TaskStatuses.Backlog, initialHistory.ToStatus);
        Assert.Equal(creatorId, initialHistory.ChangedById);
        Assert.Equal("Task created.", initialHistory.Comment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TaskItem_EmptyTitle_ThrowsDomainException(string emptyTitle)
    {
        Assert.Throws<DomainException>(() =>
            new TaskItem(Guid.NewGuid(), emptyTitle, null, Guid.NewGuid()));
    }

    [Fact]
    public void TaskItem_TitleExceeding200Chars_ThrowsDomainException()
    {
        var longTitle = new string('A', 201);
        var ex = Assert.Throws<DomainException>(() =>
            new TaskItem(Guid.NewGuid(), longTitle, null, Guid.NewGuid()));

        Assert.Contains("cannot exceed 200", ex.Message);
    }

    [Fact]
    public void TaskItem_InvalidPriority_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new TaskItem(Guid.NewGuid(), "Valid Title", null, Guid.NewGuid(), priority: "SuperUrgent"));

        Assert.Contains("Invalid priority", ex.Message);
    }

    [Fact]
    public void TaskItem_NegativeEstimate_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new TaskItem(Guid.NewGuid(), "Valid Title", null, Guid.NewGuid(), estimateMinutes: -30));

        Assert.Contains("cannot be negative", ex.Message);
    }

    [Fact]
    public void TaskItem_UpdateDetails_WithMatchingVersion_IncrementsVersionAndUpdatesFields()
    {
        var task = new TaskItem(Guid.NewGuid(), "Initial Title", null, Guid.NewGuid());
        var newAssignee = Guid.NewGuid();
        var due = DateTime.UtcNow.AddDays(5);

        task.UpdateDetails(
            title: "Updated Title",
            description: "Updated Description",
            priority: TaskPriorities.Urgent,
            assigneeId: newAssignee,
            dueAtUtc: due,
            estimateMinutes: 240,
            expectedVersion: 1
        );

        Assert.Equal("Updated Title", task.Title);
        Assert.Equal("Updated Description", task.Description);
        Assert.Equal(TaskPriorities.Urgent, task.Priority);
        Assert.Equal(newAssignee, task.AssigneeId);
        Assert.Equal(due, task.DueAtUtc);
        Assert.Equal(240, task.EstimateMinutes);
        Assert.Equal(2, task.Version);
        Assert.NotNull(task.UpdatedAtUtc);
    }

    [Fact]
    public void TaskItem_UpdateDetails_WithStaleVersion_ThrowsConcurrencyException()
    {
        var task = new TaskItem(Guid.NewGuid(), "Initial Title", null, Guid.NewGuid());

        var ex = Assert.Throws<ConcurrencyException>(() =>
            task.UpdateDetails("Updated Title", null, TaskPriorities.Normal, null, null, null, expectedVersion: 99));

        Assert.Contains("version conflict", ex.Message);
    }

    [Fact]
    public void TaskItem_ChangeStatus_WithValidTransition_IncrementsVersionAndAppendsHistory()
    {
        var creatorId = Guid.NewGuid();
        var changerId = Guid.NewGuid();
        var task = new TaskItem(Guid.NewGuid(), "Implement UI", null, creatorId, status: TaskStatuses.Backlog);

        task.ChangeStatus(TaskStatuses.ToDo, changerId, expectedVersion: 1, comment: "Moved to sprint board");

        Assert.Equal(TaskStatuses.ToDo, task.Status);
        Assert.Equal(2, task.Version);
        Assert.Equal(2, task.StatusHistory.Count);

        var latestHistory = task.StatusHistory.Last();
        Assert.Equal(TaskStatuses.Backlog, latestHistory.FromStatus);
        Assert.Equal(TaskStatuses.ToDo, latestHistory.ToStatus);
        Assert.Equal(changerId, latestHistory.ChangedById);
        Assert.Equal("Moved to sprint board", latestHistory.Comment);
    }

    [Fact]
    public void TaskItem_ChangeStatus_WithInvalidTransition_ThrowsDomainException()
    {
        var task = new TaskItem(Guid.NewGuid(), "Implement UI", null, Guid.NewGuid(), status: TaskStatuses.Backlog);

        var ex = Assert.Throws<DomainException>(() =>
            task.ChangeStatus(TaskStatuses.Done, Guid.NewGuid(), expectedVersion: 1));

        Assert.Contains("Invalid status transition", ex.Message);
        Assert.Equal(1, task.Version);
        Assert.Equal(TaskStatuses.Backlog, task.Status);
    }

    [Fact]
    public void TaskItem_ChangeStatus_ToSameStatus_ThrowsDomainException()
    {
        var task = new TaskItem(Guid.NewGuid(), "Implement UI", null, Guid.NewGuid(), status: TaskStatuses.Backlog);

        var ex = Assert.Throws<DomainException>(() =>
            task.ChangeStatus(TaskStatuses.Backlog, Guid.NewGuid(), expectedVersion: 1));

        Assert.Contains("already in status", ex.Message);
    }

    [Fact]
    public void TaskItem_ChangeStatus_WithStaleVersion_ThrowsConcurrencyException()
    {
        var task = new TaskItem(Guid.NewGuid(), "Implement UI", null, Guid.NewGuid(), status: TaskStatuses.Backlog);

        var ex = Assert.Throws<ConcurrencyException>(() =>
            task.ChangeStatus(TaskStatuses.ToDo, Guid.NewGuid(), expectedVersion: 5));

        Assert.Contains("version conflict", ex.Message);
    }

    [Fact]
    public void TaskItem_AddComment_AppendsCommentToCollection()
    {
        var task = new TaskItem(Guid.NewGuid(), "Task Title", null, Guid.NewGuid());
        var authorId = Guid.NewGuid();

        var comment = task.AddComment(authorId, "First review completed.");

        Assert.NotNull(comment);
        Assert.Equal(authorId, comment.AuthorId);
        Assert.Equal("First review completed.", comment.Body);
        Assert.Single(task.Comments);
        Assert.Equal(comment, task.Comments.First());
    }

    [Fact]
    public void TaskItem_AddComment_WithEmptyBody_ThrowsDomainException()
    {
        var task = new TaskItem(Guid.NewGuid(), "Task Title", null, Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            task.AddComment(Guid.NewGuid(), "   "));
    }
}
