using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PullRequestReviewer.Abstractions;

namespace PullRequestReviewer.Providers.AzureDevOps;

/// <summary>
/// Retrieves pull request data from Azure DevOps using the REST API.
/// Supports both Azure DevOps Services (dev.azure.com) and on-premises Azure DevOps Server.
/// </summary>
/// <remarks>
/// Authentication is via Personal Access Token (PAT).
/// Set the <c>AZURE_DEVOPS_PAT</c> environment variable or pass the token directly.
///
/// The <paramref name="organizationUrl"/> should be the base URL of your organization,
/// e.g. <c>https://dev.azure.com/myorg</c> or <c>https://myserver/tfs/DefaultCollection</c>.
/// </remarks>
public sealed class AzureDevOpsPullRequestProvider : IPullRequestProvider, IDisposable
{
    private const string ApiVersion = "7.1";

    private readonly HttpClient _httpClient;
    private readonly string _organizationUrl;

    public AzureDevOpsPullRequestProvider(string organizationUrl, string? pat = null)
    {
        _organizationUrl = organizationUrl.TrimEnd('/');
        _httpClient = new HttpClient();

        pat ??= Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
        if (!string.IsNullOrEmpty(pat))
        {
            var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", encoded);
        }
    }

    public string ProviderName => "Azure DevOps";

    /// <summary>
    /// Retrieves a pull request from Azure DevOps.
    /// </summary>
    /// <param name="repositoryOwner">The Azure DevOps project name.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="pullRequestNumber">The pull request ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<PullRequestInfo> GetPullRequestAsync(
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        var project = repositoryOwner;

        // Fetch the PR metadata
        var prUrl = $"{_organizationUrl}/{project}/_apis/git/repositories/{repositoryName}" +
                    $"/pullrequests/{pullRequestNumber}?api-version={ApiVersion}";

        var prResponse = await _httpClient.GetFromJsonAsync<AdoPullRequest>(prUrl, ct)
            ?? throw new InvalidOperationException($"Failed to fetch PR #{pullRequestNumber}.");

        // Fetch the iterations to get file changes
        var iterationsUrl = $"{_organizationUrl}/{project}/_apis/git/repositories/{repositoryName}" +
                            $"/pullrequests/{pullRequestNumber}/iterations?api-version={ApiVersion}";

        var iterationsResponse = await _httpClient.GetFromJsonAsync<AdoListResponse<AdoIteration>>(iterationsUrl, ct);
        var lastIteration = iterationsResponse?.Value?.LastOrDefault();

        var fileChanges = new List<FileChange>();

        if (lastIteration is not null)
        {
            // Fetch changes for the last iteration
            var changesUrl = $"{_organizationUrl}/{project}/_apis/git/repositories/{repositoryName}" +
                             $"/pullrequests/{pullRequestNumber}/iterations/{lastIteration.Id}" +
                             $"/changes?api-version={ApiVersion}";

            var changesResponse = await _httpClient.GetFromJsonAsync<AdoIterationChanges>(changesUrl, ct);

            if (changesResponse?.ChangeEntries is not null)
            {
                foreach (var change in changesResponse.ChangeEntries)
                {
                    fileChanges.Add(new FileChange(
                        Path: change.Item?.Path?.TrimStart('/') ?? "(unknown)",
                        ChangeType: MapChangeType(change.ChangeType),
                        Diff: string.Empty, // ADO doesn't return inline diffs; use git diff instead
                        Additions: 0,
                        Deletions: 0
                    ));
                }
            }
        }

        var repoUrl = $"{_organizationUrl}/{project}/_git/{repositoryName}";

        return new PullRequestInfo
        {
            Number = prResponse.PullRequestId,
            Title = prResponse.Title ?? string.Empty,
            Description = prResponse.Description ?? string.Empty,
            Author = prResponse.CreatedBy?.DisplayName ?? prResponse.CreatedBy?.UniqueName ?? "unknown",
            SourceBranch = StripRefPrefix(prResponse.SourceRefName),
            TargetBranch = StripRefPrefix(prResponse.TargetRefName),
            Files = fileChanges,
            CloneUrl = repoUrl
        };
    }

    public void Dispose() => _httpClient.Dispose();

    private static string StripRefPrefix(string? refName) =>
        refName?.Replace("refs/heads/", "") ?? string.Empty;

    private static ChangeType MapChangeType(string? changeType) =>
        changeType?.ToLowerInvariant() switch
        {
            "add" => ChangeType.Added,
            "delete" => ChangeType.Deleted,
            "rename" => ChangeType.Renamed,
            "edit" => ChangeType.Modified,
            _ => ChangeType.Modified
        };

    // ── Azure DevOps API response models ──

    private sealed record AdoPullRequest
    {
        [JsonPropertyName("pullRequestId")]
        public int PullRequestId { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("sourceRefName")]
        public string? SourceRefName { get; init; }

        [JsonPropertyName("targetRefName")]
        public string? TargetRefName { get; init; }

        [JsonPropertyName("createdBy")]
        public AdoIdentity? CreatedBy { get; init; }

        [JsonPropertyName("repository")]
        public AdoRepository? Repository { get; init; }
    }

    private sealed record AdoIdentity
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("uniqueName")]
        public string? UniqueName { get; init; }
    }

    private sealed record AdoRepository
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("remoteUrl")]
        public string? RemoteUrl { get; init; }
    }

    private sealed record AdoListResponse<T>
    {
        [JsonPropertyName("value")]
        public T[]? Value { get; init; }

        [JsonPropertyName("count")]
        public int Count { get; init; }
    }

    private sealed record AdoIteration
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }
    }

    private sealed record AdoIterationChanges
    {
        [JsonPropertyName("changeEntries")]
        public AdoChangeEntry[]? ChangeEntries { get; init; }
    }

    private sealed record AdoChangeEntry
    {
        [JsonPropertyName("changeType")]
        public string? ChangeType { get; init; }

        [JsonPropertyName("item")]
        public AdoChangeItem? Item { get; init; }
    }

    private sealed record AdoChangeItem
    {
        [JsonPropertyName("path")]
        public string? Path { get; init; }
    }
}
