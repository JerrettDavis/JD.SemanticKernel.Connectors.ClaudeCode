# Sample Tools

Three production-ready CLI tools demonstrate how
`JD.SemanticKernel.Connectors.ClaudeCode` enables agentic workflows powered by
Claude. Each tool is distributed as a [.NET global tool](https://learn.microsoft.com/dotnet/core/tools/global-tools)
and follows the same core architecture.

---

## Tools at a glance

| Tool | Command | Purpose |
|---|---|---|
| [Gherkin Generator](gherkin-generator.md) | `jdgerkinator` | Converts acceptance criteria into Gherkin `.feature` files |
| [PR Review Agent](pull-request-reviewer.md) | `jdpr` | AI-powered pull request review for GitHub, Azure DevOps, and GitLab |
| [Codebase Explorer](codebase-explorer.md) | `jdxplr` | Profiles a codebase and generates markdown knowledgebase documentation |
| [Todo Extractor](todo-extractor.md) | *(library demo)* | Extracts structured todos from natural language using the library directly |

---

## Installation

All three tools are published alongside the main NuGet package and share the same
version (managed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)):

```shell
# Install all tools
dotnet tool install -g JD.Tools.GherkinGenerator
dotnet tool install -g JD.Tools.PullRequestReviewer
dotnet tool install -g JD.Tools.CodebaseExplorer
```

---

## Common architecture

Every sample follows the same pattern:

```
┌────────────────────────────────────────────────────┐
│  System.CommandLine CLI                            │
│  Parses options → delegates to Semantic Kernel     │
├────────────────────────────────────────────────────┤
│  Semantic Kernel + Claude Code Auth                │
│  UseClaudeCodeChatCompletion() — one-line setup    │
├────────────────────────────────────────────────────┤
│  Domain Plugins (KernelFunction methods)           │
│  File I/O, API calls, analysis, writing            │
├────────────────────────────────────────────────────┤
│  Agentic Loop                                      │
│  FunctionChoiceBehavior.Auto() drives tool calls   │
└────────────────────────────────────────────────────┘
```

1. **CLI layer** — `System.CommandLine 2.0.3` handles argument parsing, help text,
   and version display.
2. **Kernel setup** — A single call to `UseClaudeCodeChatCompletion()` wires
   authentication transparently (OAuth session or API key).
3. **Plugins** — Domain-specific `[KernelFunction]` methods exposed to the model
   as callable tools.
4. **Agentic execution** — `FunctionChoiceBehavior.Auto()` lets Claude
   autonomously select and invoke plugins in a structured workflow.

---

## Prerequisites

- .NET 8.0 or .NET 10.0 SDK
- [Claude Code](https://claude.ai/download) installed and authenticated (`claude login`), **or** an
  `ANTHROPIC_API_KEY` environment variable
- Git (for PR Review Agent's git operations)

---

## Building from source

```shell
git clone https://github.com/JerrettDavis/JD.SemanticKernel.Connectors.ClaudeCode.git
cd JD.SemanticKernel.Connectors.ClaudeCode

# Build all projects
dotnet build

# Run a sample directly
dotnet run --project samples/GherkinGenerator --framework net8.0 -- --help
dotnet run --project samples/PullRequestReviewer --framework net8.0 -- --help
dotnet run --project samples/CodebaseExplorer --framework net8.0 -- --help

# Run the TodoExtractor demo
dotnet run --project samples/TodoExtractor --framework net8.0

# Pack as tools
dotnet pack
```

---

## Versioning

All tools share the repository version managed by **Nerdbank.GitVersioning**.
The `version.json` at the repository root controls the version for every package:

```json
{
  "version": "0.1",
  "publicReleaseRefSpec": [
    "^refs/heads/main$",
    "^refs/tags/v\\d+\\.\\d+"
  ]
}
```

CI builds automatically stamp the correct semantic version into every NuGet
package and tool manifest.
