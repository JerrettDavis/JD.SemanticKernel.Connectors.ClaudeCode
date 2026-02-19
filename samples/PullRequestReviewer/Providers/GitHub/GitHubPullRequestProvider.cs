using Octokit;
using PullRequestReviewer.Abstractions;
using ChangeType = PullRequestReviewer.Abstractions.ChangeType;

namespace PullRequestReviewer.Providers.GitHub;

/// <summary>
/// Retrieves pull request data from GitHub using Octokit.
/// </summary>
public sealed class GitHubPullRequestProvider(string? token = null) : IPullRequestProvider
{
    public void Dispose() { /* Octokit client has no resources to release */ }
    private readonly GitHubClient _client = new(new ProductHeaderValue("SK-PR-Reviewer"))
    {
        Credentials = string.IsNullOrEmpty(token)
            ? Credentials.Anonymous
            : new Credentials(token)
    };

    public string ProviderName => "GitHub";

    public async Task<PullRequestInfo> GetPullRequestAsync(
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        var pr = await _client.PullRequest
            .Get(repositoryOwner, repositoryName, pullRequestNumber);

        var files = await _client.PullRequest
            .Files(repositoryOwner, repositoryName, pullRequestNumber);

        var fileChanges = files.Select(f => new FileChange(
            Path: f.FileName,
            ChangeType: MapChangeType(f.Status),
            Diff: f.Patch ?? string.Empty,
            PreviousPath: f.PreviousFileName,
            Additions: f.Additions,
            Deletions: f.Deletions
        )).ToList();

        return new PullRequestInfo
        {
            Number = pr.Number,
            Title = pr.Title,
            Description = pr.Body ?? string.Empty,
            Author = pr.User.Login,
            SourceBranch = pr.Head.Ref,
            TargetBranch = pr.Base.Ref,
            Files = fileChanges,
            CloneUrl = pr.Head.Repository?.CloneUrl
        };
    }

    private static ChangeType MapChangeType(string status)
        => status.ToLowerInvariant() switch
        {
            "added" => ChangeType.Added,
            "removed" => ChangeType.Deleted,
            "renamed" => ChangeType.Renamed,
            _ => ChangeType.Modified
        };
}
