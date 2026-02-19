namespace PullRequestReviewer.Abstractions;

/// <summary>
/// Represents a file changed in a pull request.
/// </summary>
/// <param name="Path">The relative file path within the repository.</param>
/// <param name="ChangeType">The type of change (Added, Modified, Deleted, Renamed).</param>
/// <param name="Diff">The unified diff content for this file.</param>
/// <param name="PreviousPath">The previous path if the file was renamed; null otherwise.</param>
/// <param name="Additions">Number of lines added.</param>
/// <param name="Deletions">Number of lines deleted.</param>
public sealed record FileChange(
    string Path,
    ChangeType ChangeType,
    string Diff,
    string? PreviousPath = null,
    int Additions = 0,
    int Deletions = 0);

/// <summary>
/// Types of file changes in a pull request.
/// </summary>
public enum ChangeType
{
    Added,
    Modified,
    Deleted,
    Renamed
}
