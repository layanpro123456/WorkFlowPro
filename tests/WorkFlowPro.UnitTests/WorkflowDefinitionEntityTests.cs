using WorkFlowPro.Domain.Entities;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class WorkflowDefinitionEntityTests
{
    [Fact]
    public void CreateWorkflowStep_WithBothRoleAndUser_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new WorkflowStep(Guid.NewGuid(), 1, "Step 1", "Manager", Guid.NewGuid()));

        Assert.Contains("cannot specify both", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateWorkflowStep_WithNeitherRoleNorUser_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new WorkflowStep(Guid.NewGuid(), 1, "Step 1", null, null));

        Assert.Contains("must specify exactly one approver selector", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateWorkflowStep_WithInvalidStepOrder_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkflowStep(Guid.NewGuid(), 0, "Step 0", "Manager", null));
    }

    [Fact]
    public void SetSteps_WithGapsInOrder_ThrowsInvalidOperationException()
    {
        var defId = Guid.NewGuid();
        var def = new WorkflowDefinition("Expense Approval", "Approve expenses", Guid.NewGuid());
        var steps = new[]
        {
            new WorkflowStep(defId, 1, "Manager Review", "Manager", null),
            new WorkflowStep(defId, 3, "Director Review", "Director", null) // Gap at 2
        };

        var ex = Assert.Throws<InvalidOperationException>(() => def.SetSteps(steps));
        Assert.Contains("sequential orders", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetSteps_ValidSteps_SucceedsAndSetsOrderedSteps()
    {
        var defId = Guid.NewGuid();
        var def = new WorkflowDefinition("Purchase Order", null, Guid.NewGuid());
        var step1 = new WorkflowStep(defId, 1, "Manager", "Manager", null);
        var step2 = new WorkflowStep(defId, 2, "Finance", null, Guid.NewGuid());

        def.SetSteps(new[] { step2, step1 }); // Out of order passed in

        Assert.Equal(2, def.Steps.Count);
        var stepList = def.Steps.ToList();
        Assert.Equal(1, stepList[0].StepOrder);
        Assert.Equal("Manager", stepList[0].Name);
        Assert.Equal(2, stepList[1].StepOrder);
        Assert.Equal("Finance", stepList[1].Name);
    }
}
