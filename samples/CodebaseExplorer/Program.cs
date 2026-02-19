using System.CommandLine;
using CodebaseExplorer.Plugins;
using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// ──────────────────────────────────────────────────────────────────
// jdxplr — Structured Codebase Knowledge Generator
//
// Demonstrates:
//   • Claude Code authentication via UseClaudeCodeChatCompletion()
//   • SK plugins for file system exploration and code analysis
//   • Agentic workflow: scan → analyze → generate knowledgebase
//   • Structured markdown documentation output
//   • System.CommandLine for CLI parsing
// ──────────────────────────────────────────────────────────────────

var pathArgument = new Argument<DirectoryInfo>("path")
{
    Description = "Root directory of the codebase to analyze"
};

var outputOption = new Option<DirectoryInfo?>("--output", "-o")
{
    Description = "Output directory for knowledgebase documents (defaults to <path>/.knowledgebase)"
};

var modelOption = new Option<string>("--model", "-m")
{
    Description = "Claude model to use",
    DefaultValueFactory = _ => "claude-sonnet-4-6"
};

var depthOption = new Option<int>("--depth", "-d")
{
    Description = "Maximum directory tree depth for scanning",
    DefaultValueFactory = _ => 3
};

var rootCommand = new RootCommand("AI-powered codebase profiler that generates structured knowledgebases as markdown documentation")
{
    pathArgument,
    outputOption,
    modelOption,
    depthOption
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var codebaseDir = parseResult.GetValue(pathArgument)!;
    var outputDir = parseResult.GetValue(outputOption);
    var model = parseResult.GetValue(modelOption)!;
    var depth = parseResult.GetValue(depthOption);

    var codebasePath = codebaseDir.FullName;
    if (!codebaseDir.Exists)
    {
        Console.Error.WriteLine($"Error: Directory not found at '{codebasePath}'.");
        return 1;
    }

    var outputPath = outputDir?.FullName ?? Path.Combine(codebasePath, ".knowledgebase");

    Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║    jdxplr — AI Codebase Knowledge Generator              ║");
    Console.WriteLine("║    Powered by Semantic Kernel + Claude Code Auth          ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"📂 Codebase: {codebasePath}");
    Console.WriteLine($"📁 Output:   {outputPath}");
    Console.WriteLine();

    var builder = Kernel.CreateBuilder();
    builder.UseClaudeCodeChatCompletion(model);
    builder.Plugins.AddFromObject(new FileSystemPlugin(codebasePath), "FileSystem");
    builder.Plugins.AddFromObject(new CodeAnalysisPlugin(), "CodeAnalysis");
    builder.Plugins.AddFromObject(new KnowledgeBaseWriterPlugin(outputDir?.FullName ?? Path.Combine(codebasePath, "knowledgebase")), "KnowledgeBase");

    var kernel = builder.Build();
    var chat = kernel.GetRequiredService<IChatCompletionService>();

    var history = new ChatHistory();
    history.AddSystemMessage($$"""
        You are a senior software architect tasked with profiling a codebase and generating a
        comprehensive knowledgebase. You have access to file system, code analysis, and documentation
        writing tools.

        Target codebase: {{codebasePath}}
        Output directory: {{outputPath}}
        Max directory depth: {{depth}}

        Follow this structured workflow:

        Phase 1 — Discovery:
        1. Get the directory tree (use depth {{depth}}) to understand the project structure
        2. Detect project files to identify the tech stack
        3. Count lines of code by extension to gauge size and language mix
        4. Find entry points to understand application startup

        Phase 2 — Deep Analysis:
        5. Analyze each project file (csproj, package.json, etc.) for dependencies
        6. Extract namespaces and types for the API surface map
        7. Search for architectural patterns (DI registration, middleware, handlers, etc.)

        Phase 3 — Knowledge Generation:
        Generate these knowledgebase documents using the writer plugin:

        8.  README.md — Project overview, purpose, tech stack, getting started
        9.  ARCHITECTURE.md — System architecture with component descriptions and data flow
        10. DEPENDENCIES.md — Complete dependency analysis with purposes
        11. API-SURFACE.md — Public types, interfaces, and extension points
        12. PATTERNS.md — Coding patterns, conventions, and architectural decisions

        Guidelines:
        - Be thorough but concise in documentation
        - Use mermaid diagrams where they add clarity
        - Cite specific files and line numbers when discussing patterns
        - Focus on information that would help a new developer onboard quickly
        - Note any potential concerns or tech debt you observe
        """);

    history.AddUserMessage("Profile this codebase and generate a comprehensive knowledgebase. Start with discovery, then deep analysis, then write all documentation.");

    Console.WriteLine("🔍 Starting codebase analysis...\n");

    var settings = new OpenAIPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    };

    var response = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);

    Console.WriteLine("─────────────────────────────────────────────");
    Console.WriteLine("ANALYSIS COMPLETE");
    Console.WriteLine("─────────────────────────────────────────────");
    Console.WriteLine(response.Content);
    Console.WriteLine();
    Console.WriteLine($"✅ Knowledgebase written to: {outputPath}");

    return 0;
});

return await rootCommand.Parse(args).InvokeAsync();
