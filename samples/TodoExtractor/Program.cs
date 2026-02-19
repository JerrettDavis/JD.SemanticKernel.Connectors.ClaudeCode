using JD.SemanticKernel.Connectors.ClaudeCode;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// ──────────────────────────────────────────────────────────────────
// TodoExtractor — Minimal Semantic Kernel + Claude Code example
//
// Demonstrates:
//   • Direct use of the JD.SemanticKernel.Connectors.ClaudeCode
//     library (UseClaudeCodeChatCompletion one-liner)
//   • ClaudeModels constants for model selection
//   • Simple prompt-only workflow (no plugins)
//   • Structured JSON output from natural language
// ──────────────────────────────────────────────────────────────────

var messages = """
    Hey team — quick update from today's standup:

    @alice said she'll finish the auth middleware by Thursday,
    but needs the OpenAPI spec from @bob first. Bob mentioned
    he's blocked on the database migration that @carol started
    last week. Carol said she found a bug in the seed data and
    will push a fix today, but asked if someone can review the
    PR (#327) before EOD. Also, @dave volunteered to write the
    integration tests once the migration lands.

    Unrelated: the CI pipeline has been flaky on the Windows
    runner — someone should investigate the timeout on the
    E2E suite. @alice said she'll look at it if she has time
    Friday.
    """;

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║    TodoExtractor — Structured Todo Extraction Demo       ║");
Console.WriteLine("║    Powered by Semantic Kernel + Claude Code Auth         ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine();

var insecure = args.Contains("--insecure", StringComparer.OrdinalIgnoreCase);
var model = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? ClaudeModels.Default;
Console.WriteLine($"📋 Model: {model}");
Console.WriteLine();

var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(model, configure: insecure
        ? o => o.DangerouslyDisableSslValidation = true
        : null)
    .Build();

var chat = kernel.GetRequiredService<IChatCompletionService>();

var history = new ChatHistory();
history.AddSystemMessage("""
    You are a project manager assistant. Extract actionable
    todo items from the messages you receive.

    For each todo, output a JSON array with objects containing:
      - "assignee": who is responsible
      - "task": concise description
      - "blocked_by": what it depends on (null if unblocked)
      - "priority": "high", "medium", or "low"

    Output ONLY the JSON array, no markdown fences or prose.
    """);

history.AddUserMessage(messages);

Console.WriteLine("🔍 Extracting todos from standup notes...\n");

try
{
    var settings = new OpenAIPromptExecutionSettings();
    var response = await chat.GetChatMessageContentAsync(
        history, settings, kernel);

    Console.WriteLine("─────────────────────────────────────────────");
    Console.WriteLine("EXTRACTED TODOS");
    Console.WriteLine("─────────────────────────────────────────────");
    Console.WriteLine(response.Content);
    Console.WriteLine();
    Console.WriteLine("✅ Done");
    return 0;
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
