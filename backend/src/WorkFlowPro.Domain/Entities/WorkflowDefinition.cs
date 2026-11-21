using WorkFlowPro.Domain.Common;

namespace WorkFlowPro.Domain.Entities;

public class WorkflowDefinition : BaseEntity
{
    private readonly List<WorkflowStep> _steps = new();

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedById { get; private set; }

    public IReadOnlyCollection<WorkflowStep> Steps => _steps.AsReadOnly();

    // Private constructor for EF Core
    private WorkflowDefinition() { }

    public WorkflowDefinition(
        string name,
        string? description,
        Guid createdById,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name cannot be empty.", nameof(name));

        if (createdById == Guid.Empty)
            throw new ArgumentException("CreatedById cannot be empty.", nameof(createdById));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CreatedById = createdById;
        IsActive = isActive;
    }

    public void Update(string name, string? description, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name cannot be empty.", nameof(name));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = isActive;
    }

    public void SetSteps(IEnumerable<WorkflowStep> steps)
    {
        var stepList = steps.OrderBy(s => s.StepOrder).ToList();
        if (stepList.Count == 0)
        {
            throw new InvalidOperationException("A workflow definition must have at least one step.");
        }

        // Validate step orders are sequential starting from 1: 1, 2, 3...
        for (int i = 0; i < stepList.Count; i++)
        {
            var expectedOrder = i + 1;
            if (stepList[i].StepOrder != expectedOrder)
            {
                throw new InvalidOperationException(
                    $"Workflow steps must have sequential orders starting from 1 with no gaps. Expected step {expectedOrder}, but got {stepList[i].StepOrder}.");
            }
        }

        _steps.Clear();
        _steps.AddRange(stepList);
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
