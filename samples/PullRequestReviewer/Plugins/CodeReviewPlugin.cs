using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using PullRequestReviewer.Abstractions;

namespace PullRequestReviewer.Plugins;

/// <summary>
/// Semantic Kernel plugin that exposes code review functions to the agent.
/// The agent uses these to retrieve PR data, file diffs, and record review comments.
/// </summary>
public sealed class CodeReviewPlugin
{
    private PullRequestInfo? _currentPr;
    private readonly List<ReviewComment> _comments = [];
    private readonly List<AnalyzerResult> _analyzerResults = [];

    /// <summary>The accumulated review comments.</summary>
    public IReadOnlyList<ReviewComment> Comments => _comments;

    /// <summary>The analyzer results fed into this review.</summary>
    public IReadOnlyList<AnalyzerResult> AnalyzerResults => _analyzerResults;

    /// <summary>Sets the PR context for this review session.</summary>
    public void SetPullRequest(PullRequestInfo pr)
    {
        _currentPr = pr;
    }

    /// <summary>Adds analyzer results for the agent to consider.</summary>
    public void AddAnalyzerResults(IEnumerable<AnalyzerResult> results)
    {
        _analyzerResults.AddRange(results);
    }

    [KernelFunction("get_pr_summary")]
    [Description("Gets the pull request summary including title, description, author, branches, and file change statistics.")]
    public string GetPrSummary()
    {
        if (_currentPr is null)
            return "Error: No pull request loaded.";

        var sb = new StringBuilder();
        sb.AppendLine($"PR #{_currentPr.Number}: {_currentPr.Title}");
        sb.AppendLine($"Author: {_currentPr.Author}");
        sb.AppendLine($"Branch: {_currentPr.SourceBranch} → {_currentPr.TargetBranch}");
        sb.AppendLine($"Files changed: {_currentPr.Files.Count}");
        sb.AppendLine($"Total: +{_currentPr.Files.Sum(f => f.Additions)}/-{_currentPr.Files.Sum(f => f.Deletions)} lines");
        sb.AppendLine();
        sb.AppendLine("Description:");
        sb.AppendLine(_currentPr.Description);

        return sb.ToString();
    }

    [KernelFunction("list_changed_files")]
    [Description("Lists all files changed in the PR with their change types and line counts.")]
    public string ListChangedFiles()
    {
        if (_currentPr is null)
            return "Error: No pull request loaded.";

        var sb = new StringBuilder();
        sb.AppendLine($"Changed files ({_currentPr.Files.Count}):");
        foreach (var file in _currentPr.Files)
        {
            sb.AppendLine($"  [{file.ChangeType}] {file.Path} (+{file.Additions}/-{file.Deletions})");
        }

        return sb.ToString();
    }

    [KernelFunction("get_file_diff")]
    [Description("Gets the unified diff for a specific file in the PR. Use this to review the actual code changes.")]
    public string GetFileDiff(
        [Description("The file path to get the diff for")] string filePath)
    {
        if (_currentPr is null)
            return "Error: No pull request loaded.";

        var file = _currentPr.Files.FirstOrDefault(f =>
            f.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));

        if (file is null)
            return $"Error: File '{filePath}' not found in PR changes.";

        return $"Diff for {file.Path} ({file.ChangeType}, +{file.Additions}/-{file.Deletions}):\n\n{file.Diff}";
    }

    [KernelFunction("get_analyzer_findings")]
    [Description("Gets all findings from automated analyzers that ran on the PR.")]
    public string GetAnalyzerFindings()
    {
        if (_analyzerResults.Count == 0)
            return "No analyzer results available.";

        var sb = new StringBuilder();
        foreach (var result in _analyzerResults)
        {
            sb.AppendLine($"--- {result.AnalyzerName} ---");
            if (result.RawOutput is not null)
                sb.AppendLine(result.RawOutput);

            foreach (var finding in result.Findings)
            {
                sb.AppendLine($"  [{finding.Severity}] {finding.FilePath}" +
                             (finding.LineNumber.HasValue ? $":{finding.LineNumber}" : "") +
                             $" — {finding.Message}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [KernelFunction("add_review_comment")]
    [Description("Records a review comment for a specific file. Use this to capture findings during the code review.")]
    public string AddReviewComment(
        [Description("The file path this comment applies to")] string filePath,
        [Description("Severity: Info, Warning, Error, or Critical")] string severity,
        [Description("Category of the finding (e.g., Security, Performance, Style, Logic, BestPractice)")] string category,
        [Description("The detailed review comment")] string comment,
        [Description("Optional line number in the file")] int? lineNumber = null,
        [Description("Optional suggested code fix")] string? suggestedFix = null)
    {
        if (!Enum.TryParse<ReviewSeverity>(severity, ignoreCase: true, out var sev))
            sev = ReviewSeverity.Info;

        _comments.Add(new ReviewComment
        {
            FilePath = filePath,
            LineNumber = lineNumber,
            Severity = sev,
            Category = category,
            Body = comment,
            SuggestedFix = suggestedFix
        });

        return $"Comment recorded for {filePath} [{severity}/{category}].";
    }

    [KernelFunction("get_review_summary")]
    [Description("Gets a summary of all review comments recorded so far, grouped by severity.")]
    public string GetReviewSummary()
    {
        if (_comments.Count == 0)
            return "No review comments recorded yet.";

        var sb = new StringBuilder();
        sb.AppendLine($"Review Summary — {_comments.Count} comment(s):");

        foreach (var group in _comments.GroupBy(c => c.Severity).OrderByDescending(g => g.Key))
        {
            sb.AppendLine($"\n  {group.Key} ({group.Count()}):");
            foreach (var c in group)
            {
                sb.AppendLine($"    [{c.Category}] {c.FilePath}" +
                             (c.LineNumber.HasValue ? $":{c.LineNumber}" : "") +
                             $" — {c.Body}");
            }
        }

        return sb.ToString();
    }
}
