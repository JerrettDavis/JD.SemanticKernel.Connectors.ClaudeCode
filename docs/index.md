---
_layout: landing
---

# JD.SemanticKernel.Connectors.ClaudeCode

Wire your local **Claude Code** session — or a standard Anthropic API key — into any
[Semantic Kernel](https://learn.microsoft.com/semantic-kernel) application with a single
extension method call.

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion()   // reads ~/.claude/.credentials.json automatically
    .Build();
```

No tokens to manage. No configuration files to maintain. If you are logged in to Claude Code
locally (`claude login`), the package finds and validates your session automatically.

---

## Why this package?

Claude Code uses **OAuth tokens** (`sk-ant-oat*`) that require a special HTTP request signature
before `api.anthropic.com` will accept them. The standard `x-api-key` header flow does not work
for these tokens. This package handles that signature transparently — including the
`anthropic-beta`, `user-agent`, and `x-app` headers required by the OAuth path — while falling
back cleanly to standard API key auth when a `sk-ant-api*` key is supplied instead.

---

## Quick links

| Topic | Description |
|---|---|
| [Getting Started](articles/getting-started.md) | Minimal working example — SK kernel in five lines |
| [Credential Resolution](articles/credential-resolution.md) | How tokens are found and which source wins |
| [Kernel Builder Integration](articles/kernel-builder-integration.md) | Full `UseClaudeCodeChatCompletion()` reference |
| [Service Collection Integration](articles/service-collection-integration.md) | DI/`IServiceCollection` registration |
| [HTTP Client Factory](articles/http-client-factory.md) | Bring-your-own-provider scenarios |
| [Configuration Reference](articles/configuration-reference.md) | All options, environment variables, JSON config |
| [Sample Tools](samples/index.md) | Four sample projects demonstrating agentic workflows |
| [API Reference](api/index.md) | Full generated API docs |

---

## Sample tools

Four sample projects built on this package demonstrate real-world
agentic workflows:

| Tool | Command | Description |
|---|---|---|
| [Gherkin Generator](samples/gherkin-generator.md) | `jdgerkinator` | Acceptance criteria → `.feature` files |
| [PR Review Agent](samples/pull-request-reviewer.md) | `jdpr` | AI code review for GitHub / ADO / GitLab |
| [Codebase Explorer](samples/codebase-explorer.md) | `jdxplr` | Profile a codebase → markdown knowledgebase |
| [Todo Extractor](samples/todo-extractor.md) | *(library demo)* | Extract structured todos from natural language |

See the [Sample Tools](samples/index.md) overview for installation and
architecture details.

---

## Supported targets

| TFM | SK extensions | Core auth types |
|---|---|---|
| `netstandard2.0` | — | ✓ |
| `net8.0` | ✓ | ✓ |
| `net10.0` | ✓ | ✓ |
