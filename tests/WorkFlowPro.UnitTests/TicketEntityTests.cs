using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class TicketEntityTests
{
    [Fact]
    public void SupportTicket_WhenCreated_HasStatusNewAndInitialStatusHistory()
    {
        var requesterId = Guid.NewGuid();
        var ticket = new SupportTicket(
            requesterId: requesterId,
            subject: "VPN Connection Failure",
            description: "Cannot connect to EU VPN gateway since morning.",
            category: TicketCategories.Technical,
            priority: TicketPriorities.High
        );

        Assert.Equal(requesterId, ticket.RequesterId);
        Assert.Equal("VPN Connection Failure", ticket.Subject);
        Assert.Equal("Cannot connect to EU VPN gateway since morning.", ticket.Description);
        Assert.Equal(TicketCategories.Technical, ticket.Category);
        Assert.Equal(TicketPriorities.High, ticket.Priority);
        Assert.Equal(TicketStatuses.New, ticket.Status);
        Assert.Null(ticket.AssigneeId);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
        Assert.Empty(ticket.Messages);

        Assert.Single(ticket.StatusHistory);
        var initial = ticket.StatusHistory.First();
        Assert.Null(initial.FromStatus);
        Assert.Equal(TicketStatuses.New, initial.ToStatus);
        Assert.Equal(requesterId, initial.ChangedById);
        Assert.Equal("Ticket created.", initial.Comment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SupportTicket_EmptySubject_ThrowsDomainException(string subject)
    {
        Assert.Throws<DomainException>(() =>
            new SupportTicket(Guid.NewGuid(), subject, "Desc", TicketCategories.General));
    }

    [Fact]
    public void SupportTicket_SubjectExceeding200Chars_ThrowsDomainException()
    {
        var longSubject = new string('X', 201);
        var ex = Assert.Throws<DomainException>(() =>
            new SupportTicket(Guid.NewGuid(), longSubject, "Desc", TicketCategories.General));

        Assert.Contains("cannot exceed 200", ex.Message);
    }

    [Fact]
    public void SupportTicket_InvalidCategory_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new SupportTicket(Guid.NewGuid(), "Subject", "Desc", "InvalidCategory"));

        Assert.Contains("Invalid category", ex.Message);
    }

    [Fact]
    public void SupportTicket_InvalidPriority_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new SupportTicket(Guid.NewGuid(), "Subject", "Desc", TicketCategories.General, priority: "InvalidPriority"));

        Assert.Contains("Invalid priority", ex.Message);
    }

    [Fact]
    public void SupportTicket_Assign_WhenStatusIsNew_TransitionsToAssignedAndAppendsHistory()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), "Issue", "Desc", TicketCategories.Technical);
        var agentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var history = ticket.Assign(agentId, managerId, "Assigned to network engineer");

        Assert.NotNull(history);
        Assert.Equal(agentId, ticket.AssigneeId);
        Assert.Equal(TicketStatuses.Assigned, ticket.Status);
        Assert.Equal(2, ticket.StatusHistory.Count);

        var latestHistory = ticket.StatusHistory.Last();
        Assert.Equal(TicketStatuses.New, latestHistory.FromStatus);
        Assert.Equal(TicketStatuses.Assigned, latestHistory.ToStatus);
        Assert.Equal(managerId, latestHistory.ChangedById);
        Assert.Equal("Assigned to network engineer", latestHistory.Comment);
    }

    [Fact]
    public void SupportTicket_ChangeStatus_ToResolved_SetsResolvedAtUtc()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), "Issue", "Desc", TicketCategories.Technical);
        ticket.Assign(Guid.NewGuid(), Guid.NewGuid());
        ticket.ChangeStatus(TicketStatuses.InProgress, Guid.NewGuid());

        var agentId = Guid.NewGuid();
        ticket.ChangeStatus(TicketStatuses.Resolved, agentId, "Fixed gateway DNS");

        Assert.Equal(TicketStatuses.Resolved, ticket.Status);
        Assert.NotNull(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    [Fact]
    public void SupportTicket_ChangeStatus_ToClosed_SetsClosedAtUtc()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), "Issue", "Desc", TicketCategories.Technical);
        ticket.Assign(Guid.NewGuid(), Guid.NewGuid());
        ticket.ChangeStatus(TicketStatuses.InProgress, Guid.NewGuid());
        ticket.ChangeStatus(TicketStatuses.Resolved, Guid.NewGuid());

        ticket.ChangeStatus(TicketStatuses.Closed, Guid.NewGuid(), "Requester verified fix");

        Assert.Equal(TicketStatuses.Closed, ticket.Status);
        Assert.NotNull(ticket.ClosedAtUtc);
    }

    [Fact]
    public void SupportTicket_Reopen_FromClosed_ClearsResolutionTimestamps()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), "Issue", "Desc", TicketCategories.Technical);
        ticket.Assign(Guid.NewGuid(), Guid.NewGuid());
        ticket.ChangeStatus(TicketStatuses.InProgress, Guid.NewGuid());
        ticket.ChangeStatus(TicketStatuses.Resolved, Guid.NewGuid());
        ticket.ChangeStatus(TicketStatuses.Closed, Guid.NewGuid());

        Assert.NotNull(ticket.ResolvedAtUtc);
        Assert.NotNull(ticket.ClosedAtUtc);

        ticket.ChangeStatus(TicketStatuses.InProgress, Guid.NewGuid(), "Problem reoccurred");

        Assert.Equal(TicketStatuses.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    [Fact]
    public void SupportTicket_ChangeStatus_InvalidTransition_ThrowsDomainException()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), "Issue", "Desc", TicketCategories.Technical);

        // Cannot transition directly from New to Resolved
        var ex = Assert.Throws<DomainException>(() =>
            ticket.ChangeStatus(TicketStatuses.Resolved, Guid.NewGuid()));

        Assert.Contains("Invalid status transition", ex.Message);
        Assert.Equal(TicketStatuses.New, ticket.Status);
    }

    [Fact]
    public void SupportTicket_AddMessage_AddsPublicAndInternalMessages()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), "Issue", "Desc", TicketCategories.Technical);
        var authorId = Guid.NewGuid();

        var msg1 = ticket.AddMessage(authorId, "Public reply to user", isInternalNote: false);
        var msg2 = ticket.AddMessage(authorId, "Internal investigation note", isInternalNote: true);

        Assert.Equal(2, ticket.Messages.Count);
        Assert.False(msg1.IsInternalNote);
        Assert.True(msg2.IsInternalNote);
        Assert.Equal("Public reply to user", msg1.Body);
        Assert.Equal("Internal investigation note", msg2.Body);
    }
}
