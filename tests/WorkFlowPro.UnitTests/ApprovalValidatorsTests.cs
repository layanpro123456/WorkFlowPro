using FluentValidation.TestHelper;
using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Application.Features.Approvals.Validators;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class ApprovalValidatorsTests
{
    private readonly CreateWorkflowDefinitionRequestValidator _createDefValidator = new();
    private readonly SubmitApprovalRequestValidator _submitRequestValidator = new();
    private readonly RecordDecisionRequestValidator _recordDecisionValidator = new();
    private readonly CancelApprovalRequestValidator _cancelValidator = new();

    [Fact]
    public void CreateWorkflowDefinition_WithNoSteps_ShouldHaveValidationError()
    {
        var model = new CreateWorkflowDefinitionRequest("PO Approval", "Desc", Array.Empty<WorkflowStepInputDto>());
        var result = _createDefValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Steps);
    }

    [Fact]
    public void CreateWorkflowDefinition_WithNonSequentialSteps_ShouldHaveValidationError()
    {
        var steps = new[]
        {
            new WorkflowStepInputDto(1, "Step 1", "Manager", null),
            new WorkflowStepInputDto(3, "Step 3", "Director", null)
        };
        var model = new CreateWorkflowDefinitionRequest("PO Approval", "Desc", steps);
        var result = _createDefValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Steps);
    }

    [Fact]
    public void CreateWorkflowDefinition_WithBothRoleAndUser_ShouldHaveValidationError()
    {
        var steps = new[]
        {
            new WorkflowStepInputDto(1, "Step 1", "Manager", Guid.NewGuid())
        };
        var model = new CreateWorkflowDefinitionRequest("PO Approval", "Desc", steps);
        var result = _createDefValidator.TestValidate(model);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void CreateWorkflowDefinition_WithValidSteps_ShouldPass()
    {
        var steps = new[]
        {
            new WorkflowStepInputDto(1, "Step 1", "Manager", null),
            new WorkflowStepInputDto(2, "Step 2", null, Guid.NewGuid())
        };
        var model = new CreateWorkflowDefinitionRequest("PO Approval", "Desc", steps);
        var result = _createDefValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SubmitApprovalRequest_EmptyFields_ShouldFail()
    {
        var model = new SubmitApprovalRequest(Guid.Empty, "", "", null);
        var result = _submitRequestValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.WorkflowDefinitionId);
        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void RecordDecisionRequest_InvalidDecision_ShouldFail()
    {
        var model = new RecordDecisionRequest("Maybe", "Notes", 1);
        var result = _recordDecisionValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Decision);
    }

    [Fact]
    public void RecordDecisionRequest_ValidApproved_ShouldPass()
    {
        var model = new RecordDecisionRequest("Approved", "Looks good", 1);
        var result = _recordDecisionValidator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CancelApprovalRequest_ZeroVersion_ShouldFail()
    {
        var model = new CancelApprovalRequest(0);
        var result = _cancelValidator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ExpectedVersion);
    }
}
