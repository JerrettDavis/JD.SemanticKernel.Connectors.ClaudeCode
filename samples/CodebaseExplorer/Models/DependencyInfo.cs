namespace CodebaseExplorer.Models;

/// <summary>
/// Represents a dependency of a project.
/// </summary>
/// <param name="Name">The package or library name.</param>
/// <param name="Version">The version string, if known.</param>
/// <param name="Type">The dependency type (e.g., "NuGet", "npm", "pip", "maven").</param>
/// <param name="IsDevelopment">Whether this is a dev-only dependency.</param>
public sealed record DependencyInfo(
    string Name,
    string? Version,
    string Type,
    bool IsDevelopment = false);
