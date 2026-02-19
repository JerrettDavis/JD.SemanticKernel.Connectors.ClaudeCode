namespace PullRequestReviewer.Abstractions;

/// <summary>
/// Runs automated analysis on changed files and produces structured findings.
/// </summary>
public interface IAnalyzer
{
    /// <summary>The display name of this analyzer.</summary>
    string Name { get; }

    /// <summary>
    /// Analyzes the given file changes and returns findings.
    /// </summary>
    Task<AnalyzerResult> AnalyzeAsync(
        IReadOnlyList<FileChange> files,
        CancellationToken ct = default);
}

/// <summary>
/// The output of an analyzer run.
/// </summary>
public sealed record AnalyzerResult
{
    /// <summary>Name of the analyzer that produced this result.</summary>
    public required string AnalyzerName { get; init; }

    /// <summary>Individual findings from the analysis.</summary>
    public IReadOnlyList<AnalyzerFinding> Findings { get; init; } = [];

    /// <summary>Raw output or summary from the analyzer.</summary>
    public string? RawOutput { get; init; }
}

/// <summary>A single finding from an analyzer.</summary>
public sealed record AnalyzerFinding(
    string FilePath,
    int? LineNumber,
    string Severity,
    string Message);
