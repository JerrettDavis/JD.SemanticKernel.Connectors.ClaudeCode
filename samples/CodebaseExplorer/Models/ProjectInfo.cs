namespace CodebaseExplorer.Models;

/// <summary>
/// Represents a single project within a codebase (e.g., a .csproj, package.json, pom.xml).
/// </summary>
public sealed record ProjectInfo
{
    /// <summary>The project file path relative to the codebase root.</summary>
    public required string ProjectFilePath { get; init; }

    /// <summary>The project name.</summary>
    public required string Name { get; init; }

    /// <summary>The project type (e.g., "dotnet", "node", "python", "java").</summary>
    public required string ProjectType { get; init; }

    /// <summary>Target framework(s) or runtime version.</summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    /// <summary>Package/library dependencies.</summary>
    public IReadOnlyList<DependencyInfo> Dependencies { get; init; } = [];

    /// <summary>Source files belonging to this project.</summary>
    public IReadOnlyList<string> SourceFiles { get; init; } = [];
}
