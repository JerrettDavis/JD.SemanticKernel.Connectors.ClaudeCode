using CodebaseExplorer.Plugins;
using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CodebaseExplorer.Tests;

/// <summary>
/// E2E tests for CodebaseExplorer plugins — validates file system
/// scanning, code analysis, knowledge base writing, and path safety.
/// </summary>
[Trait("Category", "E2E")]
public sealed class FileSystemPluginTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemPluginTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"fs-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        SeedTestProject();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void SeedTestProject()
    {
        var src = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(src);

        File.WriteAllText(
            Path.Combine(src, "Program.cs"),
            """
            namespace TestApp;
            public class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("Hello");
                }
            }
            """);

        File.WriteAllText(
            Path.Combine(src, "Service.cs"),
            """
            namespace TestApp;
            public interface IService { }
            public class MyService : IService { }
            """);

        File.WriteAllText(
            Path.Combine(_tempDir, "TestApp.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
    }

    [Fact]
    public void GetDirectoryTree_ReturnsStructure()
    {
        var plugin = new FileSystemPlugin(_tempDir);
        var tree = plugin.GetDirectoryTree(_tempDir);

        Assert.Contains("src", tree);
        Assert.Contains("TestApp.csproj", tree);
    }

    [Fact]
    public void DetectProjectFiles_FindsCsproj()
    {
        var plugin = new FileSystemPlugin(_tempDir);
        var result = plugin.DetectProjectFiles(_tempDir);

        Assert.Contains("TestApp.csproj", result);
    }

    [Fact]
    public void ReadFile_ReturnsContent()
    {
        var plugin = new FileSystemPlugin(_tempDir);
        var result = plugin.ReadFile(
            Path.Combine("src", "Program.cs"));

        Assert.Contains("TestApp", result);
        Assert.Contains("Main", result);
    }

    [Fact]
    public void ReadFile_BlocksPathTraversal()
    {
        var plugin = new FileSystemPlugin(_tempDir);
        var result = plugin.ReadFile("../../etc/passwd");

        Assert.Contains("access denied", result,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadFile_MissingFile_ReturnsError()
    {
        var plugin = new FileSystemPlugin(_tempDir);
        var result = plugin.ReadFile("nonexistent.cs");

        Assert.Contains("not found", result,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CountLinesByExtension_CountsCorrectly()
    {
        var plugin = new FileSystemPlugin(_tempDir);
        var result = plugin.CountLinesByExtension(_tempDir);

        Assert.Contains(".cs", result);
    }

    [Fact]
    public void SearchFiles_FindsPattern()
    {
        var plugin = new FileSystemPlugin(_tempDir);
        var result = plugin.SearchFiles(_tempDir, "*.cs");

        Assert.Contains("Program.cs", result);
        Assert.Contains("Service.cs", result);
    }
}

[Trait("Category", "E2E")]
public sealed class CodeAnalysisPluginTests : IDisposable
{
    private readonly string _tempDir;

    public CodeAnalysisPluginTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"ca-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        SeedTestProject();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void SeedTestProject()
    {
        var src = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(src);

        File.WriteAllText(
            Path.Combine(_tempDir, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(
            Path.Combine(src, "Startup.cs"),
            """
            namespace App;
            public class Startup
            {
                public static void Main(string[] args) { }
            }
            """);

        File.WriteAllText(
            Path.Combine(src, "Models.cs"),
            """
            namespace App.Models;
            public record User(string Name, string Email);
            public interface IRepository<T> { }
            """);

        File.WriteAllText(
            Path.Combine(_tempDir, "package.json"),
            """
            {
              "name": "test-app",
              "version": "1.0.0",
              "dependencies": {
                "express": "^4.18.0"
              },
              "devDependencies": {
                "jest": "^29.0.0"
              }
            }
            """);
    }

    [Fact]
    public void AnalyzeDotnetProject_ParsesDependencies()
    {
        var plugin = new CodeAnalysisPlugin();
        var csproj = Path.Combine(_tempDir, "App.csproj");
        var result = plugin.AnalyzeDotnetProject(csproj);

        Assert.Contains("Newtonsoft.Json", result);
        Assert.Contains("13.0.3", result);
    }

    [Fact]
    public void AnalyzeDotnetProject_MissingFile_ReturnsError()
    {
        var plugin = new CodeAnalysisPlugin();
        var result = plugin.AnalyzeDotnetProject(
            Path.Combine(_tempDir, "missing.csproj"));

        Assert.Contains("not found", result,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzePackageJson_ParsesDependencies()
    {
        var plugin = new CodeAnalysisPlugin();
        var pkgJson = Path.Combine(_tempDir, "package.json");
        var result = plugin.AnalyzePackageJson(pkgJson);

        Assert.Contains("express", result);
        Assert.Contains("jest", result);
    }

    [Fact]
    public void FindEntryPoints_FindsStartupFiles()
    {
        var plugin = new CodeAnalysisPlugin();
        var result = plugin.FindEntryPoints(_tempDir);

        Assert.Contains("Startup", result);
    }

    [Fact]
    public void ExtractNamespacesAndTypes_FindsTypes()
    {
        var plugin = new CodeAnalysisPlugin();
        var result = plugin.ExtractNamespacesAndTypes(_tempDir);

        Assert.Contains("App", result);
    }

    [Fact]
    public void FindPatternInFiles_MatchesPattern()
    {
        var plugin = new CodeAnalysisPlugin();
        var result = plugin.FindPatternInFiles(
            _tempDir, "interface", ".cs");

        Assert.Contains("IRepository", result);
    }
}

[Trait("Category", "E2E")]
public sealed class KnowledgeBaseWriterPluginTests : IDisposable
{
    private readonly string _tempDir;

    public KnowledgeBaseWriterPluginTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"kb-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void WriteDocument_CreatesFile()
    {
        var plugin = new KnowledgeBaseWriterPlugin(_tempDir);
        var result = plugin.WriteDocument(
            "README.md", "# Project Overview\nTest content.");

        Assert.Contains("Document written", result);
        var path = Path.Combine(_tempDir, "README.md");
        Assert.True(File.Exists(path));
        Assert.Contains(
            "Project Overview", File.ReadAllText(path));
    }

    [Fact]
    public void WriteDocument_BlocksPathTraversal()
    {
        var plugin = new KnowledgeBaseWriterPlugin(_tempDir);
        var result = plugin.WriteDocument(
            "../../evil.md", "bad content");

        Assert.Contains("Path traversal", result);
    }

    [Fact]
    public void AppendToDocument_AddsContent()
    {
        var plugin = new KnowledgeBaseWriterPlugin(_tempDir);
        File.WriteAllText(
            Path.Combine(_tempDir, "doc.md"), "# Header\n");

        var result = plugin.AppendToDocument(
            "doc.md", "\n## New Section\nMore content.");

        Assert.Contains("appended", result,
            StringComparison.OrdinalIgnoreCase);
        var content = File.ReadAllText(
            Path.Combine(_tempDir, "doc.md"));
        Assert.Contains("New Section", content);
    }

    [Fact]
    public void ListDocuments_FindsMarkdownFiles()
    {
        var plugin = new KnowledgeBaseWriterPlugin(_tempDir);
        File.WriteAllText(
            Path.Combine(_tempDir, "README.md"), "# readme");
        File.WriteAllText(
            Path.Combine(_tempDir, "ARCH.md"), "# arch");

        var result = plugin.ListDocuments();

        Assert.Contains("README.md", result);
        Assert.Contains("ARCH.md", result);
    }

    [Fact]
    public void ListDocuments_EmptyDir_ReportsNone()
    {
        var plugin = new KnowledgeBaseWriterPlugin(_tempDir);
        var result = plugin.ListDocuments();

        Assert.Contains("No knowledgebase documents", result,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTemplate_ReturnsAllSections()
    {
        var template =
            KnowledgeBaseWriterPlugin
                .GetKnowledgeBaseTemplate();

        Assert.Contains("README.md", template);
        Assert.Contains("ARCHITECTURE.md", template);
        Assert.Contains("DEPENDENCIES.md", template);
        Assert.Contains("API-SURFACE.md", template);
        Assert.Contains("PATTERNS.md", template);
    }
}

[Trait("Category", "E2E")]
public sealed class KernelWiringTests : IDisposable
{
    private readonly string _tempDir;

    public KernelWiringTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"kw-xplr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Kernel_RegistersAllExplorerPlugins()
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(
            new NoOpChatService());

        builder.Plugins.AddFromObject(
            new FileSystemPlugin(_tempDir), "FileSystem");
        builder.Plugins.AddFromObject(
            new CodeAnalysisPlugin(), "CodeAnalysis");
        builder.Plugins.AddFromObject(
            new KnowledgeBaseWriterPlugin(_tempDir),
            "KnowledgeBase");

        var kernel = builder.Build();
        var functions = kernel.Plugins
            .SelectMany(p => p.Select(
                f => $"{p.Name}.{f.Name}"))
            .ToList();

        Assert.Contains(
            "FileSystem.get_directory_tree", functions);
        Assert.Contains(
            "FileSystem.detect_project_files", functions);
        Assert.Contains(
            "FileSystem.read_file", functions);
        Assert.Contains(
            "FileSystem.count_lines_by_extension", functions);
        Assert.Contains(
            "FileSystem.search_files", functions);
        Assert.Contains(
            "CodeAnalysis.analyze_dotnet_project", functions);
        Assert.Contains(
            "CodeAnalysis.analyze_package_json", functions);
        Assert.Contains(
            "CodeAnalysis.find_entry_points", functions);
        Assert.Contains(
            "KnowledgeBase.write_knowledgebase_document",
            functions);
        Assert.Contains(
            "KnowledgeBase.get_knowledgebase_template",
            functions);
    }
}

/// <summary>
/// Full pipeline test — validates the complete exploration
/// workflow: scan → analyze → write knowledgebase.
/// </summary>
[Trait("Category", "E2E")]
public sealed class ExplorationPipelineTests : IDisposable
{
    private readonly string _codebaseDir;
    private readonly string _outputDir;

    public ExplorationPipelineTests()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"pipeline-{Guid.NewGuid():N}");
        _codebaseDir = Path.Combine(root, "codebase");
        _outputDir = Path.Combine(root, "output");
        Directory.CreateDirectory(_codebaseDir);
        Directory.CreateDirectory(_outputDir);
        SeedCodebase();
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_codebaseDir)!;
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private void SeedCodebase()
    {
        File.WriteAllText(
            Path.Combine(_codebaseDir, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var src = Path.Combine(_codebaseDir, "src");
        Directory.CreateDirectory(src);

        File.WriteAllText(
            Path.Combine(src, "Program.cs"),
            """
            namespace App;
            public class Program
            {
                public static void Main() { }
            }
            """);
    }

    [Fact]
    public void FullPipeline_ScanAnalyzeWrite_ProducesKnowledgebase()
    {
        var fs = new FileSystemPlugin(_codebaseDir);
        var analysis = new CodeAnalysisPlugin();
        var writer = new KnowledgeBaseWriterPlugin(_outputDir);

        // Phase 1: Discovery
        var tree = fs.GetDirectoryTree(_codebaseDir);
        Assert.Contains("src", tree);

        var projects = fs.DetectProjectFiles(_codebaseDir);
        Assert.Contains("App.csproj", projects);

        var loc = fs.CountLinesByExtension(_codebaseDir);
        Assert.Contains(".cs", loc);

        // Phase 2: Analysis
        var csproj = Path.Combine(_codebaseDir, "App.csproj");
        var deps = analysis.AnalyzeDotnetProject(csproj);
        Assert.Contains("net8.0", deps);

        var entryPoints = analysis.FindEntryPoints(_codebaseDir);
        Assert.Contains("Program.cs", entryPoints);

        // Phase 3: Write knowledgebase
        writer.WriteDocument(
            "README.md",
            $"# App\n\n{tree}\n\n## Dependencies\n{deps}");

        writer.WriteDocument(
            "ARCHITECTURE.md",
            $"# Architecture\n\nEntry points:\n{entryPoints}");

        // Verify output
        Assert.True(File.Exists(
            Path.Combine(_outputDir, "README.md")));
        Assert.True(File.Exists(
            Path.Combine(_outputDir, "ARCHITECTURE.md")));

        var docs = writer.ListDocuments();
        Assert.Contains("README.md", docs);
        Assert.Contains("ARCHITECTURE.md", docs);
    }
}

/// <summary>
/// Live API tests — only run locally with credentials.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveCodebaseExplorerTests : IDisposable
{
    private readonly string _codebaseDir;
    private readonly string _outputDir;

    public LiveCodebaseExplorerTests()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"live-xplr-{Guid.NewGuid():N}");
        _codebaseDir = Path.Combine(root, "codebase");
        _outputDir = Path.Combine(root, "output");
        Directory.CreateDirectory(_codebaseDir);
        Directory.CreateDirectory(_outputDir);
        SeedCodebase();
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_codebaseDir)!;
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private void SeedCodebase()
    {
        File.WriteAllText(
            Path.Combine(_codebaseDir, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        Directory.CreateDirectory(
            Path.Combine(_codebaseDir, "src"));
        File.WriteAllText(
            Path.Combine(_codebaseDir, "src", "Program.cs"),
            "namespace App;\npublic class Program\n" +
            "{\n    public static void Main() { }\n}\n");
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
    public async Task LiveChat_ExploresCodebaseAndWritesDocs()
    {
        Skip.IfNot(CanRunLive,
            "Set CLAUDE_E2E_LIVE=true with valid credentials");

        var builder = Kernel.CreateBuilder();
        builder.UseClaudeCodeChatCompletion();
        builder.Plugins.AddFromObject(
            new FileSystemPlugin(_codebaseDir), "FileSystem");
        builder.Plugins.AddFromObject(
            new CodeAnalysisPlugin(), "CodeAnalysis");
        builder.Plugins.AddFromObject(
            new KnowledgeBaseWriterPlugin(_outputDir),
            "KnowledgeBase");

        var kernel = builder.Build();
        var chat = kernel
            .GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are a codebase explorer. Scan the directory " +
            "tree, then write a brief README.md using the " +
            "KnowledgeBase plugin.");
        history.AddUserMessage(
            $"Explore the codebase at {_codebaseDir} and " +
            "write a README.md summary.");

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

        // Verify the README was created
        var readme = Path.Combine(_outputDir, "README.md");
        Assert.True(File.Exists(readme),
            $"Expected README.md at {readme}");
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
