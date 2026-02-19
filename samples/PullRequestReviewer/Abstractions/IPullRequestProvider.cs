namespace PullRequestReviewer.Abstractions;

/// <summary>
/// Provides access to pull request data from a source control platform.
/// Implement this interface to support GitHub, Azure DevOps, GitLab, etc.
/// </summary>
public interface IPullRequestProvider : IDisposable
{
    /// <summary>The display name of this provider (e.g., "GitHub", "Azure DevOps").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Retrieves the full pull request metadata and file changes.
    /// </summary>
    /// <param name="repositoryOwner">The repository owner or organization.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="pullRequestNumber">The PR number.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PullRequestInfo> GetPullRequestAsync(
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        CancellationToken ct = default);
}
