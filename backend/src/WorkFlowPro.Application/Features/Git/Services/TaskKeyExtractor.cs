using System.Text.RegularExpressions;

namespace WorkFlowPro.Application.Features.Git.Services;

public static class TaskKeyExtractor
{
    private static readonly Regex TaskKeyRegex = new(
        @"(?<![A-Za-z0-9])([A-Za-z][A-Za-z0-9_-]{1,29}-\d+)(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NonAlphaNumRegex = new("[^a-z0-9]+", RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractTaskKeys(params string?[] inputs)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input)) continue;

            foreach (Match match in TaskKeyRegex.Matches(input))
            {
                results.Add(match.Groups[1].Value.ToUpperInvariant());
            }
        }

        return results.ToList();
    }

    public static string BuildDefaultBranchName(string taskKey, string title, string prefix = "feat")
    {
        var cleanPrefix = string.IsNullOrWhiteSpace(prefix) ? "feat" : prefix.Trim().Trim('/').ToLowerInvariant();
        var normalizedKey = string.IsNullOrWhiteSpace(taskKey) ? "task" : taskKey.Trim().ToUpperInvariant();
        var lowerTitle = title.Trim().ToLowerInvariant();
        var slug = NonAlphaNumRegex.Replace(lowerTitle, "-").Trim('-');

        if (slug.Length > 45)
        {
            slug = slug[..45].TrimEnd('-');
        }

        return string.IsNullOrEmpty(slug)
            ? $"{cleanPrefix}/{normalizedKey}"
            : $"{cleanPrefix}/{normalizedKey}-{slug}";
    }
}
