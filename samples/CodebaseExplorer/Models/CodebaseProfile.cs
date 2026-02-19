namespace CodebaseExplorer.Models;

/// <summary>
/// A high-level profile of a codebase.
/// </summary>
public sealed record CodebaseProfile
{
    /// <summary>Root directory of the codebase.</summary>
    public required string RootPath { get; init; }

    /// <summary>Detected primary language(s).</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>Detected frameworks and runtimes.</summary>
    public IReadOnlyList<string> Frameworks { get; init; } = [];

    /// <summary>Projects discovered in the codebase.</summary>
    public IReadOnlyList<ProjectInfo> Projects { get; init; } = [];

    /// <summary>Total number of source files.</summary>
    public int TotalFiles { get; init; }

    /// <summary>Total lines of code (approximate).</summary>
    public long TotalLinesOfCode { get; init; }

    /// <summary>Top-level directory structure summary.</summary>
    public IReadOnlyList<string> DirectoryStructure { get; init; } = [];
}
