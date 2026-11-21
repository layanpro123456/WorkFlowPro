using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Application.Features.Tasks.Validators;
using WorkFlowPro.Domain.Constants;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class TaskValidatorsTests
{
    private readonly CreateTaskRequestValidator _createValidator = new();
    private readonly UpdateTaskRequestValidator _updateValidator = new();
    private readonly ChangeTaskStatusRequestValidator _statusValidator = new();
    private readonly AddTaskCommentRequestValidator _commentValidator = new();

    [Fact]
    public void CreateTaskRequestValidator_ValidRequest_Passes()
    {
        var request = new CreateTaskRequest(
            Title: "Setup CI Pipeline",
            Description: "GitHub Actions workflow",
            Priority: TaskPriorities.High,
            AssigneeId: Guid.NewGuid(),
            DueAtUtc: DateTime.UtcNow.AddDays(7),
            EstimateMinutes: 60
        );

        var result = _createValidator.Validate(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateTaskRequestValidator_EmptyTitle_Fails(string title)
    {
        var request = new CreateTaskRequest(title, null, TaskPriorities.Normal, null, null, null);
        var result = _createValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
    }

    [Fact]
    public void CreateTaskRequestValidator_InvalidPriority_Fails()
    {
        var request = new CreateTaskRequest("Valid Title", null, "InvalidPriority", null, null, null);
        var result = _createValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Priority");
    }

    [Fact]
    public void CreateTaskRequestValidator_NegativeEstimate_Fails()
    {
        var request = new CreateTaskRequest("Valid Title", null, TaskPriorities.Low, null, null, -15);
        var result = _createValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EstimateMinutes");
    }

    [Fact]
    public void UpdateTaskRequestValidator_InvalidVersion_Fails()
    {
        var request = new UpdateTaskRequest("Valid Title", null, TaskPriorities.Normal, null, null, null, Version: 0);
        var result = _updateValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Version");
    }

    [Fact]
    public void ChangeTaskStatusRequestValidator_ValidRequest_Passes()
    {
        var request = new ChangeTaskStatusRequest(TaskStatuses.InProgress, Version: 1, Comment: "Starting work");
        var result = _statusValidator.Validate(request);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ChangeTaskStatusRequestValidator_InvalidStatus_Fails()
    {
        var request = new ChangeTaskStatusRequest("NonExistentStatus", Version: 1);
        var result = _statusValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Status");
    }

    [Fact]
    public void AddTaskCommentRequestValidator_EmptyBody_Fails()
    {
        var request = new AddTaskCommentRequest("   ");
        var result = _commentValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Body");
    }
}
