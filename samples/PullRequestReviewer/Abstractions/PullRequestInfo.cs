namespace PullRequestReviewer.Abstractions;

/// <summary>
/// Metadata about a pull request.
/// </summary>
public sealed record PullRequestInfo
{
    /// <summary>The PR number or ID.</summary>
    public required int Number { get; init; }

    /// <summary>PR title.</summary>
    public required string Title { get; init; }

    /// <summary>PR description/body.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The author's username.</summary>
    public required string Author { get; init; }

    /// <summary>The source branch name.</summary>
    public required string SourceBranch { get; init; }

    /// <summary>The target branch name.</summary>
    public required string TargetBranch { get; init; }

    /// <summary>Files changed in this PR.</summary>
    public IReadOnlyList<FileChange> Files { get; init; } = [];

    /// <summary>The repository clone URL.</summary>
    public string? CloneUrl { get; init; }
}
