using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Infrastructure.Git;

public class GitHubProviderClient : IGitProviderClient
{
    private readonly HttpClient _httpClient;

    public string Provider => GitProviders.GitHub;

    public GitHubProviderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GitConnectionTestResult> TestConnectionAsync(
        GitProviderContext context,
        CancellationToken cancellationToken = default)
    {
        var url = $"{context.ApiBaseUrl.TrimEnd('/')}/repos/{Uri.EscapeDataString(context.Owner)}/{Uri.EscapeDataString(context.RepoName)}";
        using var request = CreateRequest(HttpMethod.Get, url, context.AccessToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new GitConnectionTestResult(
                    Success: false,
                    Message: $"GitHub API returned {(int)response.StatusCode} ({response.ReasonPhrase}).",
                    DefaultBranch: null,
                    IsPrivate: false
                );
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var defaultBranch = root.TryGetProperty("default_branch", out var dbProp) ? dbProp.GetString() : "main";
            var isPrivate = root.TryGetProperty("private", out var privProp) && privProp.GetBoolean();

            return new GitConnectionTestResult(
                Success: true,
                Message: "Connected to GitHub repository successfully.",
                DefaultBranch: defaultBranch,
                IsPrivate: isPrivate
            );
        }
        catch (Exception ex)
        {
            return new GitConnectionTestResult(
                Success: false,
                Message: $"Failed to connect to GitHub: {ex.Message}",
                DefaultBranch: null,
                IsPrivate: false
            );
        }
    }

    public async Task<GitRemoteBranchResult> CreateBranchAsync(
        GitProviderContext context,
        string branchName,
        string baseBranch,
        CancellationToken cancellationToken = default)
    {
        var apiBase = context.ApiBaseUrl.TrimEnd('/');
        var owner = Uri.EscapeDataString(context.Owner);
        var repo = Uri.EscapeDataString(context.RepoName);

        // 1. Resolve base branch SHA
        var refUrl = $"{apiBase}/repos/{owner}/{repo}/git/ref/heads/{EscapeBranchPath(baseBranch)}";
        using var refReq = CreateRequest(HttpMethod.Get, refUrl, context.AccessToken);
        using var refRes = await _httpClient.SendAsync(refReq, cancellationToken);
        var refBody = await refRes.Content.ReadAsStringAsync(cancellationToken);

        if (!refRes.IsSuccessStatusCode)
        {
            throw new DomainException($"Unable to resolve base branch '{baseBranch}' on GitHub (HTTP {(int)refRes.StatusCode}).");
        }

        string? sha;
        using (var refDoc = JsonDocument.Parse(refBody))
        {
            sha = refDoc.RootElement.TryGetProperty("object", out var objProp) &&
                  objProp.TryGetProperty("sha", out var shaProp)
                ? shaProp.GetString()
                : null;
        }

        if (string.IsNullOrWhiteSpace(sha))
        {
            throw new DomainException($"Could not determine commit SHA for base branch '{baseBranch}'.");
        }

        // 2. Create new branch ref
        var createUrl = $"{apiBase}/repos/{owner}/{repo}/git/refs";
        using var createReq = CreateRequest(HttpMethod.Post, createUrl, context.AccessToken);
        createReq.Content = JsonContent.Create(new
        {
            @ref = $"refs/heads/{branchName}",
            sha
        });

        using var createRes = await _httpClient.SendAsync(createReq, cancellationToken);
        if (createRes.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            throw new DomainException($"Branch '{branchName}' already exists in {context.Owner}/{context.RepoName}.");
        }

        if (!createRes.IsSuccessStatusCode)
        {
            throw new DomainException($"GitHub failed to create branch '{branchName}' (HTTP {(int)createRes.StatusCode}).");
        }

        var branchUrl = $"{context.WebBaseUrl.TrimEnd('/')}/{context.Owner}/{context.RepoName}/tree/{EscapeBranchPath(branchName)}";
        return new GitRemoteBranchResult(branchName, baseBranch, branchUrl, sha);
    }

    public async Task<IReadOnlyList<GitRemotePullRequest>> ListPullRequestsAsync(
        GitProviderContext context,
        string state = "all",
        int maxResults = 30,
        CancellationToken cancellationToken = default)
    {
        var apiBase = context.ApiBaseUrl.TrimEnd('/');
        var owner = Uri.EscapeDataString(context.Owner);
        var repo = Uri.EscapeDataString(context.RepoName);
        var perPage = Math.Clamp(maxResults, 1, 100);

        var url = $"{apiBase}/repos/{owner}/{repo}/pulls?state={Uri.EscapeDataString(state)}&sort=updated&direction=desc&per_page={perPage}";
        using var request = CreateRequest(HttpMethod.Get, url, context.AccessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new DomainException($"GitHub API returned HTTP {(int)response.StatusCode} while listing pull requests.");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var results = new List<GitRemotePullRequest>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var prEl in doc.RootElement.EnumerateArray())
        {
            var pr = ParsePullRequestElement(prEl);
            if (pr == null) continue;

            // Enrich check status if head sha is present and PR is open/draft
            if ((pr.State == PullRequestStates.Open || pr.State == PullRequestStates.Draft) &&
                prEl.TryGetProperty("head", out var headEl) &&
                headEl.TryGetProperty("sha", out var shaEl))
            {
                var headSha = shaEl.GetString();
                if (!string.IsNullOrWhiteSpace(headSha))
                {
                    var checkStatus = await TryGetCheckStatusAsync(context, headSha, cancellationToken);
                    var reviewStatus = await TryGetReviewStatusAsync(context, pr.PrNumber, cancellationToken);
                    pr = pr with
                    {
                        CheckStatus = checkStatus ?? pr.CheckStatus,
                        ReviewStatus = reviewStatus ?? pr.ReviewStatus
                    };
                }
            }

            results.Add(pr);
        }

        return results;
    }

    public async Task<GitRemotePullRequest?> GetPullRequestAsync(
        GitProviderContext context,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var apiBase = context.ApiBaseUrl.TrimEnd('/');
        var owner = Uri.EscapeDataString(context.Owner);
        var repo = Uri.EscapeDataString(context.RepoName);

        var url = $"{apiBase}/repos/{owner}/{repo}/pulls/{prNumber}";
        using var request = CreateRequest(HttpMethod.Get, url, context.AccessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new DomainException($"GitHub API returned HTTP {(int)response.StatusCode} fetching PR #{prNumber}.");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var pr = ParsePullRequestElement(doc.RootElement);
        if (pr == null) return null;

        if (doc.RootElement.TryGetProperty("head", out var headEl) &&
            headEl.TryGetProperty("sha", out var shaEl))
        {
            var headSha = shaEl.GetString();
            if (!string.IsNullOrWhiteSpace(headSha))
            {
                var checkStatus = await TryGetCheckStatusAsync(context, headSha, cancellationToken);
                var reviewStatus = await TryGetReviewStatusAsync(context, pr.PrNumber, cancellationToken);
                pr = pr with
                {
                    CheckStatus = checkStatus ?? pr.CheckStatus,
                    ReviewStatus = reviewStatus ?? pr.ReviewStatus
                };
            }
        }

        return pr;
    }

    public bool VerifyWebhookSignature(string payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hexSig = signatureHeader[prefix.Length..].Trim();
        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(hexSig);
        }
        catch (FormatException)
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var computedBytes = HMACSHA256.HashData(keyBytes, payloadBytes);

        return CryptographicOperations.FixedTimeEquals(computedBytes, providedBytes);
    }

    public GitWebhookEvent? ParseWebhookEvent(string eventType, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var action = root.TryGetProperty("action", out var actionProp) ? actionProp.GetString() ?? string.Empty : string.Empty;
        var repoFullName = root.TryGetProperty("repository", out var repoProp) &&
                           repoProp.TryGetProperty("full_name", out var fnProp)
            ? fnProp.GetString()
            : null;
        var senderLogin = root.TryGetProperty("sender", out var senderProp) &&
                          senderProp.TryGetProperty("login", out var loginProp)
            ? loginProp.GetString()
            : null;

        if (string.Equals(eventType, "pull_request", StringComparison.OrdinalIgnoreCase) &&
            root.TryGetProperty("pull_request", out var prEl))
        {
            var pr = ParsePullRequestElement(prEl);
            if (pr == null) return null;

            return new GitWebhookEvent(
                EventType: "pull_request",
                Action: action,
                RepositoryFullName: repoFullName,
                BranchName: pr.SourceBranch,
                BaseBranch: pr.TargetBranch,
                BranchUrl: null,
                PullRequest: pr,
                RelatedPrNumber: pr.PrNumber,
                ReviewState: null,
                CheckConclusion: null,
                HeadBranch: pr.SourceBranch,
                SenderLogin: senderLogin
            );
        }

        if (string.Equals(eventType, "pull_request_review", StringComparison.OrdinalIgnoreCase) &&
            root.TryGetProperty("pull_request", out var revPrEl) &&
            root.TryGetProperty("review", out var reviewEl))
        {
            var prNumber = revPrEl.TryGetProperty("number", out var numProp) ? numProp.GetInt32() : (int?)null;
            var rawState = reviewEl.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : null;
            var mappedReview = MapGitHubReviewState(rawState);

            return new GitWebhookEvent(
                EventType: "pull_request_review",
                Action: action,
                RepositoryFullName: repoFullName,
                BranchName: null,
                BaseBranch: null,
                BranchUrl: null,
                PullRequest: null,
                RelatedPrNumber: prNumber,
                ReviewState: mappedReview,
                CheckConclusion: null,
                HeadBranch: null,
                SenderLogin: senderLogin
            );
        }

        if (string.Equals(eventType, "create", StringComparison.OrdinalIgnoreCase))
        {
            var refType = root.TryGetProperty("ref_type", out var rtProp) ? rtProp.GetString() : null;
            if (!string.Equals(refType, "branch", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var branchName = root.TryGetProperty("ref", out var refProp) ? refProp.GetString() : null;
            var defaultBranch = root.TryGetProperty("master_branch", out var mbProp) ? mbProp.GetString() : null;

            return new GitWebhookEvent(
                EventType: "create",
                Action: "created",
                RepositoryFullName: repoFullName,
                BranchName: branchName,
                BaseBranch: defaultBranch,
                BranchUrl: null,
                PullRequest: null,
                RelatedPrNumber: null,
                ReviewState: null,
                CheckConclusion: null,
                HeadBranch: branchName,
                SenderLogin: senderLogin
            );
        }

        if ((string.Equals(eventType, "check_suite", StringComparison.OrdinalIgnoreCase) &&
             root.TryGetProperty("check_suite", out var suiteEl)) ||
            (string.Equals(eventType, "check_run", StringComparison.OrdinalIgnoreCase) &&
             root.TryGetProperty("check_run", out suiteEl)))
        {
            var status = suiteEl.TryGetProperty("status", out var stProp) ? stProp.GetString() : null;
            var conclusion = suiteEl.TryGetProperty("conclusion", out var cProp) ? cProp.GetString() : null;
            var headBranch = suiteEl.TryGetProperty("head_branch", out var hbProp)
                ? hbProp.GetString()
                : suiteEl.TryGetProperty("check_suite", out var nestedSuite) &&
                  nestedSuite.TryGetProperty("head_branch", out var nhbProp)
                    ? nhbProp.GetString()
                    : null;

            var mappedCheck = MapCheckConclusion(status, conclusion);
            return new GitWebhookEvent(
                EventType: eventType.ToLowerInvariant(),
                Action: action,
                RepositoryFullName: repoFullName,
                BranchName: headBranch,
                BaseBranch: null,
                BranchUrl: null,
                PullRequest: null,
                RelatedPrNumber: null,
                ReviewState: null,
                CheckConclusion: mappedCheck,
                HeadBranch: headBranch,
                SenderLogin: senderLogin
            );
        }

        return null;
    }

    private async Task<string?> TryGetCheckStatusAsync(
        GitProviderContext context,
        string headSha,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{context.ApiBaseUrl.TrimEnd('/')}/repos/{Uri.EscapeDataString(context.Owner)}/{Uri.EscapeDataString(context.RepoName)}/commits/{Uri.EscapeDataString(headSha)}/check-runs";
            using var req = CreateRequest(HttpMethod.Get, url, context.AccessToken);
            using var res = await _httpClient.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("check_runs", out var runsEl) || runsEl.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var hasAny = false;
            var hasPending = false;
            var hasFailure = false;

            foreach (var run in runsEl.EnumerateArray())
            {
                hasAny = true;
                var status = run.TryGetProperty("status", out var sProp) ? sProp.GetString() : null;
                var conclusion = run.TryGetProperty("conclusion", out var cProp) ? cProp.GetString() : null;
                var mapped = MapCheckConclusion(status, conclusion);
                if (mapped == PullRequestCheckStatuses.Failure) hasFailure = true;
                else if (mapped == PullRequestCheckStatuses.Pending) hasPending = true;
            }

            if (!hasAny) return PullRequestCheckStatuses.None;
            if (hasFailure) return PullRequestCheckStatuses.Failure;
            if (hasPending) return PullRequestCheckStatuses.Pending;
            return PullRequestCheckStatuses.Success;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryGetReviewStatusAsync(
        GitProviderContext context,
        int prNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{context.ApiBaseUrl.TrimEnd('/')}/repos/{Uri.EscapeDataString(context.Owner)}/{Uri.EscapeDataString(context.RepoName)}/pulls/{prNumber}/reviews";
            using var req = CreateRequest(HttpMethod.Get, url, context.AccessToken);
            using var res = await _httpClient.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var latestByUser = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rev in doc.RootElement.EnumerateArray())
            {
                var user = rev.TryGetProperty("user", out var uEl) && uEl.TryGetProperty("login", out var lProp)
                    ? lProp.GetString() ?? "unknown"
                    : "unknown";
                var state = rev.TryGetProperty("state", out var sProp) ? sProp.GetString() : null;
                var mapped = MapGitHubReviewState(state);
                if (mapped != null && mapped != PullRequestReviewStatuses.Commented)
                {
                    latestByUser[user] = mapped;
                }
            }

            if (latestByUser.Values.Contains(PullRequestReviewStatuses.ChangesRequested))
            {
                return PullRequestReviewStatuses.ChangesRequested;
            }
            if (latestByUser.Values.Contains(PullRequestReviewStatuses.Approved))
            {
                return PullRequestReviewStatuses.Approved;
            }
            return PullRequestReviewStatuses.Pending;
        }
        catch
        {
            return null;
        }
    }

    private static GitRemotePullRequest? ParsePullRequestElement(JsonElement prEl)
    {
        if (!prEl.TryGetProperty("number", out var numberProp)) return null;

        var prNumber = numberProp.GetInt32();
        var externalId = prEl.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0;
        var title = prEl.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? $"PR #{prNumber}" : $"PR #{prNumber}";
        var body = prEl.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String ? bodyProp.GetString() : null;
        var htmlUrl = prEl.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? string.Empty : string.Empty;
        var rawState = prEl.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? "open" : "open";
        var isDraft = prEl.TryGetProperty("draft", out var draftProp) && draftProp.ValueKind == JsonValueKind.True;

        var mergedAt = TryGetDateTime(prEl, "merged_at");
        var closedAt = TryGetDateTime(prEl, "closed_at");
        var openedAt = TryGetDateTime(prEl, "created_at") ?? DateTime.UtcNow;

        var mappedState = mergedAt.HasValue || (prEl.TryGetProperty("merged", out var mergedProp) && mergedProp.ValueKind == JsonValueKind.True)
            ? PullRequestStates.Merged
            : string.Equals(rawState, "closed", StringComparison.OrdinalIgnoreCase)
                ? PullRequestStates.Closed
                : isDraft
                    ? PullRequestStates.Draft
                    : PullRequestStates.Open;

        var sourceBranch = prEl.TryGetProperty("head", out var headProp) && headProp.TryGetProperty("ref", out var headRef)
            ? headRef.GetString() ?? "unknown"
            : "unknown";
        var targetBranch = prEl.TryGetProperty("base", out var baseProp) && baseProp.TryGetProperty("ref", out var baseRef)
            ? baseRef.GetString() ?? "main"
            : "main";

        var authorLogin = prEl.TryGetProperty("user", out var userProp) && userProp.TryGetProperty("login", out var loginProp)
            ? loginProp.GetString() ?? "unknown"
            : "unknown";
        var avatarUrl = prEl.TryGetProperty("user", out var uProp) && uProp.TryGetProperty("avatar_url", out var avProp)
            ? avProp.GetString()
            : null;

        var commits = prEl.TryGetProperty("commits", out var comProp) && comProp.ValueKind == JsonValueKind.Number ? comProp.GetInt32() : 0;
        var additions = prEl.TryGetProperty("additions", out var addProp) && addProp.ValueKind == JsonValueKind.Number ? addProp.GetInt32() : 0;
        var deletions = prEl.TryGetProperty("deletions", out var delProp) && delProp.ValueKind == JsonValueKind.Number ? delProp.GetInt32() : 0;

        return new GitRemotePullRequest(
            ExternalPrId: externalId,
            PrNumber: prNumber,
            Title: title,
            Body: body,
            State: mappedState,
            SourceBranch: sourceBranch,
            TargetBranch: targetBranch,
            AuthorLogin: authorLogin,
            AuthorAvatarUrl: avatarUrl,
            HtmlUrl: htmlUrl,
            ReviewStatus: PullRequestReviewStatuses.Pending,
            CheckStatus: PullRequestCheckStatuses.None,
            CommitsCount: commits,
            Additions: additions,
            Deletions: deletions,
            OpenedAtUtc: openedAt,
            MergedAtUtc: mergedAt,
            ClosedAtUtc: closedAt
        );
    }

    private static string? MapGitHubReviewState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        return state.ToUpperInvariant() switch
        {
            "APPROVED" => PullRequestReviewStatuses.Approved,
            "CHANGES_REQUESTED" => PullRequestReviewStatuses.ChangesRequested,
            "COMMENTED" => PullRequestReviewStatuses.Commented,
            "DISMISSED" => PullRequestReviewStatuses.Pending,
            _ => PullRequestReviewStatuses.Pending
        };
    }

    private static string MapCheckConclusion(string? status, string? conclusion)
    {
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return PullRequestCheckStatuses.Pending;
        }

        return conclusion?.ToLowerInvariant() switch
        {
            "success" or "neutral" or "skipped" => PullRequestCheckStatuses.Success,
            "failure" or "timed_out" or "action_required" or "cancelled" => PullRequestCheckStatuses.Failure,
            _ => PullRequestCheckStatuses.Pending
        };
    }

    private static DateTime? TryGetDateTime(JsonElement el, string propertyName)
    {
        if (el.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(prop.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
            {
                return dt;
            }
        }
        return null;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? accessToken)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("WorkFlowPro", "1.0"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return req;
    }

    private static string EscapeBranchPath(string branch) =>
        string.Join("/", branch.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
}
