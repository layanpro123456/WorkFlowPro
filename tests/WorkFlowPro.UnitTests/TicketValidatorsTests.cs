using WorkFlowPro.Application.Features.Tickets.Dtos;
using WorkFlowPro.Application.Features.Tickets.Validators;
using WorkFlowPro.Domain.Constants;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class TicketValidatorsTests
{
    private readonly CreateTicketRequestValidator _createValidator = new();
    private readonly AssignTicketRequestValidator _assignValidator = new();
    private readonly ChangeTicketStatusRequestValidator _statusValidator = new();
    private readonly AddTicketMessageRequestValidator _messageValidator = new();

    [Fact]
    public void CreateTicketRequestValidator_ValidRequest_Passes()
    {
        var request = new CreateTicketRequest(
            Subject: "Hardware replacement",
            Description: "Monitor is flickering and showing artifacts",
            Category: TicketCategories.Hardware,
            Priority: TicketPriorities.Normal
        );

        var result = _createValidator.Validate(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateTicketRequestValidator_EmptySubject_Fails(string subject)
    {
        var request = new CreateTicketRequest(subject, "Description", TicketCategories.General, TicketPriorities.Low);
        var result = _createValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Subject");
    }

    [Fact]
    public void CreateTicketRequestValidator_InvalidCategory_Fails()
    {
        var request = new CreateTicketRequest("Subject", "Description", "UnknownCategory", TicketPriorities.Low);
        var result = _createValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Category");
    }

    [Fact]
    public void AssignTicketRequestValidator_EmptyAssignee_Fails()
    {
        var request = new AssignTicketRequest(Guid.Empty);
        var result = _assignValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "AssigneeId");
    }

    [Fact]
    public void ChangeTicketStatusRequestValidator_InvalidStatus_Fails()
    {
        var request = new ChangeTicketStatusRequest("FakeStatus");
        var result = _statusValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Status");
    }

    [Fact]
    public void AddTicketMessageRequestValidator_EmptyBody_Fails()
    {
        var request = new AddTicketMessageRequest("   ");
        var result = _messageValidator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Body");
    }
}
