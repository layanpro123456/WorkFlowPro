using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Application.Features.Git.Dtos;
using WorkFlowPro.Application.Features.Notifications.Dtos;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure.Git;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class GitIntegrationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _baseFactory;

    public GitIntegrationEndpointsTests(CustomWebApplicationFactory baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private sealed class StubGitHubHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            Requests.Add($"{request.Method} {path}");

            if (request.Method == HttpMethod.Get && path.EndsWith("/repos/octocat/workflowpro"))
            {
                return JsonResponse(HttpStatusCode.OK, new { default_branch = "main", @private = false });
            }

            if (request.Method == HttpMethod.Get && path.Contains("/git/ref/heads/main"))
            {
                return JsonResponse(HttpStatusCode.OK, new { @object = new { sha = "abc123def456" } });
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/git/refs"))
            {
                return JsonResponse(HttpStatusCode.Created, new { @ref = "refs/heads/created" });
            }

            if (request.Method == HttpMethod.Get && path.Contains("/pulls/99"))
            {
                return JsonResponse(HttpStatusCode.OK, new
                {
                    id = 9900,
                    number = 99,
                    title = "Manual PR #99",
                    body = "Manual link test",
                    state = "open",
                    draft = false,
                    created_at = "2026-11-28T09:00:00Z",
                    html_url = "https://github.com/octocat/workflowpro/pull/99",
                    head = new { @ref = "chore/manual-99", sha = "sha99" },
                    base_ = new { @ref = "main" },
                    @base = new { @ref = "main" },
                    user = new { login = "octocat" },
                    commits = 2,
                    additions = 40,
                    deletions = 5
                });
            }

            if (request.Method == HttpMethod.Get && path.Contains("/pulls?"))
            {
                return JsonResponse(HttpStatusCode.OK, Array.Empty<object>());
            }

            if (request.Method == HttpMethod.Get && path.Contains("/check-runs"))
            {
                return JsonResponse(HttpStatusCode.OK, new { check_runs = Array.Empty<object>() });
            }

            if (request.Method == HttpMethod.Get && path.Contains("/reviews"))
            {
                return JsonResponse(HttpStatusCode.OK, Array.Empty<object>());
            }

            return JsonResponse(HttpStatusCode.NotFound, new { message = "Not found" });
        }

        private static Task<HttpResponseMessage> JsonResponse(HttpStatusCode code, object payload)
        {
            var res = new HttpResponseMessage(code)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            return Task.FromResult(res);
        }
    }

    private static async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedClientAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string email,
        string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new ApplicationRole(role));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = email.Split('@')[0],
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await userManager.CreateAsync(user, "Password@123");
            await userManager.AddToRoleAsync(user, role);
        }

        var loginClient = factory.CreateClient();
        var loginRes = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Password@123"));
        loginRes.EnsureSuccessStatusCode();
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, user.Id);
    }

    [Fact]
    public async Task EndToEnd_GitConnect_BranchCreation_SignedWebhook_And_ManualPrLink()
    {
        var handler = new StubGitHubHandler();
        using var factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGitProviderClient>();
                services.AddSingleton<IGitProviderClient>(_ => new GitHubProviderClient(new HttpClient(handler)));
            });
        });

        var (mgrClient, _) = await CreateAuthenticatedClientAsync(factory, "git_mgr@workflowpro.local", Roles.Manager);
        var (devClient, devId) = await CreateAuthenticatedClientAsync(factory, "git_dev@workflowpro.local", Roles.Employee);
        var (outsiderClient, _) = await CreateAuthenticatedClientAsync(factory, "git_out@workflowpro.local", Roles.Employee);

        // 1. Create Project + Add Dev Member
        var projRes = await mgrClient.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("GITPRJ", "Git Integration Project", "Test Git", null, null));
        projRes.EnsureSuccessStatusCode();
        var project = await projRes.Content.ReadFromJsonAsync<ProjectSummaryDto>();
        Assert.NotNull(project);

        await mgrClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/members",
            new AddProjectMemberRequest(devId, ProjectRoles.Member));

        // 2. Member cannot connect Git repo (403 Forbidden); outsider gets 404
        var connectReq = new ConnectGitRepositoryRequest(
            Provider: "GitHub",
            RepositoryOwner: "octocat",
            RepositoryName: "workflowpro",
            DefaultBranch: "main",
            ApiBaseUrl: "https://api.github.com",
            WebBaseUrl: "https://github.com",
            AccessToken: "ghp_secret_token_123",
            WebhookSecret: "my_webhook_secret",
            IsDefault: true,
            AutoLinkEnabled: true,
            AutoTransitionEnabled: true,
            UseInReviewColumn: true);

        var forbiddenRes = await devClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/git/repositories", connectReq);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        var outsiderRes = await outsiderClient.GetAsync($"/api/v1/projects/{project.Id}/git/repositories");
        Assert.Equal(HttpStatusCode.NotFound, outsiderRes.StatusCode);

        // Manager connects repo
        var connectRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/git/repositories", connectReq);
        Assert.Equal(HttpStatusCode.Created, connectRes.StatusCode);
        var connected = await connectRes.Content.ReadFromJsonAsync<ConnectGitRepositoryResponse>();
        Assert.NotNull(connected);
        Assert.True(connected.Repository.HasAccessToken);
        Assert.True(connected.Repository.HasWebhookSecret);
        Assert.Null(connected.GeneratedWebhookSecret);

        // Ensure token/secret never leaked in response JSON
        var rawRepoJson = await connectRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ghp_secret_token_123", rawRepoJson);
        Assert.DoesNotContain("my_webhook_secret", rawRepoJson);

        // 3. Test Connection
        var testRes = await mgrClient.PostAsync($"/api/v1/projects/{project.Id}/git/repositories/{connected.Repository.Id}/test", null);
        testRes.EnsureSuccessStatusCode();
        var testDto = await testRes.Content.ReadFromJsonAsync<GitConnectionTestResultDto>();
        Assert.True(testDto!.Success);
        Assert.Equal("main", testDto.DetectedDefaultBranch);

        // 4. Create Task -> gets sequential TaskKey GITPRJ-1
        var taskRes = await devClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/tasks",
            new CreateTaskRequest("Add OAuth Login", "Support GitHub OAuth", TaskPriorities.High, devId, null, 120));
        taskRes.EnsureSuccessStatusCode();
        var task = await taskRes.Content.ReadFromJsonAsync<TaskSummaryDto>();
        Assert.NotNull(task);
        Assert.Equal(1, task.TaskNumber);
        Assert.Equal("GITPRJ-1", task.TaskKey);

        // 5. Create Git Branch directly from Kanban card -> moves Backlog -> ToDo -> InProgress
        var branchRes = await devClient.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/git/branches",
            new CreateTaskBranchRequest(connected.Repository.Id, null, "main", "feat"));
        Assert.Equal(HttpStatusCode.Created, branchRes.StatusCode);
        var branchDto = await branchRes.Content.ReadFromJsonAsync<TaskGitBranchDto>();
        Assert.NotNull(branchDto);
        Assert.Equal("feat/GITPRJ-1-add-oauth-login", branchDto.BranchName);

        // Verify task list returns GitSummary & InProgress status
        var listRes = await devClient.GetAsync($"/api/v1/projects/{project.Id}/tasks");
        listRes.EnsureSuccessStatusCode();
        var paged = await listRes.Content.ReadFromJsonAsync<PagedResult<TaskSummaryDto>>();
        var boardTask = Assert.Single(paged!.Items);
        Assert.Equal(TaskStatuses.InProgress, boardTask.Status);
        Assert.NotNull(boardTask.GitSummary);
        Assert.Equal(1, boardTask.GitSummary.BranchCount);

        // 6. Webhook with bad signature -> 401
        var anonClient = factory.CreateClient();
        var badWebhookReq = new HttpRequestMessage(HttpMethod.Post, connected.Repository.WebhookPath)
        {
            Content = new StringContent("{\"action\":\"opened\"}", Encoding.UTF8, "application/json")
        };
        badWebhookReq.Headers.Add("X-GitHub-Event", "pull_request");
        badWebhookReq.Headers.Add("X-GitHub-Delivery", "deliv-bad");
        badWebhookReq.Headers.Add("X-Hub-Signature-256", "sha256=0000");
        var badWebhookRes = await anonClient.SendAsync(badWebhookReq);
        Assert.Equal(HttpStatusCode.Unauthorized, badWebhookRes.StatusCode);

        // 7. Signed Webhook: pull_request opened -> auto-links & transitions InProgress -> InReview
        var openPayload = JsonSerializer.Serialize(new
        {
            action = "opened",
            repository = new { full_name = "octocat/workflowpro" },
            sender = new { login = "devuser" },
            pull_request = new
            {
                id = 5001,
                number = 10,
                title = "feat(GITPRJ-1): Add OAuth Login",
                body = "Implements OAuth",
                state = "open",
                draft = false,
                merged = false,
                created_at = "2026-11-28T09:30:00Z",
                html_url = "https://github.com/octocat/workflowpro/pull/10",
                head = new { @ref = "feat/GITPRJ-1-add-oauth-login" },
                @base = new { @ref = "main" },
                user = new { login = "devuser" },
                commits = 2,
                additions = 80,
                deletions = 10
            }
        });

        var openRes = await SendSignedWebhookAsync(anonClient, connected.Repository.WebhookPath, "pull_request", "deliv-open-1", "my_webhook_secret", openPayload);
        openRes.EnsureSuccessStatusCode();
        var openResult = await openRes.Content.ReadFromJsonAsync<GitWebhookProcessResultDto>();
        Assert.Equal(1, openResult!.TasksLinked);
        Assert.Equal(1, openResult.TasksTransitioned);

        // Duplicate delivery -> Ignored
        var dupRes = await SendSignedWebhookAsync(anonClient, connected.Repository.WebhookPath, "pull_request", "deliv-open-1", "my_webhook_secret", openPayload);
        dupRes.EnsureSuccessStatusCode();
        var dupResult = await dupRes.Content.ReadFromJsonAsync<GitWebhookProcessResultDto>();
        Assert.Equal(GitWebhookDeliveryStatuses.Ignored, dupResult!.Status);

        // Verify task is now InReview and has PR #10
        var detailAfterOpen = await (await devClient.GetAsync($"/api/v1/tasks/{task.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>();
        Assert.Equal(TaskStatuses.InReview, detailAfterOpen!.Status);
        Assert.Single(detailAfterOpen.PullRequests!);
        Assert.Equal(10, detailAfterOpen.PullRequests![0].PrNumber);

        // 8. Signed Webhook: pull_request merged -> transitions InReview -> Done
        var mergedPayload = JsonSerializer.Serialize(new
        {
            action = "closed",
            repository = new { full_name = "octocat/workflowpro" },
            sender = new { login = "mgruser" },
            pull_request = new
            {
                id = 5001,
                number = 10,
                title = "feat(GITPRJ-1): Add OAuth Login",
                body = "Implements OAuth",
                state = "closed",
                draft = false,
                merged = true,
                merged_at = "2026-11-28T10:00:00Z",
                closed_at = "2026-11-28T10:00:00Z",
                created_at = "2026-11-28T09:30:00Z",
                html_url = "https://github.com/octocat/workflowpro/pull/10",
                head = new { @ref = "feat/GITPRJ-1-add-oauth-login" },
                @base = new { @ref = "main" },
                user = new { login = "devuser" },
                commits = 2,
                additions = 80,
                deletions = 10
            }
        });

        var mergeRes = await SendSignedWebhookAsync(anonClient, connected.Repository.WebhookPath, "pull_request", "deliv-merge-1", "my_webhook_secret", mergedPayload);
        mergeRes.EnsureSuccessStatusCode();

        var detailAfterMerge = await (await devClient.GetAsync($"/api/v1/tasks/{task.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>();
        Assert.Equal(TaskStatuses.Done, detailAfterMerge!.Status);
        Assert.Equal(PullRequestStates.Merged, detailAfterMerge.PullRequests![0].State);

        // Verify notifications created for dev
        var notifRes = await devClient.GetAsync("/api/v1/notifications?unreadOnly=true");
        notifRes.EnsureSuccessStatusCode();
        var notifs = await notifRes.Content.ReadFromJsonAsync<PagedResult<NotificationDto>>();
        Assert.Contains(notifs!.Items, n => n.Type == NotificationTypes.PullRequestMerged);

        // 9. Manual PR link & unlink (#99)
        var linkRes = await devClient.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/git/pull-requests/link",
            new LinkPullRequestRequest(connected.Repository.Id, 99));
        Assert.Equal(HttpStatusCode.Created, linkRes.StatusCode);
        var linkedPr = await linkRes.Content.ReadFromJsonAsync<TaskPullRequestDto>();
        Assert.Equal(99, linkedPr!.PrNumber);

        var unlinkRes = await devClient.DeleteAsync($"/api/v1/tasks/{task.Id}/git/pull-requests/{linkedPr.Id}");
        Assert.Equal(HttpStatusCode.NoContent, unlinkRes.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendSignedWebhookAsync(
        HttpClient client,
        string path,
        string eventType,
        string deliveryId,
        string secret,
        string jsonPayload)
    {
        var sigBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(jsonPayload));
        var signature = "sha256=" + Convert.ToHexString(sigBytes).ToLowerInvariant();

        var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-GitHub-Event", eventType);
        req.Headers.Add("X-GitHub-Delivery", deliveryId);
        req.Headers.Add("X-Hub-Signature-256", signature);

        return await client.SendAsync(req);
    }
}
