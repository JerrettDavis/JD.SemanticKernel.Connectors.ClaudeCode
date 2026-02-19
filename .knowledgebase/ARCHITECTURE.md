# Architecture

## Overview

The repository is organized as a **single-library + samples** monorepo. The core
`JD.SemanticKernel.Connectors.ClaudeCode` library is an authentication/integration shim sitting
between Anthropic's API and Microsoft Semantic Kernel. The sample CLI tools build on top of it to
demonstrate practical agentic workflows.

---

## High-Level Component Diagram

```mermaid
graph TB
    subgraph "Core Library (NuGet)"
        SCE["ServiceCollectionExtensions\n(IServiceCollection.AddClaudeCodeAuthentication)"]
        KBE["KernelBuilderExtensions\n(IKernelBuilder.UseClaudeCodeChatCompletion)\n[net8.0+ only]"]
        CSP["ClaudeCodeSessionProvider\n(credential resolution + caching)"]
        CSH["ClaudeCodeSessionHttpHandler\n(DelegatingHandler — injects auth headers)"]
        CHCF["ClaudeCodeHttpClientFactory\n(static factory — BYOC scenarios)"]
        CSO["ClaudeCodeSessionOptions\n(config model)"]
        CCF["ClaudeCodeCredentialsFile\n(JSON model for ~/.claude/.credentials.json)"]
        COC["ClaudeCodeOAuthCredentials\n(OAuth token details + expiry)"]
        CM["ClaudeModels\n(well-known model ID constants)"]
        EX["ClaudeCodeSessionException\n(user-displayable error)"]
    end

    subgraph "External"
        ANTHROPIC["api.anthropic.com"]
        CLAUDE_FILE["~/.claude/.credentials.json"]
        ENV["Environment Variables\n(ANTHROPIC_API_KEY\nCLAUDE_CODE_OAUTH_TOKEN)"]
    end

    subgraph "Semantic Kernel Host"
        SK["Microsoft.SemanticKernel\n(Kernel + IChatCompletionService)"]
        ASDK["Anthropic.SDK\n(AnthropicClient)"]
        MEAI["Microsoft.Extensions.AI\n(ChatClientBuilder)"]
    end

    SCE --> CSP
    KBE --> CSP
    KBE --> CSH
    KBE --> ASDK
    KBE --> MEAI
    CHCF --> CSP
    CHCF --> CSH
    CSP --> CSO
    CSP --> CLAUDE_FILE
    CSP --> ENV
    CSP --> CCF
    CCF --> COC
    CSH --> CSP
    CSH --> ANTHROPIC
    MEAI --> SK
    ASDK --> CSH
```

---

## Credential Resolution Flow

```mermaid
flowchart TD
    A[GetTokenAsync called] --> B{ApiKey in options?}
    B -- Yes --> Z[Return ApiKey]
    B -- No --> C{OAuthToken in options?}
    C -- Yes --> Z2[Return OAuthToken]
    C -- No --> D{ANTHROPIC_API_KEY env var?}
    D -- Yes --> Z3[Return env var value]
    D -- No --> E{CLAUDE_CODE_OAUTH_TOKEN env var?}
    E -- Yes --> Z4[Return env var value]
    E -- No --> F[Read ~/.claude/.credentials.json]
    F --> G{File exists?}
    G -- No --> H[Throw ClaudeCodeSessionException\n'Install Claude Code and run claude login']
    G -- Yes --> I{Token expired?}
    I -- Yes --> J[Throw ClaudeCodeSessionException\n'Run claude login to refresh']
    I -- No --> K[Return accessToken from file]
```

---

## HTTP Request Authentication Flow

```mermaid
sequenceDiagram
    participant SDK as Anthropic.SDK
    participant H as ClaudeCodeSessionHttpHandler
    participant P as ClaudeCodeSessionProvider
    participant API as api.anthropic.com

    SDK->>H: SendAsync(request, ct)
    H->>H: Assert HTTPS (or localhost)
    H->>P: GetTokenAsync(ct)
    P-->>H: token string
    alt token starts with "sk-ant-oat"
        H->>H: Remove x-api-key header
        H->>H: Set Authorization: Bearer {token}
        H->>H: Set anthropic-beta: claude-code-20250219,oauth-2025-04-20,...
        H->>H: Set user-agent: claude-cli/2.1.45 (external, cli)
        H->>H: Set x-app: cli
    else token starts with "sk-ant-api"
        H->>H: Remove x-api-key
        H->>H: Set x-api-key: {token}
    end
    H->>API: Forwarded request with correct auth headers
    API-->>H: Response
    H-->>SDK: Response
```

---

## DI Integration Architecture

### Path 1 — `UseClaudeCodeChatCompletion()` (net8.0+ only)

This is the **simplest path**, requiring only one method call. It bypasses the DI container entirely
and wires up the complete authentication → HTTP → Anthropic SDK → Semantic Kernel chain internally.

```
IKernelBuilder
  └── UseClaudeCodeChatCompletion(modelId, apiKey?, configure?)
        ├── new ClaudeCodeSessionOptions()
        ├── new ClaudeCodeSessionProvider(Options.Create(options), NullLogger)
        ├── new HttpClient(new ClaudeCodeSessionHttpHandler(provider))
        ├── new AnthropicClient("placeholder", httpClient)
        ├── ChatClientBuilder(anthropicClient.Messages)
        │     .ConfigureOptions(o => o.ModelId ??= modelId)
        │     .UseFunctionInvocation()
        │     .Build()
        └── builder.Services.AddSingleton<IChatCompletionService>(chatClient.AsChatCompletionService())
```

> **Note:** `KernelBuilderExtensions.cs` is wrapped in `#if !NETSTANDARD2_0`, so it is **not
> compiled** for the `netstandard2.0` target. For netstandard2.0 hosts, use the
> `ServiceCollectionExtensions` + `ClaudeCodeHttpClientFactory` path instead.

### Path 2 — `AddClaudeCodeAuthentication()` (all TFMs)

For ASP.NET Core or Generic Host applications. Registers `ClaudeCodeSessionProvider` as a
singleton in the DI container. You then construct `ClaudeCodeSessionHttpHandler` and your
Anthropic SDK client manually, or use `ClaudeCodeHttpClientFactory.Create(provider)`.

```
IServiceCollection
  ├── Configure<ClaudeCodeSessionOptions>(...)      ← from delegate or IConfiguration
  └── AddSingleton<ClaudeCodeSessionProvider>()
```

### Path 3 — `ClaudeCodeHttpClientFactory.Create()` (all TFMs, no DI)

Standalone static factory for console apps or scenarios without a DI container. Returns a
fully-configured `HttpClient`. The caller is responsible for disposing it.

---

## Sample Tools Architecture

Each sample tool follows the same pattern:

```
Program.cs (System.CommandLine root command)
  ├── Parse CLI arguments
  ├── Kernel.CreateBuilder()
  │     ├── .UseClaudeCodeChatCompletion(model)
  │     └── .Plugins.AddFromObject(...)   ← domain-specific SK plugins
  ├── kernel.Build()
  ├── ChatHistory  ← system prompt + user message
  └── chat.GetChatMessageContentAsync(history, settings, kernel, ct)
        └── FunctionChoiceBehavior.Auto()  ← agentic tool-use loop
```

### CodebaseExplorer (`jdxplr`)

Agentic codebase profiler. Exposes three SK plugin groups to the LLM:

| Plugin Class | SK Group Name | Capabilities |
|---|---|---|
| `FileSystemPlugin` | `FileSystem` | Directory tree, file read, line counts, file search |
| `CodeAnalysisPlugin` | `CodeAnalysis` | .csproj analysis, namespace extraction, pattern search, entry points |
| `KnowledgeBaseWriterPlugin` | `KnowledgeBase` | Write/append/list markdown documents |

### GherkinGenerator (`jdgerkinator`)

Acceptance criteria → Gherkin feature file generator. Supports interactive REPL and file input.

| Plugin Class | SK Group Name | Capabilities |
|---|---|---|
| `StepDefinitionScannerPlugin` | `StepScanner` | Scan .NET assemblies for Reqnroll/SpecFlow step defs (MetadataLoadContext) |
| `FeatureFilePlugin` | `Features` | Read/list existing .feature files |
| `GherkinWriterPlugin` | `GherkinWriter` | Write .feature files to output directory |

### PullRequestReviewer (`jdpr`)

Multi-provider AI code review agent. Runs an analyzer pipeline before handing off to the LLM.

```
Provider Selection (--provider flag)
  ├── GitHub  → GitHubPullRequestProvider (Octokit)
  ├── ADO     → AzureDevOpsPullRequestProvider (HttpClient + REST)
  └── GitLab  → GitLabPullRequestProvider (HttpClient + REST)
         ↓
PullRequestInfo (normalized cross-platform PR model)
         ↓
IAnalyzer pipeline (extensible)
  └── DiffAnalyzer  → detects large diffs, high churn, secrets, TODOs, deleted tests
         ↓
SK Kernel with CodeReviewPlugin + GitPlugin
         ↓
Structured review with severity counts + verdict
  (Approve / RequestChanges / Comment)
```

---

## Data Models

### Core Library

```
ClaudeCodeSessionOptions        ← configuration input
    .ApiKey?                    ← explicit override
    .OAuthToken?                ← explicit OAuth override
    .CredentialsPath?           ← custom path to credentials file

ClaudeCodeCredentialsFile       ← JSON root of ~/.claude/.credentials.json
    .ClaudeAiOauth?             → ClaudeCodeOAuthCredentials

ClaudeCodeOAuthCredentials      ← deserialized OAuth token data
    .AccessToken
    .RefreshToken
    .ExpiresAt                  ← Unix epoch ms
    .Scopes[]
    .SubscriptionType?
    .RateLimitTier?
    .ExpiresAtUtc               ← computed DateTimeOffset
    .IsExpired                  ← computed bool
```

### PullRequestReviewer Abstractions

```
PullRequestInfo
    .Number / .Title / .Description / .Author
    .SourceBranch / .TargetBranch
    .Files: IReadOnlyList<FileChange>
    .CloneUrl?

FileChange
    .Path / .ChangeType / .Diff / .PreviousPath?
    .Additions / .Deletions

AnalyzerResult
    .AnalyzerName
    .Findings: IReadOnlyList<AnalyzerFinding>
    .RawOutput?

AnalyzerFinding(FilePath, LineNumber?, Severity, Message)

ReviewComment
    .FilePath / .LineNumber? / .Severity / .Category
    .Body / .SuggestedFix?
```

---

## Security Considerations

- `ClaudeCodeSessionHttpHandler` **enforces HTTPS** for all outbound requests (localhost is
  exempted for testing). Any non-HTTPS target throws `InvalidOperationException`.
- `FileSystemPlugin` in CodebaseExplorer enforces a **path traversal guard** — all file reads are
  validated to remain within the configured codebase root.
- `KnowledgeBaseWriterPlugin` enforces a **path traversal guard** — all writes are validated to
  remain within the configured output directory (`UnauthorizedAccessException` on violation).
- OAuth tokens are **never logged** — only expiry time and tier metadata are emitted to the logger.
- The `ClaudeCodeSessionProvider` credential cache uses a `SemaphoreSlim` with double-checked
  locking to prevent duplicate file reads under concurrent load.
