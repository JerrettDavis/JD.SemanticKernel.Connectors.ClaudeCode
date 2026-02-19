# API Surface

This document covers all **public types** in the `JD.SemanticKernel.Connectors.ClaudeCode`
namespace (the core NuGet package), plus the **plugin interfaces and extension points** exposed
by the sample CLI tools.

---

## Core Library — `JD.SemanticKernel.Connectors.ClaudeCode`

---

### `ClaudeCodeSessionProvider` _(sealed class : IDisposable)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ClaudeCodeSessionProvider.cs`

The central credential-resolution engine. Registered as a **singleton** in the DI container.
Thread-safe via `SemaphoreSlim` with double-checked locking on the credential cache.

```csharp
public sealed class ClaudeCodeSessionProvider : IDisposable
{
    // Constructor — DI-injected
    public ClaudeCodeSessionProvider(
        IOptions<ClaudeCodeSessionOptions> options,
        ILogger<ClaudeCodeSessionProvider> logger);

    // Returns the best available token string (API key or OAuth token).
    // Throws ClaudeCodeSessionException if no credentials are available or expired.
    public Task<string> GetTokenAsync(CancellationToken ct = default);

    // Returns full OAuth credential details, or null when the active
    // source is an API key or environment variable.
    // Uses an in-memory cache; invalidates when token is expired.
    public Task<ClaudeCodeOAuthCredentials?> GetCredentialsAsync(CancellationToken ct = default);

    public void Dispose();  // releases SemaphoreSlim
}
```

---

### `ClaudeCodeSessionHttpHandler` _(sealed class : DelegatingHandler)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ClaudeCodeSessionHttpHandler.cs`

An `HttpMessageHandler` that intercepts every outbound HTTP request and injects the correct
Anthropic authentication headers. Enforces HTTPS for all non-localhost traffic.

```csharp
public sealed class ClaudeCodeSessionHttpHandler : DelegatingHandler
{
    // Production constructor — wraps a new HttpClientHandler
    public ClaudeCodeSessionHttpHandler(ClaudeCodeSessionProvider provider);

    // internal testing constructor — accepts a custom inner handler
    internal ClaudeCodeSessionHttpHandler(ClaudeCodeSessionProvider provider, HttpMessageHandler innerHandler);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken);
}
```

**OAuth header set** (`sk-ant-oat*` tokens):
- `Authorization: Bearer {token}`
- `anthropic-beta: claude-code-20250219,oauth-2025-04-20,fine-grained-tool-streaming-2025-05-14`
- `user-agent: claude-cli/2.1.45 (external, cli)`
- `x-app: cli`

**API key header set** (`sk-ant-api*` tokens):
- `x-api-key: {token}`

---

### `ClaudeCodeHttpClientFactory` _(static class)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ClaudeCodeHttpClientFactory.cs`

Static factory for creating pre-authenticated `HttpClient` instances without a DI container.
The caller **owns** the returned `HttpClient` and must dispose it.

```csharp
public static class ClaudeCodeHttpClientFactory
{
    // Auto-resolves credentials (file → env vars)
    public static HttpClient Create();

    // Explicit API key or OAuth token
    public static HttpClient Create(string apiKey);

    // Full options control via delegate
    public static HttpClient Create(Action<ClaudeCodeSessionOptions>? configure);

    // DI scenario — use an already-constructed provider
    public static HttpClient Create(ClaudeCodeSessionProvider provider);
}
```

---

### `KernelBuilderExtensions` _(static class)_ `[net8.0+]`

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/KernelBuilderExtensions.cs`

> ⚠️ This class is compiled only for `net8.0` and `net10.0` targets (excluded from `netstandard2.0`
> via `#if !NETSTANDARD2_0`).

```csharp
public static class KernelBuilderExtensions
{
    public static IKernelBuilder UseClaudeCodeChatCompletion(
        this IKernelBuilder builder,
        string modelId = ClaudeModels.Default,  // "claude-sonnet-4-6"
        string? apiKey = null,
        Action<ClaudeCodeSessionOptions>? configure = null);
}
```

**What it does internally:**
1. Creates `ClaudeCodeSessionOptions` and applies `apiKey`/`configure`
2. Instantiates `ClaudeCodeSessionProvider` with `NullLogger`
3. Builds `HttpClient(new ClaudeCodeSessionHttpHandler(provider))`
4. Constructs `AnthropicClient("placeholder", httpClient)`
5. Builds `IChatClient` via `ChatClientBuilder` with `UseFunctionInvocation()`
6. Registers `IChatCompletionService` in `builder.Services` as singleton

---

### `ServiceCollectionExtensions` _(static class)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ServiceCollectionExtensions.cs`

```csharp
public static class ServiceCollectionExtensions
{
    // Bind options from IConfiguration "ClaudeSession" section
    public static IServiceCollection AddClaudeCodeAuthentication(
        this IServiceCollection services,
        IConfiguration configuration);

    // Configure options via delegate (or no-op if configure is null)
    public static IServiceCollection AddClaudeCodeAuthentication(
        this IServiceCollection services,
        Action<ClaudeCodeSessionOptions>? configure = null);
}
```

Both overloads register `ClaudeCodeSessionProvider` as **singleton**.

---

### `ClaudeCodeSessionOptions` _(sealed class)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ClaudeCodeSessionOptions.cs`

Options model. Bind from configuration section `"ClaudeSession"`.

```csharp
public sealed class ClaudeCodeSessionOptions
{
    public const string SectionName = "ClaudeSession";

    public string? CredentialsPath { get; set; }   // custom path; null = ~/.claude/.credentials.json
    public string? ApiKey { get; set; }             // sk-ant-api* — highest priority override
    public string? OAuthToken { get; set; }         // sk-ant-oat* — second priority override
}
```

---

### `ClaudeCodeCredentialsFile` _(sealed record)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ClaudeCodeCredentials.cs`

```csharp
public sealed record ClaudeCodeCredentialsFile
{
    [JsonPropertyName("claudeAiOauth")]
    public ClaudeCodeOAuthCredentials? ClaudeAiOauth { get; init; }
}
```

---

### `ClaudeCodeOAuthCredentials` _(sealed record)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ClaudeCodeCredentials.cs`

```csharp
public sealed record ClaudeCodeOAuthCredentials
{
    [JsonPropertyName("accessToken")]   public string AccessToken { get; init; }
    [JsonPropertyName("refreshToken")]  public string RefreshToken { get; init; }
    [JsonPropertyName("expiresAt")]     public long ExpiresAt { get; init; }      // Unix epoch ms
    [JsonPropertyName("scopes")]        public string[] Scopes { get; init; }
    [JsonPropertyName("subscriptionType")] public string? SubscriptionType { get; init; }
    [JsonPropertyName("rateLimitTier")] public string? RateLimitTier { get; init; }

    // Computed
    public DateTimeOffset ExpiresAtUtc { get; }  // DateTimeOffset.FromUnixTimeMilliseconds(ExpiresAt)
    public bool IsExpired { get; }               // DateTimeOffset.UtcNow >= ExpiresAtUtc
}
```

---

### `ClaudeCodeSessionException` _(sealed class : InvalidOperationException)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ClaudeCodeSessionException.cs`

User-displayable exception. The `Message` is always suitable for console output.

```csharp
public sealed class ClaudeCodeSessionException : InvalidOperationException
{
    public ClaudeCodeSessionException();
    public ClaudeCodeSessionException(string message);
    public ClaudeCodeSessionException(string message, Exception innerException);
}
```

Thrown in two cases by `ClaudeCodeSessionProvider`:
- No credentials file found → "Install Claude Code from https://claude.ai/download and run 'claude login'."
- Token expired → "Claude session expired at {time} UTC. Run 'claude login' to refresh your session."

---

### `ClaudeModels` _(static class)_

**File:** `src/JD.SemanticKernel.Connectors.ClaudeCode/ClaudeModels.cs`

```csharp
public static class ClaudeModels
{
    public const string Opus    = "claude-opus-4-6";
    public const string Sonnet  = "claude-sonnet-4-6";
    public const string Haiku   = "claude-haiku-4-5-20251001";
    public const string Default = Sonnet;  // "claude-sonnet-4-6"
}
```

---

## PullRequestReviewer — Extension Interfaces

### `IPullRequestProvider` _(interface : IDisposable)_

**File:** `samples/PullRequestReviewer/Abstractions/IPullRequestProvider.cs`

Implement this to add support for a new source control platform.

```csharp
public interface IPullRequestProvider : IDisposable
{
    string ProviderName { get; }

    Task<PullRequestInfo> GetPullRequestAsync(
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        CancellationToken ct = default);
}
```

**Implementations:**
- `GitHubPullRequestProvider` — uses Octokit
- `AzureDevOpsPullRequestProvider` — raw HttpClient + ADO REST API
- `GitLabPullRequestProvider` — raw HttpClient + GitLab REST API

---

### `IAnalyzer` _(interface)_

**File:** `samples/PullRequestReviewer/Abstractions/IAnalyzer.cs`

Implement this to add a new automated code analysis step.

```csharp
public interface IAnalyzer
{
    string Name { get; }

    Task<AnalyzerResult> AnalyzeAsync(
        IReadOnlyList<FileChange> files,
        CancellationToken ct = default);
}
```

**Implementation:** `DiffAnalyzer` — detects large diffs, high churn, potential secrets,
TODO/FIXME additions, and deleted test files.

---

### PR Domain Records

```csharp
record PullRequestInfo
{
    int Number; string Title; string Description; string Author;
    string SourceBranch; string TargetBranch;
    IReadOnlyList<FileChange> Files;
    string? CloneUrl;
}

record FileChange(string Path, ChangeType ChangeType, string Diff,
                  string? PreviousPath, int Additions, int Deletions)

record AnalyzerResult
{
    string AnalyzerName;
    IReadOnlyList<AnalyzerFinding> Findings;
    string? RawOutput;
}

record AnalyzerFinding(string FilePath, int? LineNumber, string Severity, string Message)

record ReviewComment
{
    string FilePath; int? LineNumber; ReviewSeverity Severity;
    string Category; string Body; string? SuggestedFix;
}

enum ChangeType      { Added, Modified, Deleted, Renamed }
enum ReviewSeverity  { Info, Warning, Error, Critical }
enum ReviewVerdict   { Approve, Comment, RequestChanges }
```

---

## Semantic Kernel Plugin Functions

The following `[KernelFunction]` methods are exposed to the LLM's tool-use loop:

### CodebaseExplorer Plugins

**`FileSystem` group** (`FileSystemPlugin`):
| Function | Description |
|----------|-------------|
| `get_directory_tree(rootPath, maxDepth)` | ASCII tree view, skips build artifacts |
| `detect_project_files(rootPath)` | Detects tech stack by manifest files |
| `read_file(filePath, maxLines)` | Reads source file, skips binaries |
| `count_lines_by_extension(rootPath)` | LoC stats grouped by extension |
| `search_files(rootPath, pattern)` | Glob file search, max 100 results |

**`CodeAnalysis` group** (`CodeAnalysisPlugin`):
| Function | Description |
|----------|-------------|
| `analyze_dotnet_project(csprojPath)` | Extracts TFMs, NuGet refs, project refs |
| `analyze_package_json(packageJsonPath)` | Extracts deps, devDeps, scripts |
| `find_entry_points(rootPath)` | Locates Program.cs, main.*, index.* etc. |
| `extract_namespaces_and_types(rootPath)` | Regex-based C# type scanner |
| `find_pattern_in_files(rootPath, pattern, extensionFilter?)` | Regex search across files, max 50 matches |

**`KnowledgeBase` group** (`KnowledgeBaseWriterPlugin`):
| Function | Description |
|----------|-------------|
| `write_knowledgebase_document(fileName, content)` | Write markdown to output dir |
| `append_to_document(filePath, content)` | Append to existing document |
| `list_knowledgebase_documents()` | List all .md files in output dir |
| `get_knowledgebase_template()` | Returns standard 5-doc template structure |

### GherkinGenerator Plugins

**`StepScanner` group** (`StepDefinitionScannerPlugin`):
| Function | Description |
|----------|-------------|
| `scan_assembly_for_step_definitions(assemblyPath)` | MetadataLoadContext scan for [Given]/[When]/[Then] |
| `list_step_keywords()` | Returns Gherkin keyword reference |

**`Features` group** (`FeatureFilePlugin`):
Reads and lists existing `.feature` files.

**`GherkinWriter` group** (`GherkinWriterPlugin`):
Writes generated `.feature` files to output directory.

### PullRequestReviewer Plugins

**`Review` group** (`CodeReviewPlugin`):
| Function | Description |
|----------|-------------|
| `get_pr_summary()` | PR metadata, branch info, file counts |
| `list_changed_files()` | Changed files with change types and line counts |
| `get_file_diff(filePath)` | Unified diff for a specific file |
| `get_analyzer_findings()` | All findings from pre-run analyzers |
| `add_review_comment(filePath, severity, category, comment, lineNumber?, suggestedFix?)` | Records structured review comment |
| `get_review_summary()` | All comments grouped by severity |

**`Git` group** (`GitPlugin`):
Utility git helper functions for the review agent.
