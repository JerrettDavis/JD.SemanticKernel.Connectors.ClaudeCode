using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PullRequestReviewer.Abstractions;

namespace PullRequestReviewer.Providers.GitLab;

/// <summary>
/// Retrieves merge request data from GitLab using the REST API (v4).
/// Supports both GitLab.com and self-hosted GitLab instances.
/// </summary>
/// <remarks>
/// Authentication is via a Personal Access Token or Project/Group token.
/// Set the <c>GITLAB_TOKEN</c> environment variable or pass the token directly.
///
/// The <paramref name="gitLabUrl"/> should be the base URL of your GitLab instance,
/// e.g. <c>https://gitlab.com</c> or <c>https://gitlab.mycompany.com</c>.
/// </remarks>
public sealed class GitLabPullRequestProvider : IPullRequestProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBase;

    public GitLabPullRequestProvider(string gitLabUrl = "https://gitlab.com", string? token = null)
    {
        _apiBase = gitLabUrl.TrimEnd('/') + "/api/v4";
        _httpClient = new HttpClient();

        token ??= Environment.GetEnvironmentVariable("GITLAB_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
        }
    }

    public string ProviderName => "GitLab";

    /// <summary>
    /// Retrieves a merge request from GitLab.
    /// </summary>
    /// <param name="repositoryOwner">
    /// The project path (namespace/project) URL-encoded, or the numeric project ID.
    /// For example: <c>"mygroup%2Fmyproject"</c> or <c>"12345"</c>.
    /// </param>
    /// <param name="repositoryName">
    /// Unused for GitLab (project identity is fully captured in <paramref name="repositoryOwner"/>).
    /// Pass any value or an empty string.
    /// </param>
    /// <param name="pullRequestNumber">The merge request IID (internal ID).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<PullRequestInfo> GetPullRequestAsync(
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        // GitLab uses project-scoped IIDs for merge requests.
        // repositoryOwner is the URL-encoded project path or numeric project ID.
        var projectId = Uri.EscapeDataString(
            string.IsNullOrEmpty(repositoryName)
                ? repositoryOwner
                : $"{repositoryOwner}/{repositoryName}");

        // Fetch MR metadata
        var mrUrl = $"{_apiBase}/projects/{projectId}/merge_requests/{pullRequestNumber}";
        var mr = await _httpClient.GetFromJsonAsync<GitLabMergeRequest>(mrUrl, ct)
            ?? throw new InvalidOperationException($"Failed to fetch MR !{pullRequestNumber}.");

        // Fetch MR changes (diffs)
        var changesUrl = $"{_apiBase}/projects/{projectId}/merge_requests/{pullRequestNumber}/changes";
        var mrChanges = await _httpClient.GetFromJsonAsync<GitLabMergeRequestChanges>(changesUrl, ct);

        var fileChanges = new List<FileChange>();

        if (mrChanges?.Changes is not null)
        {
            foreach (var change in mrChanges.Changes)
            {
                var changeType = DetermineChangeType(change);
                var path = change.NewPath ?? change.OldPath ?? "(unknown)";

                fileChanges.Add(new FileChange(
                    Path: path,
                    ChangeType: changeType,
                    Diff: change.Diff ?? string.Empty,
                    PreviousPath: change.RenamedFile == true ? change.OldPath : null,
                    Additions: CountDiffLines(change.Diff, '+'),
                    Deletions: CountDiffLines(change.Diff, '-')
                ));
            }
        }

        // Build clone URL from project web_url
        var cloneUrl = mr.WebUrl is not null
            ? mr.WebUrl.Split("/-/")[0] + ".git"
            : null;

        return new PullRequestInfo
        {
            Number = mr.Iid,
            Title = mr.Title ?? string.Empty,
            Description = mr.Description ?? string.Empty,
            Author = mr.Author?.Username ?? "unknown",
            SourceBranch = mr.SourceBranch ?? string.Empty,
            TargetBranch = mr.TargetBranch ?? string.Empty,
            Files = fileChanges,
            CloneUrl = cloneUrl
        };
    }

    public void Dispose() => _httpClient.Dispose();

    private static ChangeType DetermineChangeType(GitLabDiffEntry change)
    {
        if (change.NewFile == true) return ChangeType.Added;
        if (change.DeletedFile == true) return ChangeType.Deleted;
        if (change.RenamedFile == true) return ChangeType.Renamed;
        return ChangeType.Modified;
    }

    private static int CountDiffLines(string? diff, char prefix)
    {
        if (string.IsNullOrEmpty(diff)) return 0;
        return diff.Split('\n')
            .Count(line => line.Length > 0 && line[0] == prefix && !(line.Length >= 3 && line[1] == prefix && line[2] == prefix));
    }

    // ── GitLab API response models ──

    private sealed record GitLabMergeRequest
    {
        [JsonPropertyName("iid")]
        public int Iid { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("source_branch")]
        public string? SourceBranch { get; init; }

        [JsonPropertyName("target_branch")]
        public string? TargetBranch { get; init; }

        [JsonPropertyName("author")]
        public GitLabUser? Author { get; init; }

        [JsonPropertyName("web_url")]
        public string? WebUrl { get; init; }
    }

    private sealed record GitLabMergeRequestChanges
    {
        [JsonPropertyName("changes")]
        public GitLabDiffEntry[]? Changes { get; init; }
    }

    private sealed record GitLabDiffEntry
    {
        [JsonPropertyName("old_path")]
        public string? OldPath { get; init; }

        [JsonPropertyName("new_path")]
        public string? NewPath { get; init; }

        [JsonPropertyName("diff")]
        public string? Diff { get; init; }

        [JsonPropertyName("new_file")]
        public bool? NewFile { get; init; }

        [JsonPropertyName("renamed_file")]
        public bool? RenamedFile { get; init; }

        [JsonPropertyName("deleted_file")]
        public bool? DeletedFile { get; init; }
    }

    private sealed record GitLabUser
    {
        [JsonPropertyName("username")]
        public string? Username { get; init; }
    }
}
