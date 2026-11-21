using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Application.Features.Auth.Validators;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Theory]
    [InlineData("", "Password123!", "Email")]
    [InlineData("notanemail", "Password123!", "Email")]
    [InlineData("user@example.com", "", "Password")]
    public void Validate_InvalidRequest_HasValidationError(string email, string password, string errorProperty)
    {
        var request = new LoginRequest(email, password);
        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == errorProperty);
    }

    [Fact]
    public void Validate_ValidRequest_PassesValidation()
    {
        var request = new LoginRequest("user@workflowpro.local", "ValidPassword123!");
        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
