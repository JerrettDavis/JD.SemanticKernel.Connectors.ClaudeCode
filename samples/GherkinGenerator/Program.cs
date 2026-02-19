using System.CommandLine;
using GherkinGenerator.Plugins;
using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// ──────────────────────────────────────────────────────────────────
// jdgerkinator — Acceptance Criteria → Gherkin Feature Files
//
// Demonstrates:
//   • Claude Code authentication via UseClaudeCodeChatCompletion()
//   • SK plugins for assembly scanning, feature file I/O
//   • Agentic loop with automatic function calling
//   • System.CommandLine for CLI parsing
// ──────────────────────────────────────────────────────────────────

var assemblyOption = new Option<FileInfo?>("--assembly", "-a")
{
    Description = "Path to a .NET assembly DLL to scan for Reqnroll/SpecFlow step definitions"
};

var featuresOption = new Option<DirectoryInfo?>("--features", "-f")
{
    Description = "Directory containing existing .feature files to integrate with"
};

var outputOption = new Option<DirectoryInfo>("--output", "-o")
{
    Description = "Output directory for generated .feature files",
    DefaultValueFactory = _ => new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "generated-features"))
};

var modelOption = new Option<string>("--model", "-m")
{
    Description = "Claude model to use",
    DefaultValueFactory = _ => ClaudeModels.Default
};

var inputOption = new Option<FileInfo?>("--input", "-i")
{
    Description = "File containing acceptance criteria (reads from stdin if omitted)"
};

var rootCommand = new RootCommand("AI-powered Acceptance Criteria to Gherkin feature file generator")
{
    assemblyOption,
    featuresOption,
    outputOption,
    modelOption,
    inputOption
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var assemblyPath = parseResult.GetValue(assemblyOption);
    var featuresDir = parseResult.GetValue(featuresOption);
    var outputDir = parseResult.GetValue(outputOption)!;
    var model = parseResult.GetValue(modelOption)!;
    var inputFile = parseResult.GetValue(inputOption);

    Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║    jdgerkinator — AC → Gherkin Feature Files             ║");
    Console.WriteLine("║    Powered by Semantic Kernel + Claude Code Auth          ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    var builder = Kernel.CreateBuilder();
    builder.UseClaudeCodeChatCompletion(model);
    builder.Plugins.AddFromObject(new StepDefinitionScannerPlugin(), "StepScanner");
    builder.Plugins.AddFromObject(new FeatureFilePlugin(), "Features");
    builder.Plugins.AddFromObject(new GherkinWriterPlugin(outputDir.FullName), "GherkinWriter");

    var kernel = builder.Build();
    var chat = kernel.GetRequiredService<IChatCompletionService>();

    var history = new ChatHistory();
    history.AddSystemMessage($$"""
        You are an expert BDD/Gherkin author. Your job is to transform acceptance criteria into
        well-structured Gherkin feature files that follow best practices.

        Guidelines:
        - Use clear, business-readable language in Given/When/Then steps
        - Group related scenarios under a single Feature
        - Use Scenario Outline with Examples for parameterized cases
        - Add Background sections for shared preconditions
        - Use descriptive tags (@smoke, @regression, @wip, etc.)
        - Write "As a / I want / So that" feature descriptions

        {{(assemblyPath is not null ? $"An assembly has been provided at: {assemblyPath.FullName}\nScan it first to discover existing step definitions, then reuse matching steps where possible." : "")}}

        {{(featuresDir is not null ? $"Existing feature files are at: {featuresDir.FullName}\nCheck them first to understand the current test structure and avoid duplicates." : "")}}

        When ready to write feature files, use the GherkinWriter plugin to save them to: {{outputDir.FullName}}

        Always use your tools to scan for existing context before generating new features.
        After generating, save the files using the writer plugin.
        """);

    var settings = new OpenAIPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    };

    // If assembly or features were provided, prompt the agent to scan them first
    if (assemblyPath is not null || featuresDir is not null)
    {
        Console.WriteLine("🔍 Scanning existing context...");
        var scanPrompt = "";
        if (assemblyPath is not null)
            scanPrompt += $"Scan the assembly at '{assemblyPath.FullName}' for existing step definitions. ";
        if (featuresDir is not null)
            scanPrompt += $"List existing feature files at '{featuresDir.FullName}'. ";

        history.AddUserMessage(scanPrompt);
        var scanResult = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);
        history.AddAssistantMessage(scanResult.Content ?? "");
        Console.WriteLine(scanResult.Content);
        Console.WriteLine();
    }

    // If input file provided, process it non-interactively
    if (inputFile is not null)
    {
        var criteria = await File.ReadAllTextAsync(inputFile.FullName, cancellationToken);
        Console.WriteLine($"📄 Reading acceptance criteria from: {inputFile.FullName}");
        history.AddUserMessage($"Transform the following acceptance criteria into Gherkin feature file(s) and save them:\n\n{criteria}");

        Console.WriteLine("⏳ Generating Gherkin...\n");
        var response = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);
        Console.WriteLine(response.Content);
        Console.WriteLine($"\n✅ Done. Generated features saved to: {outputDir.FullName}");
        return 0;
    }

    // Interactive loop
    Console.WriteLine("Enter acceptance criteria (or 'quit' to exit):");
    Console.WriteLine("─────────────────────────────────────────────");

    while (!cancellationToken.IsCancellationRequested)
    {
        Console.Write("\n📋 AC> ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input)) continue;
        if (input.Equals("quit", StringComparison.OrdinalIgnoreCase)) break;

        history.AddUserMessage($"Transform the following acceptance criteria into Gherkin feature file(s) and save them:\n\n{input}");

        Console.WriteLine("\n⏳ Generating Gherkin...\n");
        var response = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);
        history.AddAssistantMessage(response.Content ?? "");
        Console.WriteLine(response.Content);
    }

    Console.WriteLine($"\n✅ Done. Generated features saved to: {outputDir.FullName}");
    return 0;
});

return await rootCommand.Parse(args).InvokeAsync();
