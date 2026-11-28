using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class TaskStatusTransitionsTests
{
    [Theory]
    // Backlog transitions
    [InlineData(TaskStatuses.Backlog, TaskStatuses.ToDo, true)]
    [InlineData(TaskStatuses.Backlog, TaskStatuses.Cancelled, true)]
    [InlineData(TaskStatuses.Backlog, TaskStatuses.InProgress, false)]
    [InlineData(TaskStatuses.Backlog, TaskStatuses.Blocked, false)]
    [InlineData(TaskStatuses.Backlog, TaskStatuses.Done, false)]
    // ToDo transitions
    [InlineData(TaskStatuses.ToDo, TaskStatuses.InProgress, true)]
    [InlineData(TaskStatuses.ToDo, TaskStatuses.Backlog, true)]
    [InlineData(TaskStatuses.ToDo, TaskStatuses.Cancelled, true)]
    [InlineData(TaskStatuses.ToDo, TaskStatuses.Blocked, false)]
    [InlineData(TaskStatuses.ToDo, TaskStatuses.Done, false)]
    // InProgress transitions
    [InlineData(TaskStatuses.InProgress, TaskStatuses.InReview, true)]
    [InlineData(TaskStatuses.InProgress, TaskStatuses.Blocked, true)]
    [InlineData(TaskStatuses.InProgress, TaskStatuses.Done, true)]
    [InlineData(TaskStatuses.InProgress, TaskStatuses.ToDo, true)]
    [InlineData(TaskStatuses.InProgress, TaskStatuses.Cancelled, true)]
    [InlineData(TaskStatuses.InProgress, TaskStatuses.Backlog, false)]
    // InReview transitions
    [InlineData(TaskStatuses.InReview, TaskStatuses.Done, true)]
    [InlineData(TaskStatuses.InReview, TaskStatuses.InProgress, true)]
    [InlineData(TaskStatuses.InReview, TaskStatuses.Blocked, true)]
    [InlineData(TaskStatuses.InReview, TaskStatuses.Cancelled, true)]
    [InlineData(TaskStatuses.InReview, TaskStatuses.Backlog, false)]
    [InlineData(TaskStatuses.InReview, TaskStatuses.ToDo, false)]
    // Blocked transitions
    [InlineData(TaskStatuses.Blocked, TaskStatuses.InProgress, true)]
    [InlineData(TaskStatuses.Blocked, TaskStatuses.ToDo, true)]
    [InlineData(TaskStatuses.Blocked, TaskStatuses.Cancelled, true)]
    [InlineData(TaskStatuses.Blocked, TaskStatuses.Done, false)]
    [InlineData(TaskStatuses.Blocked, TaskStatuses.Backlog, false)]
    // Done transitions (reopening)
    [InlineData(TaskStatuses.Done, TaskStatuses.InProgress, true)]
    [InlineData(TaskStatuses.Done, TaskStatuses.ToDo, true)]
    [InlineData(TaskStatuses.Done, TaskStatuses.Backlog, false)]
    [InlineData(TaskStatuses.Done, TaskStatuses.Blocked, false)]
    [InlineData(TaskStatuses.Done, TaskStatuses.Cancelled, false)]
    // Cancelled transitions (reviving)
    [InlineData(TaskStatuses.Cancelled, TaskStatuses.Backlog, true)]
    [InlineData(TaskStatuses.Cancelled, TaskStatuses.ToDo, true)]
    [InlineData(TaskStatuses.Cancelled, TaskStatuses.InProgress, false)]
    [InlineData(TaskStatuses.Cancelled, TaskStatuses.Blocked, false)]
    [InlineData(TaskStatuses.Cancelled, TaskStatuses.Done, false)]
    public void IsValidTransition_ValidatesAccordingToPolicy(string from, string to, bool expectedValid)
    {
        var isValid = TaskStatusTransitions.IsValidTransition(from, to);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData(TaskStatuses.Backlog)]
    [InlineData(TaskStatuses.ToDo)]
    [InlineData(TaskStatuses.InProgress)]
    [InlineData(TaskStatuses.InReview)]
    [InlineData(TaskStatuses.Blocked)]
    [InlineData(TaskStatuses.Done)]
    [InlineData(TaskStatuses.Cancelled)]
    public void IsValidTransition_SelfTransition_ReturnsFalse(string status)
    {
        var isValid = TaskStatusTransitions.IsValidTransition(status, status);
        Assert.False(isValid);
    }

    [Fact]
    public void EnsureValidTransition_ValidTransition_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            TaskStatusTransitions.EnsureValidTransition(TaskStatuses.Backlog, TaskStatuses.ToDo));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureValidTransition_InvalidTransition_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TaskStatusTransitions.EnsureValidTransition(TaskStatuses.Backlog, TaskStatuses.Done));

        Assert.Contains("Invalid status transition", ex.Message);
    }

    [Fact]
    public void EnsureValidTransition_SelfTransition_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TaskStatusTransitions.EnsureValidTransition(TaskStatuses.InProgress, TaskStatuses.InProgress));

        Assert.Contains("already in status", ex.Message);
    }

    [Fact]
    public void EnsureValidTransition_UnknownStatus_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TaskStatusTransitions.EnsureValidTransition(TaskStatuses.Backlog, "UnknownStatus"));

        Assert.Contains("Invalid target status", ex.Message);
    }
}
