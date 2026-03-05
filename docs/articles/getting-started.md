# Getting Started

## Prerequisites

- .NET 8 or .NET 10
- An Anthropic API key (`sk-ant-api*`) for standard usage
- Optional: [Claude Code](https://claude.ai/download) installed and authenticated (`claude login`) for local interactive OAuth usage

## Installation

```shell
dotnet add package JD.SemanticKernel.Connectors.ClaudeCode
```

Also add Semantic Kernel if you haven't already:

```shell
dotnet add package Microsoft.SemanticKernel
```

---

## Minimal example — API key

```csharp
using Microsoft.SemanticKernel;
using JD.SemanticKernel.Connectors.ClaudeCode;

var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(apiKey: "sk-ant-api...")
    .Build();

var result = await kernel.InvokePromptAsync("Explain async/await in two sentences.");
Console.WriteLine(result);
```

That's it. API-key authentication is the default and recommended path.

---

## Optional local OAuth (interactive only)

OAuth support is disabled by default. Enable it explicitly for local interactive sessions:

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(configure: o =>
    {
        o.EnableOAuthTokenSupport = true;
    })
    .Build();
```

For unattended or automated workflows, use API keys instead of OAuth.

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
