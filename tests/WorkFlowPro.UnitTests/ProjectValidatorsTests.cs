using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Application.Features.Projects.Validators;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class ProjectValidatorsTests
{
    private readonly CreateProjectRequestValidator _createValidator = new();
    private readonly UpdateProjectRequestValidator _updateValidator = new();
    private readonly AddProjectMemberRequestValidator _memberValidator = new();

    [Theory]
    [InlineData("", "Project Name", "ProjectCode")]
    [InlineData("INVALID CODE!", "Project Name", "ProjectCode")]
    [InlineData("VALID_CODE", "", "Name")]
    public void CreateValidator_InvalidInput_HasError(string code, string name, string expectedProperty)
    {
        var request = new CreateProjectRequest(code, name, null, null, null);
        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == expectedProperty);
    }

    [Fact]
    public void CreateValidator_TargetDateBeforeStartDate_HasError()
    {
        var request = new CreateProjectRequest("CODE1", "Name", null, new DateOnly(2026, 5, 1), new DateOnly(2026, 1, 1));
        var result = _createValidator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateValidator_InvalidStatus_HasError()
    {
        var request = new UpdateProjectRequest("Name", null, "InvalidStatus", null, null);
        var result = _updateValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Status");
    }

    [Fact]
    public void MemberValidator_InvalidRole_HasError()
    {
        var request = new AddProjectMemberRequest(Guid.NewGuid(), "SuperUser");
        var result = _memberValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ProjectRole");
    }
}
