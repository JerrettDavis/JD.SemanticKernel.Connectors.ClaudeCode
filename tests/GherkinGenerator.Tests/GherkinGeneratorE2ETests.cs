using GherkinGenerator.Plugins;
using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GherkinGenerator.Tests;

/// <summary>
/// E2E tests for GherkinGenerator plugins — validates file I/O,
/// path safety, kernel wiring, and template output.
/// </summary>
[Trait("Category", "E2E")]
public sealed class GherkinWriterPluginTests : IDisposable
{
    private readonly string _tempDir;

    public GherkinWriterPluginTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"gherkin-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void WriteFeatureFile_CreatesFile()
    {
        var plugin = new GherkinWriterPlugin(_tempDir);
        var content = """
            Feature: Login
              Scenario: Valid credentials
                Given a registered user
                When they log in
                Then they see the dashboard
            """;

        var result = plugin.WriteFeatureFile(
            "login.feature", content);

        Assert.Contains("written successfully", result);
        var path = Path.Combine(_tempDir, "login.feature");
        Assert.True(File.Exists(path));
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void WriteFeatureFile_AppendsExtension()
    {
        var plugin = new GherkinWriterPlugin(_tempDir);
        plugin.WriteFeatureFile("signup", "Feature: Signup");

        Assert.True(
            File.Exists(Path.Combine(_tempDir, "signup.feature")));
    }

    [Fact]
    public void WriteFeatureFile_BlocksPathTraversal()
    {
        var plugin = new GherkinWriterPlugin(_tempDir);
        var result = plugin.WriteFeatureFile(
            "../../etc/passwd.feature", "bad");

        Assert.Contains("Path traversal", result);
    }

    [Fact]
    public void AppendToFeatureFile_AppendsContent()
    {
        var plugin = new GherkinWriterPlugin(_tempDir);
        var initial = "Feature: Cart\n\n";
        File.WriteAllText(
            Path.Combine(_tempDir, "cart.feature"), initial);

        var scenario = """
              Scenario: Add item
                Given an empty cart
                When I add an item
                Then cart has 1 item
            """;

        var result = plugin.AppendToFeatureFile(
            "cart.feature", scenario);

        Assert.Contains("appended successfully", result);
        var content = File.ReadAllText(
            Path.Combine(_tempDir, "cart.feature"));
        Assert.Contains("Add item", content);
    }

    [Fact]
    public void AppendToFeatureFile_MissingFile_ReturnsError()
    {
        var plugin = new GherkinWriterPlugin(_tempDir);
        var result = plugin.AppendToFeatureFile(
            "nope.feature", "content");

        Assert.Contains("not found", result);
    }

    [Fact]
    public void GetGherkinTemplate_ReturnsValidTemplate()
    {
        var template = GherkinWriterPlugin.GetGherkinTemplate();

        Assert.Contains("Feature:", template);
        Assert.Contains("Scenario:", template);
        Assert.Contains("Given", template);
        Assert.Contains("When", template);
        Assert.Contains("Then", template);
        Assert.Contains("Scenario Outline:", template);
        Assert.Contains("Examples:", template);
    }
}

[Trait("Category", "E2E")]
public sealed class FeatureFilePluginTests : IDisposable
{
    private readonly string _tempDir;

    public FeatureFilePluginTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"feature-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void ListFeatureFiles_FindsFiles()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "a.feature"),
            "Feature: Alpha\n  Scenario: S1\n    Given x");
        File.WriteAllText(
            Path.Combine(_tempDir, "b.feature"),
            "Feature: Beta\n  Scenario: S2\n    Given y");

        var plugin = new FeatureFilePlugin();
        var result = plugin.ListFeatureFiles(_tempDir);

        Assert.Contains("2 feature file(s)", result);
        Assert.Contains("Alpha", result);
        Assert.Contains("Beta", result);
    }

    [Fact]
    public void ListFeatureFiles_EmptyDir_ReportsNone()
    {
        var plugin = new FeatureFilePlugin();
        var result = plugin.ListFeatureFiles(_tempDir);

        Assert.Contains("No .feature files", result);
    }

    [Fact]
    public void ListFeatureFiles_MissingDir_ReturnsError()
    {
        var plugin = new FeatureFilePlugin();
        var result = plugin.ListFeatureFiles(
            Path.Combine(_tempDir, "nope"));

        Assert.Contains("not found", result);
    }

    [Fact]
    public void ReadFeatureFile_ReturnsContent()
    {
        var path = Path.Combine(_tempDir, "test.feature");
        File.WriteAllText(path,
            "@smoke\nFeature: Test\n  Scenario: S1\n    Given x");

        var plugin = new FeatureFilePlugin();
        var result = plugin.ReadFeatureFile(path);

        Assert.Contains("Feature: Test", result);
        Assert.Contains("@smoke", result);
    }

    [Fact]
    public void ReadFeatureFile_MissingFile_ReturnsError()
    {
        var plugin = new FeatureFilePlugin();
        var result = plugin.ReadFeatureFile(
            Path.Combine(_tempDir, "nope.feature"));

        Assert.Contains("not found", result);
    }
}

[Trait("Category", "E2E")]
public sealed class StepDefinitionScannerPluginTests
{
    [Fact]
    public void ListStepKeywords_ReturnsAllKeywords()
    {
        var result =
            StepDefinitionScannerPlugin.ListStepKeywords();

        Assert.Contains("Given", result);
        Assert.Contains("When", result);
        Assert.Contains("Then", result);
        Assert.Contains("And", result);
        Assert.Contains("But", result);
    }

    [Fact]
    public void ScanAssembly_MissingFile_ReturnsError()
    {
        var plugin = new StepDefinitionScannerPlugin();
        var result = plugin.ScanAssembly("/nonexistent.dll");

        Assert.Contains("not found", result);
    }
}

[Trait("Category", "E2E")]
public sealed class KernelWiringTests
{
    [Fact]
    public void Kernel_RegistersAllGherkinPlugins()
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(), $"kw-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var builder = Kernel.CreateBuilder();

            // Register a no-op chat service so Build() succeeds
            builder.Services.AddSingleton<IChatCompletionService>(
                new NoOpChatService());

            builder.Plugins.AddFromObject(
                new StepDefinitionScannerPlugin(), "StepScanner");
            builder.Plugins.AddFromObject(
                new FeatureFilePlugin(), "Features");
            builder.Plugins.AddFromObject(
                new GherkinWriterPlugin(tempDir), "GherkinWriter");

            var kernel = builder.Build();

            var functions = kernel.Plugins
                .SelectMany(p => p.Select(
                    f => $"{p.Name}.{f.Name}"))
                .ToList();

            Assert.Contains(
                "StepScanner.scan_assembly_for_step_definitions",
                functions);
            Assert.Contains(
                "StepScanner.list_step_keywords", functions);
            Assert.Contains(
                "Features.list_feature_files", functions);
            Assert.Contains(
                "Features.read_feature_file", functions);
            Assert.Contains(
                "GherkinWriter.write_feature_file", functions);
            Assert.Contains(
                "GherkinWriter.append_to_feature_file", functions);
            Assert.Contains(
                "GherkinWriter.get_gherkin_template", functions);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

[Trait("Category", "E2E")]
public sealed class CredentialResolutionTests
{
    [Fact]
    public void UseClaudeCodeChatCompletion_WithApiKey_Builds()
    {
        var builder = Kernel.CreateBuilder();
        builder.UseClaudeCodeChatCompletion(
            apiKey: "sk-ant-api-test-key-e2e");

        var kernel = builder.Build();
        var svc = kernel.GetRequiredService<IChatCompletionService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public void UseClaudeCodeChatCompletion_CustomModel_Builds()
    {
        var builder = Kernel.CreateBuilder();
        builder.UseClaudeCodeChatCompletion(
            "claude-opus-4-6",
            apiKey: "sk-ant-api-test-key-e2e");

        var kernel = builder.Build();
        Assert.NotNull(
            kernel.GetRequiredService<IChatCompletionService>());
    }

    [Fact]
    public void UseClaudeCodeChatCompletion_ConfigureDelegate_Builds()
    {
        var builder = Kernel.CreateBuilder();
        builder.UseClaudeCodeChatCompletion(
            configure: o => o.ApiKey = "sk-ant-api-test-key-e2e");

        var kernel = builder.Build();
        Assert.NotNull(
            kernel.GetRequiredService<IChatCompletionService>());
    }
}

/// <summary>
/// Live API tests — only run when CLAUDE_E2E_LIVE=true and
/// valid credentials are available.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveGherkinGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public LiveGherkinGeneratorTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"live-gherkin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

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
    public async Task LiveChat_GeneratesGherkinFromCriteria()
    {
        Skip.IfNot(CanRunLive,
            "Set CLAUDE_E2E_LIVE=true with valid credentials");

        var builder = Kernel.CreateBuilder();
        builder.UseClaudeCodeChatCompletion();
        builder.Plugins.AddFromObject(
            new GherkinWriterPlugin(_tempDir), "GherkinWriter");

        var kernel = builder.Build();
        var chat = kernel
            .GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are a BDD expert. Generate Gherkin features. " +
            "Use the GherkinWriter plugin to save files.");
        history.AddUserMessage(
            "Create a simple login feature with one scenario " +
            "for valid credentials. Save it as login.feature.");

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

        // Verify the file was created
        var files = Directory.GetFiles(
            _tempDir, "*.feature", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        var content = await File.ReadAllTextAsync(files[0]);
        Assert.Contains("Feature:", content);
    }
}

/// <summary>
/// Minimal no-op chat service for kernel wiring tests.
/// </summary>
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
