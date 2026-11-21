using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class ApprovalRequestEntityTests
{
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _approver1 = Guid.NewGuid();
    private readonly Guid _approver2 = Guid.NewGuid();
    private readonly Guid _workflowDefId = Guid.NewGuid();

    private ApprovalRequest CreateTwoStepRequest()
    {
        var request = new ApprovalRequest(_workflowDefId, _requesterId, "Travel Budget", "Conference in Seattle");
        request.AddStepSnapshot(1, "Direct Manager", _approver1);
        request.AddStepSnapshot(2, "Department VP", _approver2);
        return request;
    }

    [Fact]
    public void InitialState_IsPendingWithCurrentStepOne()
    {
        var request = CreateTwoStepRequest();
        Assert.Equal(ApprovalRequestStatuses.Pending, request.Status);
        Assert.Equal(1, request.CurrentStepOrder);
        Assert.Equal(1, request.Version);
        Assert.Equal(2, request.Steps.Count);
        Assert.Null(request.CompletedAtUtc);
    }

    [Fact]
    public void ProcessDecision_WrongApprover_ThrowsForbiddenException()
    {
        var request = CreateTwoStepRequest();
        var intruderId = Guid.NewGuid();

        var ex = Assert.Throws<ForbiddenException>(() =>
            request.ProcessDecision(intruderId, ApprovalDecisions.Approved, "LGTM", expectedVersion: 1));

        Assert.Contains("not the designated approver", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessDecision_ConcurrencyConflict_ThrowsConcurrencyException()
    {
        var request = CreateTwoStepRequest();

        Assert.Throws<ConcurrencyException>(() =>
            request.ProcessDecision(_approver1, ApprovalDecisions.Approved, "LGTM", expectedVersion: 99));
    }

    [Fact]
    public void ProcessDecision_Step1Approved_AdvancesToStep2()
    {
        var request = CreateTwoStepRequest();

        var decision = request.ProcessDecision(_approver1, ApprovalDecisions.Approved, "Approved by Manager", expectedVersion: 1);

        Assert.NotNull(decision);
        Assert.Equal(ApprovalDecisions.Approved, decision.Decision);
        Assert.Equal(ApprovalRequestStatuses.Pending, request.Status);
        Assert.Equal(2, request.CurrentStepOrder);
        Assert.Equal(2, request.Version);

        var step1 = request.Steps.First(s => s.StepOrder == 1);
        Assert.Equal(ApprovalStepStatuses.Approved, step1.Status);
        Assert.NotNull(step1.DecidedAtUtc);

        var step2 = request.Steps.First(s => s.StepOrder == 2);
        Assert.Equal(ApprovalStepStatuses.Pending, step2.Status);
    }

    [Fact]
    public void ProcessDecision_AllStepsApproved_CompletesRequest()
    {
        var request = CreateTwoStepRequest();

        // Step 1
        request.ProcessDecision(_approver1, ApprovalDecisions.Approved, "Step 1 OK", expectedVersion: 1);

        // Step 2
        var decision2 = request.ProcessDecision(_approver2, ApprovalDecisions.Approved, "Step 2 OK", expectedVersion: 2);

        Assert.NotNull(decision2);
        Assert.Equal(ApprovalRequestStatuses.Approved, request.Status);
        Assert.Null(request.CurrentStepOrder);
        Assert.NotNull(request.CompletedAtUtc);
        Assert.Equal(3, request.Version);

        Assert.All(request.Steps, s => Assert.Equal(ApprovalStepStatuses.Approved, s.Status));
    }

    [Fact]
    public void ProcessDecision_Step1Rejected_TerminatesRequestAndSkipsStep2()
    {
        var request = CreateTwoStepRequest();

        var decision = request.ProcessDecision(_approver1, ApprovalDecisions.Rejected, "Budget cap exceeded", expectedVersion: 1);

        Assert.NotNull(decision);
        Assert.Equal(ApprovalDecisions.Rejected, decision.Decision);
        Assert.Equal(ApprovalRequestStatuses.Rejected, request.Status);
        Assert.Null(request.CurrentStepOrder);
        Assert.NotNull(request.CompletedAtUtc);
        Assert.Equal(2, request.Version);

        var step1 = request.Steps.First(s => s.StepOrder == 1);
        Assert.Equal(ApprovalStepStatuses.Rejected, step1.Status);

        var step2 = request.Steps.First(s => s.StepOrder == 2);
        Assert.Equal(ApprovalStepStatuses.Skipped, step2.Status);
        Assert.Null(step2.DecidedAtUtc);
    }

    [Fact]
    public void ProcessDecision_AlreadyDecided_ThrowsInvalidOperationException()
    {
        var request = CreateTwoStepRequest();
        request.ProcessDecision(_approver1, ApprovalDecisions.Rejected, "No", expectedVersion: 1);

        // Request is now rejected
        Assert.Throws<InvalidOperationException>(() =>
            request.ProcessDecision(_approver2, ApprovalDecisions.Approved, "Yes", expectedVersion: 2));
    }

    [Fact]
    public void Cancel_SkipsPendingStepsAndMarksCancelled()
    {
        var request = CreateTwoStepRequest();

        request.Cancel(_requesterId, expectedVersion: 1);

        Assert.Equal(ApprovalRequestStatuses.Cancelled, request.Status);
        Assert.Null(request.CurrentStepOrder);
        Assert.NotNull(request.CompletedAtUtc);
        Assert.Equal(2, request.Version);
        Assert.All(request.Steps, s => Assert.Equal(ApprovalStepStatuses.Skipped, s.Status));
    }
}
