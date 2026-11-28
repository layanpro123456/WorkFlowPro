using System.Security.Cryptography;
using System.Text;
using WorkFlowPro.Application.Features.Git.Dtos;
using WorkFlowPro.Application.Features.Git.Services;
using WorkFlowPro.Application.Features.Git.Validators;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Infrastructure.Git;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class GitIntegrationUnitTests
{
    [Fact]
    public void TaskItem_AssignTaskKey_SetsNumberAndFormattedKeyOnce()
    {
        var task = new TaskItem(Guid.NewGuid(), "Add OAuth", null, Guid.NewGuid());
        task.AssignTaskKey(12, "prj-alpha");

        Assert.Equal(12, task.TaskNumber);
        Assert.Equal("PRJ-ALPHA-12", task.TaskKey);

        // Idempotent: second assignment does not overwrite
        task.AssignTaskKey(99, "OTHER");
        Assert.Equal(12, task.TaskNumber);
        Assert.Equal("PRJ-ALPHA-12", task.TaskKey);
    }

    [Fact]
    public void TaskItem_ApplyAutomationTransition_WalksShortestValidPathFromBacklogToInReview()
    {
        var actorId = Guid.NewGuid();
        var task = new TaskItem(Guid.NewGuid(), "Implement PR sync", null, actorId, status: TaskStatuses.Backlog);

        var histories = task.ApplyAutomationTransition(TaskStatuses.InReview, actorId, "PR #7 opened");

        Assert.Equal(TaskStatuses.InReview, task.Status);
        Assert.Equal(3, histories.Count);
        Assert.Equal(TaskStatuses.ToDo, histories[0].ToStatus);
        Assert.Equal(TaskStatuses.InProgress, histories[1].ToStatus);
        Assert.Equal(TaskStatuses.InReview, histories[2].ToStatus);
        Assert.Equal("PR #7 opened", histories[2].Comment);
        Assert.Equal(4, task.Version);
    }

    [Fact]
    public void TaskItem_ApplyAutomationTransition_SameStatus_IsNoOp()
    {
        var actorId = Guid.NewGuid();
        var task = new TaskItem(Guid.NewGuid(), "Already in progress", null, actorId, status: TaskStatuses.InProgress);

        var histories = task.ApplyAutomationTransition(TaskStatuses.InProgress, actorId, "Branch created");

        Assert.Empty(histories);
        Assert.Equal(1, task.Version);
    }

    [Fact]
    public void TaskKeyExtractor_ExtractsUniqueUppercaseKeysAcrossInputs()
    {
        var keys = TaskKeyExtractor.ExtractTaskKeys(
            "feat/prj-alpha-12-oauth-flow",
            "Fix login bug (PRJ-ALPHA-12 & prj-beta-4)",
            "Closes PRJ-ALPHA-12 and relates to CORE-99.");

        Assert.Equal(3, keys.Count);
        Assert.Contains("PRJ-ALPHA-12", keys);
        Assert.Contains("PRJ-BETA-4", keys);
        Assert.Contains("CORE-99", keys);
    }

    [Fact]
    public void TaskKeyExtractor_BuildDefaultBranchName_ProducesCleanGitBranch()
    {
        var branch = TaskKeyExtractor.BuildDefaultBranchName("PRJ-ALPHA-5", "Add GitHub Webhooks & PR Sync!", "feat");
        Assert.Equal("feat/PRJ-ALPHA-5-add-github-webhooks-pr-sync", branch);
    }

    [Fact]
    public void GitHubProviderClient_VerifyWebhookSignature_ValidatesHmacSha256Correctly()
    {
        var client = new GitHubProviderClient(new HttpClient());
        const string secret = "super_webhook_secret";
        const string payload = "{\"action\":\"opened\",\"number\":42}";

        var sigBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        var validHeader = "sha256=" + Convert.ToHexString(sigBytes).ToLowerInvariant();

        Assert.True(client.VerifyWebhookSignature(payload, validHeader, secret));
        Assert.False(client.VerifyWebhookSignature(payload, "sha256=deadbeef", secret));
        Assert.False(client.VerifyWebhookSignature(payload, "invalid-format", secret));
        Assert.False(client.VerifyWebhookSignature(payload, validHeader, "wrong_secret"));
    }

    [Fact]
    public void GitHubProviderClient_ParseWebhookEvent_ParsesMergedPullRequest()
    {
        var client = new GitHubProviderClient(new HttpClient());
        const string payload = """
        {
          "action": "closed",
          "repository": { "full_name": "octocat/workflow" },
          "sender": { "login": "octocat" },
          "pull_request": {
            "id": 987654,
            "number": 42,
            "title": "feat(PRJ-ALPHA-2): Connect Git",
            "body": "Closes PRJ-ALPHA-2",
            "state": "closed",
            "draft": false,
            "merged": true,
            "merged_at": "2026-11-28T10:00:00Z",
            "closed_at": "2026-11-28T10:00:00Z",
            "created_at": "2026-11-28T08:00:00Z",
            "html_url": "https://github.com/octocat/workflow/pull/42",
            "head": { "ref": "feat/PRJ-ALPHA-2-connect-git" },
            "base": { "ref": "main" },
            "user": { "login": "octocat", "avatar_url": "https://avatars.example.com/1" },
            "commits": 3,
            "additions": 120,
            "deletions": 15
          }
        }
        """;

        var parsed = client.ParseWebhookEvent("pull_request", payload);

        Assert.NotNull(parsed);
        Assert.Equal("pull_request", parsed.EventType);
        Assert.NotNull(parsed.PullRequest);
        Assert.Equal(42, parsed.PullRequest.PrNumber);
        Assert.Equal(PullRequestStates.Merged, parsed.PullRequest.State);
        Assert.Equal("feat/PRJ-ALPHA-2-connect-git", parsed.PullRequest.SourceBranch);
        Assert.Equal(3, parsed.PullRequest.CommitsCount);
    }

    [Fact]
    public void GitValidators_RejectHttpApiUrlAndInvalidBranchNames()
    {
        var connectValidator = new ConnectGitRepositoryRequestValidator();
        var badUrlReq = new ConnectGitRepositoryRequest(
            "GitHub", "octocat", "repo", "main", "http://169.254.169.254/latest", null, null, null);
        var urlResult = connectValidator.Validate(badUrlReq);
        Assert.False(urlResult.IsValid);
        Assert.Contains(urlResult.Errors, e => e.PropertyName == "ApiBaseUrl");

        var branchValidator = new CreateTaskBranchRequestValidator();
        var badBranchResult = branchValidator.Validate(new CreateTaskBranchRequest(BranchName: "feat/invalid..branch name"));
        Assert.False(badBranchResult.IsValid);
        Assert.Contains(badBranchResult.Errors, e => e.PropertyName == "BranchName");
    }
}
