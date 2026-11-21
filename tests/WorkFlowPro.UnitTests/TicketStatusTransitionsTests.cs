using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class TicketStatusTransitionsTests
{
    [Theory]
    // New transitions
    [InlineData(TicketStatuses.New, TicketStatuses.Assigned, true)]
    [InlineData(TicketStatuses.New, TicketStatuses.InProgress, true)]
    [InlineData(TicketStatuses.New, TicketStatuses.Closed, true)]
    [InlineData(TicketStatuses.New, TicketStatuses.WaitingForRequester, false)]
    [InlineData(TicketStatuses.New, TicketStatuses.Resolved, false)]
    // Assigned transitions
    [InlineData(TicketStatuses.Assigned, TicketStatuses.InProgress, true)]
    [InlineData(TicketStatuses.Assigned, TicketStatuses.WaitingForRequester, true)]
    [InlineData(TicketStatuses.Assigned, TicketStatuses.Resolved, true)]
    [InlineData(TicketStatuses.Assigned, TicketStatuses.Closed, true)]
    [InlineData(TicketStatuses.Assigned, TicketStatuses.New, false)]
    // InProgress transitions
    [InlineData(TicketStatuses.InProgress, TicketStatuses.WaitingForRequester, true)]
    [InlineData(TicketStatuses.InProgress, TicketStatuses.Resolved, true)]
    [InlineData(TicketStatuses.InProgress, TicketStatuses.Closed, true)]
    [InlineData(TicketStatuses.InProgress, TicketStatuses.New, false)]
    [InlineData(TicketStatuses.InProgress, TicketStatuses.Assigned, false)]
    // WaitingForRequester transitions
    [InlineData(TicketStatuses.WaitingForRequester, TicketStatuses.InProgress, true)]
    [InlineData(TicketStatuses.WaitingForRequester, TicketStatuses.Resolved, true)]
    [InlineData(TicketStatuses.WaitingForRequester, TicketStatuses.Closed, true)]
    [InlineData(TicketStatuses.WaitingForRequester, TicketStatuses.New, false)]
    // Resolved transitions
    [InlineData(TicketStatuses.Resolved, TicketStatuses.Closed, true)]
    [InlineData(TicketStatuses.Resolved, TicketStatuses.InProgress, true)]
    [InlineData(TicketStatuses.Resolved, TicketStatuses.New, false)]
    [InlineData(TicketStatuses.Resolved, TicketStatuses.WaitingForRequester, false)]
    // Closed transitions
    [InlineData(TicketStatuses.Closed, TicketStatuses.InProgress, true)]
    [InlineData(TicketStatuses.Closed, TicketStatuses.New, false)]
    [InlineData(TicketStatuses.Closed, TicketStatuses.Resolved, false)]
    public void IsValidTransition_ValidatesAccordingToPolicy(string from, string to, bool expectedValid)
    {
        var isValid = TicketStatusTransitions.IsValidTransition(from, to);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData(TicketStatuses.New)]
    [InlineData(TicketStatuses.Assigned)]
    [InlineData(TicketStatuses.InProgress)]
    [InlineData(TicketStatuses.WaitingForRequester)]
    [InlineData(TicketStatuses.Resolved)]
    [InlineData(TicketStatuses.Closed)]
    public void IsValidTransition_SelfTransition_ReturnsFalse(string status)
    {
        var isValid = TicketStatusTransitions.IsValidTransition(status, status);
        Assert.False(isValid);
    }

    [Fact]
    public void EnsureValidTransition_InvalidTransition_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TicketStatusTransitions.EnsureValidTransition(TicketStatuses.New, TicketStatuses.Resolved));

        Assert.Contains("Invalid status transition", ex.Message);
    }
}
