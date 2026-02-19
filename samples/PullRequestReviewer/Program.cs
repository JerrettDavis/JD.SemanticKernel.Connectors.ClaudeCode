using System.CommandLine;
using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PullRequestReviewer.Abstractions;
using PullRequestReviewer.Analyzers;
using PullRequestReviewer.Plugins;
using PullRequestReviewer.Providers.AzureDevOps;
using PullRequestReviewer.Providers.GitHub;
using PullRequestReviewer.Providers.GitLab;

// ──────────────────────────────────────────────────────────────────
// jdpr — AI-Powered Pull Request Review Agent
//
// Demonstrates:
//   • Claude Code authentication via UseClaudeCodeChatCompletion()
//   • Extensible provider architecture (GitHub, Azure DevOps, GitLab)
//   • Analyzer pipeline feeding into agentic review
//   • Structured multi-file code review workflow
//   • System.CommandLine for CLI parsing
// ──────────────────────────────────────────────────────────────────

var providerOption = new Option<string>("--provider", "-p")
{
    Description = "Source control provider: github, ado, gitlab",
    DefaultValueFactory = _ => "github"
};

var ownerOption = new Option<string>("--owner")
{
    Description = "Repository owner/org (GitHub, ADO project name, or GitLab namespace/project)",
    Required = true
};

var repoOption = new Option<string>("--repo", "-r")
{
    Description = "Repository name (not required for GitLab)",
    DefaultValueFactory = _ => string.Empty
};

var prOption = new Option<int>("--pr")
{
    Description = "Pull request / merge request number",
    Required = true
};

var tokenOption = new Option<string?>("--token", "-t")
{
    Description = "PAT for the provider (or set GITHUB_TOKEN / AZURE_DEVOPS_PAT / GITLAB_TOKEN)"
};

var orgOption = new Option<string?>("--org")
{
    Description = "Azure DevOps organization URL (e.g. https://dev.azure.com/myorg)"
};

var urlOption = new Option<string>("--url")
{
    Description = "GitLab instance URL",
    DefaultValueFactory = _ => "https://gitlab.com"
};

var modelOption = new Option<string>("--model", "-m")
{
    Description = "Claude model to use",
    DefaultValueFactory = _ => ClaudeModels.Default
};

var rootCommand = new RootCommand("AI-powered pull request review agent supporting GitHub, Azure DevOps, and GitLab")
{
    providerOption,
    ownerOption,
    repoOption,
    prOption,
    tokenOption,
    orgOption,
    urlOption,
    modelOption
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var providerName = parseResult.GetValue(providerOption)!;
    var owner = parseResult.GetValue(ownerOption)!;
    var repo = parseResult.GetValue(repoOption)!;
    var prNum = parseResult.GetValue(prOption);
    var token = parseResult.GetValue(tokenOption);
    var orgUrl = parseResult.GetValue(orgOption);
    var gitlabUrl = parseResult.GetValue(urlOption)!;
    var model = parseResult.GetValue(modelOption)!;

    Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║          jdpr — AI Code Review Agent                     ║");
    Console.WriteLine("║          Powered by Semantic Kernel + Claude Code Auth    ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    // ── Step 1: Resolve provider and fetch PR data ──
    using var provider = (IPullRequestProvider)(providerName.ToLowerInvariant() switch
    {
        "github" or "gh" => new GitHubPullRequestProvider(
            token ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")),

        "ado" or "azuredevops" or "azure-devops" => new AzureDevOpsPullRequestProvider(
            orgUrl ?? throw new InvalidOperationException("Azure DevOps requires --org <organization-url>."),
            token ?? Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT")),

        "gitlab" or "gl" => new GitLabPullRequestProvider(
            gitlabUrl,
            token ?? Environment.GetEnvironmentVariable("GITLAB_TOKEN")),

        _ => throw new InvalidOperationException(
            $"Unknown provider '{providerName}'. Supported: github, ado, gitlab.")
    });

    Console.WriteLine($"📡 [{provider.ProviderName}] Fetching PR #{prNum} from {owner}/{(string.IsNullOrEmpty(repo) ? "(project)" : repo)}...");

    PullRequestInfo pr;
    try
    {
        pr = await provider.GetPullRequestAsync(owner, repo, prNum, cancellationToken);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error fetching PR: {ex.Message}");
        return 1;
    }

    Console.WriteLine($"   Title: {pr.Title}");
    Console.WriteLine($"   Author: {pr.Author}");
    Console.WriteLine($"   Branch: {pr.SourceBranch} → {pr.TargetBranch}");
    Console.WriteLine($"   Files: {pr.Files.Count} changed");
    Console.WriteLine();

    // ── Step 2: Run analyzers ──
    Console.WriteLine("🔍 Running analyzers...");

    var analyzers = new List<IAnalyzer> { new DiffAnalyzer() };
    var analyzerResults = new List<AnalyzerResult>();

    foreach (var analyzer in analyzers)
    {
        Console.WriteLine($"   Running {analyzer.Name}...");
        var result = await analyzer.AnalyzeAsync(pr.Files, cancellationToken);
        analyzerResults.Add(result);
        Console.WriteLine($"   {analyzer.Name}: {result.Findings.Count} finding(s)");
    }

    Console.WriteLine();

    // ── Step 3: Build SK kernel and plugins ──
    Console.WriteLine("🤖 Initializing review agent...");

    var codeReviewPlugin = new CodeReviewPlugin();
    codeReviewPlugin.SetPullRequest(pr);
    codeReviewPlugin.AddAnalyzerResults(analyzerResults);

    var builder = Kernel.CreateBuilder();
    builder.UseClaudeCodeChatCompletion(model);
    builder.Plugins.AddFromObject(codeReviewPlugin, "Review");
    using var gitPlugin = new GitPlugin();
    builder.Plugins.AddFromObject(gitPlugin, "Git");

    var kernel = builder.Build();
    var chat = kernel.GetRequiredService<IChatCompletionService>();

    // ── Step 4: Run structured review ──
    Console.WriteLine("📝 Starting code review...\n");

    var history = new ChatHistory();
    history.AddSystemMessage("""
        You are an expert code reviewer. Perform a thorough, structured review of the pull request.

        Review Workflow:
        1. First, get the PR summary to understand the context
        2. Check analyzer findings for any automated issues detected
        3. List all changed files to plan your review
        4. For each changed file, get its diff and review it carefully
        5. Record review comments for any issues found using add_review_comment
        6. After reviewing all files, get the review summary

        Review Criteria:
        - Correctness: Logic errors, edge cases, null handling
        - Security: Input validation, authentication, data exposure
        - Performance: Unnecessary allocations, N+1 queries, blocking calls
        - Maintainability: Naming, complexity, code organization
        - Best Practices: SOLID principles, error handling, logging
        - Testing: Adequate test coverage for changes

        Severity Guide:
        - Critical: Security vulnerabilities, data loss risk, crash bugs
        - Error: Logic errors, broken functionality
        - Warning: Performance issues, maintainability concerns
        - Info: Style suggestions, minor improvements

        Be thorough but fair. Only flag real issues; do not nitpick style preferences.
        Focus on what matters for production code quality.
        """);

    history.AddUserMessage("Review this pull request. Follow the structured workflow: get the PR summary, check analyzer findings, then review each changed file systematically.");

    var settings = new OpenAIPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    };

    var response = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);

    Console.WriteLine("─────────────────────────────────────────────");
    Console.WriteLine("REVIEW COMPLETE");
    Console.WriteLine("─────────────────────────────────────────────");
    Console.WriteLine(response.Content);
    Console.WriteLine();

    // ── Step 5: Output structured results ──
    var comments = codeReviewPlugin.Comments;
    if (comments.Count > 0)
    {
        Console.WriteLine($"\n📊 Review Statistics:");
        Console.WriteLine($"   Total comments: {comments.Count}");
        Console.WriteLine($"   Critical: {comments.Count(c => c.Severity == ReviewSeverity.Critical)}");
        Console.WriteLine($"   Errors: {comments.Count(c => c.Severity == ReviewSeverity.Error)}");
        Console.WriteLine($"   Warnings: {comments.Count(c => c.Severity == ReviewSeverity.Warning)}");
        Console.WriteLine($"   Info: {comments.Count(c => c.Severity == ReviewSeverity.Info)}");

        var verdict = comments.Any(c => c.Severity >= ReviewSeverity.Error)
            ? ReviewVerdict.RequestChanges
            : comments.Any(c => c.Severity >= ReviewSeverity.Warning)
                ? ReviewVerdict.Comment
                : ReviewVerdict.Approve;

        Console.WriteLine($"\n   Verdict: {verdict}");
    }
    else
    {
        Console.WriteLine("\n✅ No issues found — LGTM!");
    }

    return 0;
});

try
{
    return await rootCommand.Parse(args).InvokeAsync();
}
catch (ClaudeCodeSessionException ex)
{
    Console.Error.WriteLine($"Authentication error: {ex.Message}");
    return 1;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Network error: {ex.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation cancelled.");
    return 130;
}
