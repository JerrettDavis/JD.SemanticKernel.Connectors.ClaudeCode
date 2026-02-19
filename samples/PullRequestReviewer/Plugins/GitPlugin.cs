using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.SemanticKernel;

namespace PullRequestReviewer.Plugins;

/// <summary>
/// Semantic Kernel plugin for Git operations — clone, diff, branch comparison.
/// Shells out to the local git CLI using ArgumentList for injection safety.
/// </summary>
public sealed class GitPlugin
{
    private static readonly char[] s_shellMeta =
        [';', '&', '|', '$', '`', '\n', '\r', '<', '>'];

    [KernelFunction("clone_repository")]
    [Description("Clones a git repository to a local temporary directory.")]
    public async Task<string> CloneRepositoryAsync(
        [Description("The repository clone URL")] string cloneUrl,
        [Description("Optional target directory path.")] string? targetDir = null,
        CancellationToken ct = default)
    {
        var dir = targetDir
            ?? Path.Combine(
                Path.GetTempPath(),
                "pr-review-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);

        var result = await RunGitAsync(
            dir, ct, "clone", "--depth", "50", cloneUrl, ".");
        return result.ExitCode == 0
            ? $"Repository cloned to: {dir}"
            : $"Error cloning: {result.Output}";
    }

    [KernelFunction("get_branch_diff")]
    [Description("Gets the diff between two branches.")]
    public async Task<string> GetBranchDiffAsync(
        [Description("Path to the local git repo")] string repoPath,
        [Description("Target/base branch (e.g. 'main')")] string targetBranch,
        [Description("Source/feature branch")] string sourceBranch,
        CancellationToken ct = default)
    {
        await RunGitAsync(
            repoPath, ct,
            "fetch", "origin", targetBranch, sourceBranch);

        var result = await RunGitAsync(
            repoPath, ct,
            "diff",
            $"origin/{targetBranch}...origin/{sourceBranch}",
            "--stat");
        return result.ExitCode == 0
            ? $"Diff stats:\n{result.Output}"
            : $"Error getting diff: {result.Output}";
    }

    [KernelFunction("get_file_at_branch")]
    [Description("Gets the content of a specific file at a given branch.")]
    public async Task<string> GetFileAtBranchAsync(
        [Description("Path to the local git repo")] string repoPath,
        [Description("The branch name")] string branch,
        [Description("File path relative to repo root")] string filePath,
        CancellationToken ct = default)
    {
        var result = await RunGitAsync(
            repoPath, ct,
            "show", $"origin/{branch}:{filePath}");
        return result.ExitCode == 0
            ? result.Output
            : $"Error reading file: {result.Output}";
    }

    [KernelFunction("get_commit_log")]
    [Description("Gets the commit log for a branch.")]
    public async Task<string> GetCommitLogAsync(
        [Description("Path to the local git repo")] string repoPath,
        [Description("The branch name")] string branch,
        [Description("Max commits to show")] int maxCount = 20,
        CancellationToken ct = default)
    {
        await RunGitAsync(repoPath, ct, "fetch", "origin", branch);

        var result = await RunGitAsync(
            repoPath, ct,
            "log", $"origin/{branch}",
            "--oneline", "--no-decorate",
            "-n", maxCount.ToString());
        return result.ExitCode == 0
            ? $"Recent commits on {branch}:\n{result.Output}"
            : $"Error getting log: {result.Output}";
    }

    private static void ValidateArg(string value, string name)
    {
        if (value.AsSpan().IndexOfAny(s_shellMeta) >= 0)
            throw new ArgumentException(
                $"Argument '{name}' contains invalid characters.",
                name);
    }

    private static async Task<(int ExitCode, string Output)> RunGitAsync(
        string workingDir,
        CancellationToken ct,
        params string[] args)
    {
        foreach (var arg in args)
            ValidateArg(arg, nameof(args));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) output.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        return (process.ExitCode, output.ToString());
    }
}
