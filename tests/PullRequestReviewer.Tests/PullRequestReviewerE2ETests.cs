using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PullRequestReviewer.Abstractions;
using PullRequestReviewer.Analyzers;
using PullRequestReviewer.Plugins;

namespace PullRequestReviewer.Tests;

/// <summary>
/// E2E tests for PullRequestReviewer — validates plugin pipeline,
/// analyzer output, provider resolution, and review workflow.
/// </summary>
[Trait("Category", "E2E")]
public sealed class CodeReviewPluginTests
{
    private static PullRequestInfo SamplePr => new()
    {
        Number = 42,
        Title = "Add user authentication",
        Description = "Implements JWT auth for API endpoints.",
        Author = "dev-user",
        SourceBranch = "feature/auth",
        TargetBranch = "main",
        Files =
        [
            new FileChange(
                "src/Auth/JwtHandler.cs",
                ChangeType.Added,
                """
                +using System.Security;
                +public class JwtHandler
                +{
                +    public string GenerateToken(string userId)
                +    {
                +        // TODO: add refresh token support
                +        return "jwt-token";
                +    }
                +}
                """,
                Additions: 8, Deletions: 0),
            new FileChange(
                "src/Auth/AuthMiddleware.cs",
                ChangeType.Added,
                "+public class AuthMiddleware { }",
                Additions: 1, Deletions: 0),
            new FileChange(
                "tests/Auth/JwtHandlerTests.cs",
                ChangeType.Added,
                "+public class JwtHandlerTests { }",
                Additions: 1, Deletions: 0)
        ]
    };

    [Fact]
    public void GetPrSummary_ReturnsFormattedSummary()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        var summary = plugin.GetPrSummary();

        Assert.Contains("PR #42", summary);
        Assert.Contains("Add user authentication", summary);
        Assert.Contains("dev-user", summary);
        Assert.Contains("feature/auth", summary);
        Assert.Contains("main", summary);
        Assert.Contains("3", summary); // 3 files
    }

    [Fact]
    public void ListChangedFiles_ListsAllFiles()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        var result = plugin.ListChangedFiles();

        Assert.Contains("JwtHandler.cs", result);
        Assert.Contains("AuthMiddleware.cs", result);
        Assert.Contains("JwtHandlerTests.cs", result);
        Assert.Contains("[Added]", result);
    }

    [Fact]
    public void GetFileDiff_ReturnsCorrectDiff()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        var diff = plugin.GetFileDiff("src/Auth/JwtHandler.cs");

        Assert.Contains("GenerateToken", diff);
        Assert.Contains("+8/-0", diff);
    }

    [Fact]
    public void GetFileDiff_UnknownFile_ReturnsError()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        var result = plugin.GetFileDiff("nope.cs");

        Assert.Contains("not found", result);
    }

    [Fact]
    public void AddReviewComment_RecordsComment()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        plugin.AddReviewComment(
            "src/Auth/JwtHandler.cs",
            "Warning", "Security",
            "Token generation needs expiry logic.",
            lineNumber: 6,
            suggestedFix: "Add token expiry param");

        Assert.Single(plugin.Comments);
        var c = plugin.Comments[0];
        Assert.Equal("src/Auth/JwtHandler.cs", c.FilePath);
        Assert.Equal(ReviewSeverity.Warning, c.Severity);
        Assert.Equal("Security", c.Category);
        Assert.Equal(6, c.LineNumber);
        Assert.NotNull(c.SuggestedFix);
    }

    [Fact]
    public void AddReviewComment_InvalidSeverity_DefaultsToInfo()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        plugin.AddReviewComment(
            "file.cs", "bogus", "Style", "minor note");

        Assert.Equal(
            ReviewSeverity.Info, plugin.Comments[0].Severity);
    }

    [Fact]
    public void GetReviewSummary_GroupsBySeverity()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        plugin.AddReviewComment(
            "a.cs", "Critical", "Security", "SQL injection");
        plugin.AddReviewComment(
            "b.cs", "Info", "Style", "Consider renaming");
        plugin.AddReviewComment(
            "c.cs", "Warning", "Performance", "N+1 query");

        var summary = plugin.GetReviewSummary();

        Assert.Contains("3 comment(s)", summary);
        Assert.Contains("Critical", summary);
        Assert.Contains("Warning", summary);
        Assert.Contains("Info", summary);
    }

    [Fact]
    public void GetReviewSummary_NoComments_ReportsNone()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        var result = plugin.GetReviewSummary();

        Assert.Contains("No review comments", result);
    }

    [Fact]
    public void GetPrSummary_NoPr_ReturnsError()
    {
        var plugin = new CodeReviewPlugin();
        Assert.Contains("Error", plugin.GetPrSummary());
    }

    [Fact]
    public void AddAnalyzerResults_AreAccessible()
    {
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(SamplePr);

        var results = new[]
        {
            new AnalyzerResult
            {
                AnalyzerName = "TestAnalyzer",
                Findings =
                [
                    new AnalyzerFinding(
                        "a.cs", 10, "Warning", "test finding")
                ]
            }
        };

        plugin.AddAnalyzerResults(results);
        var output = plugin.GetAnalyzerFindings();

        Assert.Contains("TestAnalyzer", output);
        Assert.Contains("test finding", output);
    }
}

[Trait("Category", "E2E")]
public sealed class DiffAnalyzerTests
{
    [Fact]
    public async Task Analyze_DetectsTodoComments()
    {
        var files = new List<FileChange>
        {
            new("src/app.cs", ChangeType.Modified,
                "+// TODO: fix this later\n+var x = 1;",
                Additions: 2, Deletions: 0)
        };

        var analyzer = new DiffAnalyzer();
        var result = await analyzer.AnalyzeAsync(files);

        Assert.Contains(
            result.Findings,
            f => f.Message.Contains(
                "TODO", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Analyze_DetectsPotentialSecrets()
    {
        var files = new List<FileChange>
        {
            new("config.json", ChangeType.Modified,
                "+\"api_key\": \"sk-ant-api-secret-123abc\"",
                Additions: 1, Deletions: 0)
        };

        var analyzer = new DiffAnalyzer();
        var result = await analyzer.AnalyzeAsync(files);

        Assert.Contains(
            result.Findings,
            f => string.Equals(
                f.Severity, "Critical",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Analyze_FlagsDeletedTestFiles()
    {
        var files = new List<FileChange>
        {
            new("tests/AuthTests.cs", ChangeType.Deleted,
                "-public class AuthTests { }",
                Additions: 0, Deletions: 1)
        };

        var analyzer = new DiffAnalyzer();
        var result = await analyzer.AnalyzeAsync(files);

        Assert.Contains(
            result.Findings,
            f => f.Message.Contains(
                "Test file was deleted",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Analyze_FlagsHighChurn()
    {
        var files = new List<FileChange>
        {
            new("big.cs", ChangeType.Modified, "+x\n-y",
                Additions: 80, Deletions: 80)
        };

        var analyzer = new DiffAnalyzer();
        var result = await analyzer.AnalyzeAsync(files);

        Assert.Contains(
            result.Findings,
            f => f.Message.Contains(
                "High churn", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Analyze_ProducesSummary()
    {
        var files = new List<FileChange>
        {
            new("a.cs", ChangeType.Added, "+code",
                Additions: 5, Deletions: 0)
        };

        var analyzer = new DiffAnalyzer();
        var result = await analyzer.AnalyzeAsync(files);

        Assert.NotNull(result.RawOutput);
        Assert.Contains("Analyzed 1 file(s)", result.RawOutput);
    }
}

[Trait("Category", "E2E")]
public sealed class KernelWiringTests
{
    [Fact]
    public void Kernel_RegistersReviewPlugins()
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(
            new NoOpChatService());

        var reviewPlugin = new CodeReviewPlugin();
        reviewPlugin.SetPullRequest(new PullRequestInfo
        {
            Number = 1,
            Title = "test",
            Author = "dev",
            SourceBranch = "feat",
            TargetBranch = "main"
        });

        builder.Plugins.AddFromObject(
            reviewPlugin, "Review");
        builder.Plugins.AddFromObject(
            new GitPlugin(), "Git");

        var kernel = builder.Build();
        var functions = kernel.Plugins
            .SelectMany(p => p.Select(
                f => $"{p.Name}.{f.Name}"))
            .ToList();

        Assert.Contains(
            "Review.get_pr_summary", functions);
        Assert.Contains(
            "Review.list_changed_files", functions);
        Assert.Contains(
            "Review.get_file_diff", functions);
        Assert.Contains(
            "Review.add_review_comment", functions);
        Assert.Contains(
            "Review.get_review_summary", functions);
        Assert.Contains(
            "Review.get_analyzer_findings", functions);
        Assert.Contains(
            "Git.clone_repository", functions);
        Assert.Contains(
            "Git.get_branch_diff", functions);
    }
}

[Trait("Category", "E2E")]
public sealed class ReviewWorkflowTests
{
    [Fact]
    public async Task FullReviewWorkflow_ProducesStructuredOutput()
    {
        var pr = new PullRequestInfo
        {
            Number = 99,
            Title = "Refactor auth module",
            Description = "Splits monolithic auth into services",
            Author = "senior-dev",
            SourceBranch = "refactor/auth",
            TargetBranch = "main",
            Files =
            [
                new FileChange(
                    "src/AuthService.cs", ChangeType.Modified,
                    "+public class AuthService { }",
                    Additions: 20, Deletions: 15),
                new FileChange(
                    "src/TokenService.cs", ChangeType.Added,
                    "+public class TokenService { }",
                    Additions: 30, Deletions: 0)
            ]
        };

        // Step 1: Set up plugin
        var plugin = new CodeReviewPlugin();
        plugin.SetPullRequest(pr);

        // Step 2: Run analyzer
        var analyzer = new DiffAnalyzer();
        var analyzerResult = await analyzer
            .AnalyzeAsync(pr.Files.ToList());
        plugin.AddAnalyzerResults([analyzerResult]);

        // Step 3: Simulate agent workflow
        var summary = plugin.GetPrSummary();
        Assert.Contains("PR #99", summary);

        var findings = plugin.GetAnalyzerFindings();
        Assert.Contains("DiffAnalyzer", findings);

        var files = plugin.ListChangedFiles();
        Assert.Contains("AuthService.cs", files);

        var diff = plugin.GetFileDiff("src/AuthService.cs");
        Assert.Contains("AuthService", diff);

        // Step 4: Add review comments
        plugin.AddReviewComment(
            "src/AuthService.cs", "Warning",
            "Architecture",
            "Consider interface extraction for testability.");

        plugin.AddReviewComment(
            "src/TokenService.cs", "Info",
            "BestPractice",
            "Good separation of concerns.");

        // Step 5: Verify final state
        var reviewSummary = plugin.GetReviewSummary();
        Assert.Contains("2 comment(s)", reviewSummary);

        // Verify verdict logic
        var comments = plugin.Comments;
        var hasErrors = comments.Any(
            c => c.Severity >= ReviewSeverity.Error);
        Assert.False(hasErrors);
    }
}

/// <summary>
/// Live API tests — only run locally with credentials.
/// </summary>
[Trait("Category", "Live")]
public sealed class LivePullRequestReviewerTests
{
    private static bool CanRunLive =>
        string.Equals(
            Environment.GetEnvironmentVariable("CLAUDE_E2E_LIVE"),
            "true", StringComparison.OrdinalIgnoreCase) &&
        (File.Exists(Path.Combine(
             Environment.GetFolderPath(
                 Environment.SpecialFolder.UserProfile),
             ".claude", ".credentials.json")) ||
         !string.IsNullOrEmpty(
             Environment.GetEnvironmentVariable(
                 "ANTHROPIC_API_KEY")));

    [SkippableFact]
    public async Task LiveChat_ReviewsPullRequest()
    {
        Skip.IfNot(CanRunLive,
            "Set CLAUDE_E2E_LIVE=true with valid credentials");

        var pr = new PullRequestInfo
        {
            Number = 1,
            Title = "Test PR for E2E",
            Description = "Simple test change",
            Author = "test-user",
            SourceBranch = "feature/test",
            TargetBranch = "main",
            Files =
            [
                new FileChange(
                    "src/Hello.cs", ChangeType.Modified,
                    """
                    -    Console.WriteLine("Hello");
                    +    Console.WriteLine("Hello, World!");
                    """,
                    Additions: 1, Deletions: 1)
            ]
        };

        var reviewPlugin = new CodeReviewPlugin();
        reviewPlugin.SetPullRequest(pr);

        var builder = Kernel.CreateBuilder();
        builder.UseClaudeCodeChatCompletion();
        builder.Plugins.AddFromObject(
            reviewPlugin, "Review");

        var kernel = builder.Build();
        var chat = kernel
            .GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are a code reviewer. Get the PR summary, " +
            "then review changed files. Keep it brief.");
        history.AddUserMessage("Review this PR.");

        var settings =
            new Microsoft.SemanticKernel.Connectors.OpenAI
                .OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior =
                    FunctionChoiceBehavior.Auto()
            };

        var response = await chat
            .GetChatMessageContentAsync(
                history, settings, kernel);

        Assert.NotNull(response.Content);
        Assert.NotEmpty(response.Content!);
    }
}

file sealed class NoOpChatService : IChatCompletionService
{
    public IReadOnlyDictionary<string, object?> Attributes { get; }
        = new Dictionary<string, object?>(
            StringComparer.Ordinal);

    public Task<IReadOnlyList<ChatMessageContent>>
        GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? settings = null,
            Kernel? kernel = null,
            CancellationToken ct = default)
    {
        IReadOnlyList<ChatMessageContent> result =
            [new(AuthorRole.Assistant, "noop")];
        return Task.FromResult(result);
    }

    public IAsyncEnumerable<StreamingChatMessageContent>
        GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? settings = null,
            Kernel? kernel = null,
            CancellationToken ct = default) =>
        throw new NotSupportedException();
}
