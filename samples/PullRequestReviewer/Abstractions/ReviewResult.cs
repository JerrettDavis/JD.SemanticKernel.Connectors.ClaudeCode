namespace PullRequestReviewer.Abstractions;

/// <summary>
/// The final result of a PR review.
/// </summary>
public sealed record ReviewResult
{
    /// <summary>The PR that was reviewed.</summary>
    public required PullRequestInfo PullRequest { get; init; }

    /// <summary>Overall summary of the review.</summary>
    public required string Summary { get; init; }

    /// <summary>Overall recommendation.</summary>
    public required ReviewVerdict Verdict { get; init; }

    /// <summary>Individual review comments.</summary>
    public IReadOnlyList<ReviewComment> Comments { get; init; } = [];

    /// <summary>Analyzer results that fed into the review.</summary>
    public IReadOnlyList<AnalyzerResult> AnalyzerResults { get; init; } = [];
}

/// <summary>Overall review verdict.</summary>
public enum ReviewVerdict
{
    Approve,
    RequestChanges,
    Comment
}
