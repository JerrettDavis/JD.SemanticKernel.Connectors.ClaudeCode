using System.Text.RegularExpressions;
using PullRequestReviewer.Abstractions;

namespace PullRequestReviewer.Analyzers;

/// <summary>
/// Analyzes unified diffs to produce structural findings about the changes.
/// Detects large files, binary indicators, high churn, and common patterns.
/// </summary>
public sealed class DiffAnalyzer : IAnalyzer
{
    private const int LargeFileDiffThreshold = 500;
    private const int HighChurnThreshold = 100;

    public string Name => "DiffAnalyzer";

    public Task<AnalyzerResult> AnalyzeAsync(
        IReadOnlyList<FileChange> files,
        CancellationToken ct = default)
    {
        var findings = new List<AnalyzerFinding>();

        foreach (var file in files)
        {
            // Flag large diffs
            var diffLines = file.Diff.Split('\n').Length;
            if (diffLines > LargeFileDiffThreshold)
            {
                findings.Add(new AnalyzerFinding(
                    file.Path, null, "Warning",
                    $"Large diff ({diffLines} lines). Consider breaking into smaller changes."));
            }

            // Flag high churn (many additions + deletions = rewrite)
            if (file.Additions + file.Deletions > HighChurnThreshold)
            {
                findings.Add(new AnalyzerFinding(
                    file.Path, null, "Info",
                    $"High churn: +{file.Additions}/-{file.Deletions} lines. May warrant careful review."));
            }

            // Detect potential secrets/credentials
            if (ContainsPotentialSecrets(file.Diff))
            {
                findings.Add(new AnalyzerFinding(
                    file.Path, null, "Critical",
                    "Potential secret or credential detected in diff. Verify no sensitive data is committed."));
            }

            // Detect TODO/HACK/FIXME additions
            var todoMatches = Regex.Matches(file.Diff, @"^\+.*\b(TODO|HACK|FIXME|XXX)\b", RegexOptions.Multiline);
            if (todoMatches.Count > 0)
            {
                findings.Add(new AnalyzerFinding(
                    file.Path, null, "Info",
                    $"Found {todoMatches.Count} new TODO/HACK/FIXME comment(s)."));
            }

            // Detect deleted test files
            if (file.ChangeType == ChangeType.Deleted &&
                (file.Path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                 file.Path.Contains("spec", StringComparison.OrdinalIgnoreCase)))
            {
                findings.Add(new AnalyzerFinding(
                    file.Path, null, "Warning",
                    "Test file was deleted. Ensure test coverage is not degraded."));
            }
        }

        // Summary stats
        var summary = $"Analyzed {files.Count} file(s): " +
                       $"+{files.Sum(f => f.Additions)}/-{files.Sum(f => f.Deletions)} lines, " +
                       $"{files.Count(f => f.ChangeType == ChangeType.Added)} added, " +
                       $"{files.Count(f => f.ChangeType == ChangeType.Deleted)} deleted, " +
                       $"{files.Count(f => f.ChangeType == ChangeType.Modified)} modified.";

        return Task.FromResult(new AnalyzerResult
        {
            AnalyzerName = Name,
            Findings = findings,
            RawOutput = summary
        });
    }

    private static bool ContainsPotentialSecrets(string diff)
    {
        var patterns = new[]
        {
            @"(?i)(password|secret|api[_-]?key|token|credential)\s*[:=]\s*[""'][^""']+[""']",
            @"(?i)-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----",
            @"sk-ant-api[a-zA-Z0-9\-]+",
            @"ghp_[a-zA-Z0-9]{36}",
            @"AKIA[0-9A-Z]{16}"
        };

        return patterns.Any(p => Regex.IsMatch(diff, p));
    }
}
