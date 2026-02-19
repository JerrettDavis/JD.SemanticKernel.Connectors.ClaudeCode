# Getting Started

## Prerequisites

- .NET 8 or .NET 10
- [Claude Code](https://claude.ai/download) installed and authenticated (`claude login`)
- **Or** an Anthropic API key (`sk-ant-api*`) if you prefer explicit key auth

## Installation

```shell
dotnet add package JD.SemanticKernel.Connectors.ClaudeCode
```

Also add Semantic Kernel if you haven't already:

```shell
dotnet add package Microsoft.SemanticKernel
```

---

## Minimal example — use your local Claude Code session

```csharp
using Microsoft.SemanticKernel;
using JD.SemanticKernel.Connectors.ClaudeCode;

var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion()   // reads ~/.claude/.credentials.json
    .Build();

var result = await kernel.InvokePromptAsync("Explain async/await in two sentences.");
Console.WriteLine(result);
```

That's it. The package reads your local Claude Code session automatically.

---

## Explicit API key override

If you prefer to supply a key directly — for CI/CD or environments without Claude Code installed:

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(apiKey: "sk-ant-api...")
    .Build();
```

---

## Choose a specific model

Use the `ClaudeModels` class for well-known model identifiers:

```csharp
using JD.SemanticKernel.Connectors.ClaudeCode;

var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(ClaudeModels.Opus)
    .Build();
```

Available constants: `ClaudeModels.Opus`, `ClaudeModels.Sonnet` (default), `ClaudeModels.Haiku`.

The default model is `claude-sonnet-4-6` (`ClaudeModels.Default`).

---

## Handle session errors

`ClaudeCodeSessionException` is thrown if no valid credentials are found or if the session has
expired. The message is safe to display to users:

```csharp
try
{
    var kernel = Kernel.CreateBuilder()
        .UseClaudeCodeChatCompletion()
        .Build();
}
catch (ClaudeCodeSessionException ex)
{
    Console.Error.WriteLine(ex.Message);
    // "Claude session expired at 2025-11-01 09:00 UTC. Run 'claude login' to refresh."
}
```

---

## Next steps

- [Credential Resolution](credential-resolution.md) — understand exactly how tokens are found
- [Kernel Builder Integration](kernel-builder-integration.md) — all overloads and options
- [Configuration Reference](configuration-reference.md) — JSON / environment variable config
