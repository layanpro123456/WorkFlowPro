using System.Text.RegularExpressions;
using FluentValidation;
using WorkFlowPro.Application.Features.Git.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Git.Validators;

public class ConnectGitRepositoryRequestValidator : AbstractValidator<ConnectGitRepositoryRequest>
{
    private static readonly Regex OwnerOrRepoRegex = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    public ConnectGitRepositoryRequestValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required.")
            .Must(GitProviders.IsSupported)
            .WithMessage($"Unsupported Git provider. Allowed: {string.Join(", ", GitProviders.All)}");

        RuleFor(x => x.RepositoryOwner)
            .NotEmpty().WithMessage("Repository owner is required.")
            .MaximumLength(100)
            .Matches(OwnerOrRepoRegex).WithMessage("Repository owner contains invalid characters.");

        RuleFor(x => x.RepositoryName)
            .NotEmpty().WithMessage("Repository name is required.")
            .MaximumLength(100)
            .Matches(OwnerOrRepoRegex).WithMessage("Repository name contains invalid characters.");

        RuleFor(x => x.DefaultBranch)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultBranch));

        RuleFor(x => x.ApiBaseUrl)
            .Must(BeValidHttpsUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.ApiBaseUrl))
            .WithMessage("API base URL must be a valid HTTPS URL.");

        RuleFor(x => x.WebBaseUrl)
            .Must(BeValidHttpsUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.WebBaseUrl))
            .WithMessage("Web base URL must be a valid HTTPS URL.");
    }

    internal static bool BeValidHttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
               uri.Scheme == Uri.UriSchemeHttps &&
               !string.IsNullOrWhiteSpace(uri.Host);
    }
}

public class UpdateGitRepositoryRequestValidator : AbstractValidator<UpdateGitRepositoryRequest>
{
    public UpdateGitRepositoryRequestValidator()
    {
        RuleFor(x => x.DefaultBranch)
            .NotEmpty().WithMessage("Default branch is required.")
            .MaximumLength(100);

        RuleFor(x => x.ApiBaseUrl)
            .Must(ConnectGitRepositoryRequestValidator.BeValidHttpsUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.ApiBaseUrl))
            .WithMessage("API base URL must be a valid HTTPS URL.");

        RuleFor(x => x.WebBaseUrl)
            .Must(ConnectGitRepositoryRequestValidator.BeValidHttpsUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.WebBaseUrl))
            .WithMessage("Web base URL must be a valid HTTPS URL.");
    }
}

public class CreateTaskBranchRequestValidator : AbstractValidator<CreateTaskBranchRequest>
{
    private static readonly Regex InvalidBranchChars = new(@"[\s~^:?*\[\]\\]|(\.\.)|(//)", RegexOptions.Compiled);

    public CreateTaskBranchRequestValidator()
    {
        RuleFor(x => x.BranchName)
            .MaximumLength(120)
            .Must(IsValidBranchName)
            .When(x => !string.IsNullOrWhiteSpace(x.BranchName))
            .WithMessage("Branch name is invalid. Avoid spaces, '..', or special Git ref characters.");

        RuleFor(x => x.BaseBranch)
            .MaximumLength(100)
            .Must(IsValidBranchName)
            .When(x => !string.IsNullOrWhiteSpace(x.BaseBranch))
            .WithMessage("Base branch name is invalid.");

        RuleFor(x => x.BranchPrefix)
            .MaximumLength(30)
            .Matches("^[a-z0-9-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.BranchPrefix))
            .WithMessage("Branch prefix must contain only lowercase letters, digits, or hyphens.");
    }

    private static bool IsValidBranchName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = name.Trim();
        if (trimmed.StartsWith('-') || trimmed.StartsWith('/') || trimmed.EndsWith('/') || trimmed.EndsWith('.') || trimmed.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return !InvalidBranchChars.IsMatch(trimmed);
    }
}

public class LinkPullRequestRequestValidator : AbstractValidator<LinkPullRequestRequest>
{
    public LinkPullRequestRequestValidator()
    {
        RuleFor(x => x.RepositoryId)
            .NotEmpty().WithMessage("RepositoryId is required.");

        RuleFor(x => x.PrNumber)
            .GreaterThan(0).WithMessage("Pull request number must be greater than zero.");
    }
}
