# JD.SemanticKernel.Connectors.ClaudeCode

[![NuGet](https://img.shields.io/nuget/v/JD.SemanticKernel.Connectors.ClaudeCode.svg)](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.ClaudeCode)
[![NuGet Downloads](https://img.shields.io/nuget/dt/JD.SemanticKernel.Connectors.ClaudeCode.svg)](https://www.nuget.org/packages/JD.SemanticKernel.Connectors.ClaudeCode)
[![CI](https://github.com/JerrettDavis/JD.SemanticKernel.Connectors.ClaudeCode/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/JD.SemanticKernel.Connectors.ClaudeCode/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JerrettDavis/JD.SemanticKernel.Connectors.ClaudeCode/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/JerrettDavis/JD.SemanticKernel.Connectors.ClaudeCode/actions/workflows/codeql-analysis.yml)
[![codecov](https://codecov.io/gh/JerrettDavis/JD.SemanticKernel.Connectors.ClaudeCode/graph/badge.svg)](https://codecov.io/gh/JerrettDavis/JD.SemanticKernel.Connectors.ClaudeCode)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A **Semantic Kernel connector** that bridges your local [Claude Code](https://claude.ai/download) OAuth session into Microsoft Semantic Kernel — no manual API key management needed.

## Features

- **Zero-config authentication** — automatically reads `~/.claude/.credentials.json`
- **Multi-source credential resolution** — options → env vars → local session, in priority order
- **OAuth & API key support** — handles `sk-ant-oat*` (Bearer) and `sk-ant-api*` (x-api-key) tokens
- **Full Semantic Kernel integration** — `IKernelBuilder.UseClaudeCodeChatCompletion()` one-liner
- **DI-friendly** — `IServiceCollection.AddClaudeCodeAuthentication()` for ASP.NET Core / Generic Host
- **Broad TFM support** — `netstandard2.0`, `net8.0`, `net10.0`

## Quick Start

### Install

```bash
dotnet add package JD.SemanticKernel.Connectors.ClaudeCode
```

### Kernel Builder (Recommended)

```csharp
using JD.SemanticKernel.Connectors.ClaudeCode;

var builder = Kernel.CreateBuilder();
builder.UseClaudeCodeChatCompletion(); // defaults to ClaudeModels.Default (Sonnet)
var kernel = builder.Build();

var result = await kernel.InvokePromptAsync("Hello, Claude!");
Console.WriteLine(result);
```

### Service Collection (ASP.NET Core)

```csharp
builder.Services.AddClaudeCodeAuthentication(options =>
{
    options.CredentialsPath = "/custom/path/.credentials.json"; // optional
});
```

### Configuration Binding

```json
{
  "ClaudeSession": {
    "ApiKey": null,
    "OAuthToken": null,
    "CredentialsPath": null
  }
}
```

```csharp
builder.Services.AddClaudeCodeAuthentication(builder.Configuration);
```

## Credential Resolution Order

| Priority | Source | Description |
|----------|--------|-------------|
| 1 | `ClaudeSession:ApiKey` | Explicit API key in options/config |
| 2 | `ClaudeSession:OAuthToken` | Explicit OAuth token in options/config |
| 3 | `ANTHROPIC_API_KEY` env var | Environment variable |
| 4 | `CLAUDE_CODE_OAUTH_TOKEN` env var | Environment variable |
| 5 | `~/.claude/.credentials.json` | Claude Code local session file |

## Sample CLI Tools

This repo includes sample projects demonstrating agentic workflows with Semantic Kernel:

| Tool | Command | Description |
|------|---------|-------------|
| **Gherkin Generator** | `jdgerkinator` | Converts acceptance criteria into Gherkin/Reqnroll specs |
| **PR Review Agent** | `jdpr` | Multi-provider PR review (GitHub, Azure DevOps, GitLab) |
| **Codebase Explorer** | `jdxplr` | Profiles codebases into structured knowledgebases |
| **Todo Extractor** | *(library demo)* | Extracts structured todos from natural language |

Install the CLI tools as global tools:

```bash
dotnet tool install -g JD.Tools.GherkinGenerator
dotnet tool install -g JD.Tools.PullRequestReviewer
dotnet tool install -g JD.Tools.CodebaseExplorer
```

## Models

Well-known model constants are available via `ClaudeModels`:

```csharp
builder.UseClaudeCodeChatCompletion(ClaudeModels.Opus);   // claude-opus-4-6
builder.UseClaudeCodeChatCompletion(ClaudeModels.Sonnet);  // claude-sonnet-4-6 (default)
builder.UseClaudeCodeChatCompletion(ClaudeModels.Haiku);   // claude-haiku-4-5
```

## Documentation

Full documentation is available at the [DocFX site](docs/) including:

- [Getting Started](docs/articles/getting-started.md)
- [Credential Resolution](docs/articles/credential-resolution.md)
- [Kernel Builder Integration](docs/articles/kernel-builder-integration.md)
- [Service Collection Integration](docs/articles/service-collection-integration.md)
- [HttpClientFactory](docs/articles/http-client-factory.md)
- [Configuration Reference](docs/articles/configuration-reference.md)
- [Sample Tools Guide](docs/samples/index.md)

## Building

```bash
dotnet build
dotnet test
```

### Build Documentation

```bash
cd docs
dotnet tool restore
dotnet docfx docfx.json
```

## Shared Abstractions

This connector implements the **JD.SemanticKernel.Connectors.Abstractions** interfaces,
enabling multi-provider bridging:

| Interface | Implementation |
|---|---|
| `ISessionProvider` | `ClaudeCodeSessionProvider` — credential resolution with `IsAuthenticatedAsync()` |
| `IModelDiscoveryProvider` | `ClaudeModelDiscovery` — returns known Claude model catalogue |
| `SessionOptionsBase` | `ClaudeCodeSessionOptions` — inherits `DangerouslyDisableSslValidation`, `CustomEndpoint` |

Use the same abstractions across providers:

```csharp
ISessionProvider provider = isClaudeCode
    ? claudeCodeProvider
    : copilotProvider;

var creds = await provider.GetCredentialsAsync();
```

### Related Projects

- **[JD.SemanticKernel.Connectors.GitHubCopilot](https://github.com/JerrettDavis/JD.SemanticKernel.Connectors.GitHubCopilot)** — Same pattern for GitHub Copilot subscriptions
- **[JD.SemanticKernel.Extensions](https://github.com/JerrettDavis/JD.SemanticKernel.Extensions)** — SK extensions for skills, hooks, plugins, compaction, and semantic memory

## License

[MIT](LICENSE)
