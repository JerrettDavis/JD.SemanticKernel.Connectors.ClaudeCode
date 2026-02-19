# TodoExtractor — Library Usage Demo

The **TodoExtractor** sample demonstrates how to use
`JD.SemanticKernel.Connectors.ClaudeCode` directly as a library —
no dotnet tool packaging, no plugins, just a minimal Semantic Kernel
chat completion call.

---

## What it does

Feeds a block of natural-language standup notes into Claude and
receives back a structured JSON array of actionable todo items, each
with an assignee, task description, blocker, and priority.

This showcases:

- **One-liner kernel setup** via `UseClaudeCodeChatCompletion()`
- **`ClaudeModels` constants** for discoverable model selection
- **Prompt-only workflow** (no plugins or function calling)
- **Structured output** from unstructured text

---

## Running

```shell
# From the repository root
dotnet run --project samples/TodoExtractor --framework net8.0

# Optionally pass a different model
dotnet run --project samples/TodoExtractor --framework net8.0 -- claude-haiku-4-5-20251001
```

---

## Example output

```
╔═══════════════════════════════════════════════════════════╗
║    TodoExtractor — Structured Todo Extraction Demo       ║
║    Powered by Semantic Kernel + Claude Code Auth         ║
╚═══════════════════════════════════════════════════════════╝

📋 Model: claude-sonnet-4-6

🔍 Extracting todos from standup notes...

─────────────────────────────────────────────
EXTRACTED TODOS
─────────────────────────────────────────────
[
  {
    "assignee": "carol",
    "task": "Push fix for bug in seed data",
    "blocked_by": null,
    "priority": "high"
  },
  {
    "assignee": "team",
    "task": "Review PR #327 before EOD",
    "blocked_by": null,
    "priority": "high"
  },
  {
    "assignee": "bob",
    "task": "Complete database migration",
    "blocked_by": "Carol's seed data fix (PR #327)",
    "priority": "high"
  },
  ...
]

✅ Done
```

---

## Key code

The entire sample is a single `Program.cs`:

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(ClaudeModels.Default)
    .Build();

var chat = kernel.GetRequiredService<IChatCompletionService>();

var history = new ChatHistory();
history.AddSystemMessage("You are a project manager assistant...");
history.AddUserMessage(standupNotes);

var response = await chat.GetChatMessageContentAsync(history);
Console.WriteLine(response.Content);
```

No plugins, no function calling, no tool registration — just
`UseClaudeCodeChatCompletion()` and a prompt.
