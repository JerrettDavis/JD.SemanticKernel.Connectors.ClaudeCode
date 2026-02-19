# Patterns & Conventions

This document describes the recurring coding patterns, architectural decisions, and conventions
used throughout this codebase. It is intended to help new contributors quickly understand the
style and idioms in use.

---

## 1. Multi-Source Credential Resolution (Priority Chain)

**Where:** `ClaudeCodeSessionProvider.GetTokenAsync()` (`src/.../ClaudeCodeSessionProvider.cs:43`)

The credential resolution follows a strict **waterfall priority chain** — each source is checked
in order and the first non-null, non-whitespace value wins:

```csharp
// Priority 1 — explicit options
if (!string.IsNullOrWhiteSpace(_options.ApiKey)) return _options.ApiKey!;
if (!string.IsNullOrWhiteSpace(_options.OAuthToken)) return _options.OAuthToken!;

// Priority 2 — environment variables
var envApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (!string.IsNullOrWhiteSpace(envApiKey)) return envApiKey!;

// Priority 3 — local credentials file
return await ExtractFromCredentialsFileAsync(ct);
```

**Benefit:** Allows progressive overrides without code changes — local development uses the file,
CI/CD uses env vars, and explicit injection (e.g., tests) uses options.

---

## 2. Thread-Safe Double-Checked Locking for Credential Cache

**Where:** `ClaudeCodeSessionProvider.GetCredentialsAsync()` (`src/.../ClaudeCodeSessionProvider.cs:78`)

```csharp
private readonly SemaphoreSlim _cacheLock = new(1, 1);
private volatile ClaudeCodeOAuthCredentials? _cached;

public async Task<ClaudeCodeOAuthCredentials?> GetCredentialsAsync(CancellationToken ct = default)
{
    // Fast path — no lock needed if cache is valid
    var snapshot = _cached;
    if (snapshot is not null && !snapshot.IsExpired)
        return snapshot;

    await _cacheLock.WaitAsync(ct).ConfigureAwait(false);
    try
    {
        // Double-check after acquiring lock
        if (_cached is not null && !_cached.IsExpired)
            return _cached;

        var file = await ReadCredentialsFileAsync(ct).ConfigureAwait(false);
        _cached = file?.ClaudeAiOauth;
        return _cached;
    }
    finally { _cacheLock.Release(); }
}
```

**Pattern:** Async-compatible double-checked locking via `SemaphoreSlim(1,1)` + `volatile` field.
Avoids the `lock` keyword (which cannot be used with `await`) while maintaining thread safety.

---

## 3. DelegatingHandler for Transparent HTTP Authentication

**Where:** `ClaudeCodeSessionHttpHandler` (`src/.../ClaudeCodeSessionHttpHandler.cs`)

Rather than touching Anthropic SDK internals, authentication is injected via a standard
`DelegatingHandler` in the `HttpClient` pipeline:

```csharp
public sealed class ClaudeCodeSessionHttpHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Enforce HTTPS
        // Get token from provider
        // Mutate request headers based on token type
        return await base.SendAsync(request, cancellationToken);
    }
}
```

**Benefits:**
- Zero coupling to the Anthropic SDK's internals
- Testable by injecting a mock `innerHandler` (the internal constructor accepts one)
- Token-type detection (`sk-ant-oat` vs `sk-ant-api`) and header strategy are co-located

---

## 4. `#if` Conditional Compilation for TFM-Specific Features

**Where:** `KernelBuilderExtensions.cs`, `ServiceCollectionExtensions.cs`, `ClaudeCodeSessionProvider.cs`

```csharp
// KernelBuilderExtensions.cs — entire file excluded on netstandard2.0
#if !NETSTANDARD2_0
// ... UseClaudeCodeChatCompletion ...
#endif

// ServiceCollectionExtensions.cs — null-arg guard style varies
#if NETSTANDARD2_0
if (services is null) throw new ArgumentNullException(nameof(services));
#else
ArgumentNullException.ThrowIfNull(services);
#endif

// ClaudeCodeSessionProvider.cs — async file I/O differs
#if NETSTANDARD2_0
var json = await Task.Run(() => File.ReadAllText(path), ct).ConfigureAwait(false);
return JsonSerializer.Deserialize<ClaudeCodeCredentialsFile>(json);
#else
await using var stream = File.OpenRead(path);
return await JsonSerializer.DeserializeAsync<ClaudeCodeCredentialsFile>(stream, cancellationToken: ct)
    .ConfigureAwait(false);
#endif
```

**Convention:** All polyfills and TFM guards are clearly commented and isolated to the minimum
necessary scope. `Polyfills.cs` handles the `IsExternalInit` compiler requirement for records
on netstandard2.0.

---

## 5. Extension Method Pattern for Integration Registration

**Where:** `ServiceCollectionExtensions.cs`, `KernelBuilderExtensions.cs`

The library follows the established .NET extension method convention for DI registration:

```csharp
// Fluent chaining on IServiceCollection
services.AddClaudeCodeAuthentication(o => o.ApiKey = "...")
        .AddSomeOtherService();

// Fluent chaining on IKernelBuilder
Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion()
    .Build();
```

Both overloads support the dual registration pattern — `IConfiguration` binding (for
`appsettings.json`) and `Action<TOptions>` delegate (for code-based configuration).

---

## 6. Static Factory Pattern (`ClaudeCodeHttpClientFactory`)

**Where:** `ClaudeCodeHttpClientFactory.cs`

For scenarios without a DI container, a static factory provides named overloads:

```csharp
ClaudeCodeHttpClientFactory.Create()                    // auto credentials
ClaudeCodeHttpClientFactory.Create("sk-ant-api...")     // explicit key
ClaudeCodeHttpClientFactory.Create(o => o.Key = "...")  // delegate config
ClaudeCodeHttpClientFactory.Create(provider)            // existing provider (DI)
```

**Note:** The factory does **not** implement `IHttpClientFactory`. This is intentional — it is a
lightweight convenience for console apps. In ASP.NET Core hosts, the DI path is preferred.

---

## 7. Agentic Plugin Architecture (Semantic Kernel)

**Where:** All three sample tools

Each sample follows the same agent construction pattern:

```csharp
// 1. Build kernel with auth + plugins
var builder = Kernel.CreateBuilder();
builder.UseClaudeCodeChatCompletion(model);
builder.Plugins.AddFromObject(new MyPlugin(...), "GroupName");

// 2. Construct chat history with a structured system prompt
var history = new ChatHistory();
history.AddSystemMessage("You are an expert...");
history.AddUserMessage("Do this task...");

// 3. Enable auto function calling
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// 4. Single call — LLM drives the tool-use loop
var response = await chat.GetChatMessageContentAsync(history, settings, kernel, ct);
```

`FunctionChoiceBehavior.Auto()` lets Semantic Kernel orchestrate multi-turn tool calls without
explicit application-level loops. The LLM decides which functions to call, in what order, and
when it has sufficient information to produce a final answer.

---

## 8. Provider + Analyzer Pipeline (PullRequestReviewer)

**Where:** `samples/PullRequestReviewer/Program.cs:99–148`

The PR reviewer separates concerns into three sequential phases before involving the LLM:

```
Phase 1: Provider    — fetch normalized PullRequestInfo from the platform
Phase 2: Analyzers   — run deterministic analysis (IAnalyzer pipeline)
Phase 3: Agent       — LLM review driven by SK plugins
```

This is the **pre-processing pipeline** pattern: deterministic, fast analysis runs first and its
findings are handed to the LLM as structured context (via `GetAnalyzerFindings()` plugin function).
The LLM's focus is reserved for nuanced code review, not mechanical detection.

---

## 9. Path Traversal Guards in Plugins

**Where:** `FileSystemPlugin.ResolveSafePath()`, `KnowledgeBaseWriterPlugin.ResolveSafePath()`

Both write/read plugins validate that resolved paths remain within their configured root:

```csharp
private string? ResolveSafePath(string path)
{
    var full = Path.IsPathRooted(path)
        ? Path.GetFullPath(path)
        : Path.GetFullPath(Path.Combine(_root, path));

    return full.StartsWith(_root, StringComparison.OrdinalIgnoreCase)
        ? full
        : null;  // or throw UnauthorizedAccessException
}
```

This is essential when plugins are invoked by an LLM — the AI could attempt to navigate outside
the intended directory (either accidentally or adversarially).

---

## 10. `[SkippableFact]` for Live Integration Tests

**Where:** All `Live*Tests` classes in the test projects

Integration tests that require real Claude credentials use `Xunit.SkippableFact`:

```csharp
[SkippableFact]
public async Task LiveTest_RealApiCall()
{
    Skip.If(
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
        "Skipped: no real credentials available.");

    // ... real API call ...
}
```

This allows the full test suite to pass in CI environments without credentials while still
enabling live integration testing in development. The pattern prevents false failures on
pull requests from contributors without API access.

---

## 11. `NoOpChatService` for Unit Tests

**Where:** `CodebaseExplorer.Tests`, `GherkinGenerator.Tests`, `PullRequestReviewer.Tests`

Each sample's test project defines a `file`-scoped `NoOpChatService`:

```csharp
file sealed class NoOpChatService : IChatCompletionService
{
    // Returns a canned response without making any API calls
}
```

The `file` access modifier (C# 11+) ensures the type is invisible outside its compilation unit,
preventing naming collisions across test assemblies.

---

## 12. Naming Conventions

| Item | Convention | Example |
|------|-----------|---------|
| Classes | PascalCase | `ClaudeCodeSessionProvider` |
| Interfaces | `I` prefix + PascalCase | `IPullRequestProvider`, `IAnalyzer` |
| Records | PascalCase | `ClaudeCodeOAuthCredentials`, `PullRequestInfo` |
| Constants | PascalCase | `ClaudeModels.Default`, `ClaudeCodeSessionOptions.SectionName` |
| Private fields | `_camelCase` | `_options`, `_cacheLock`, `_cached` |
| Async methods | `...Async` suffix | `GetTokenAsync`, `GetCredentialsAsync` |
| SK plugin methods | `snake_case` via `[KernelFunction("name")]` | `get_directory_tree`, `add_review_comment` |
| Config section | `"ClaudeSession"` | `ClaudeCodeSessionOptions.SectionName` |

---

## 13. `sealed` by Default

All concrete classes in the library and samples are marked `sealed` unless there is a specific
reason for inheritance. This:
- Prevents unintended subclassing
- Enables JIT devirtualization optimizations
- Makes the API surface explicit about extension points

The only abstract constructs in the codebase are `IAnalyzer` and `IPullRequestProvider`, which
are intentionally designed as extension points.

---

## 14. XML Documentation on All Public Members

Every public type and member in the core library has `///` XML doc comments. These feed the
DocFX documentation site. The `[Description]` attribute on every `[KernelFunction]` parameter
doubles as both developer documentation and the tool description seen by the LLM during
function calling.

---

## 15. Global Build Quality Settings (`Directory.Build.props`)

```xml
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<AnalysisLevel>latest-all</AnalysisLevel>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<TreatWarningsAsErrors Condition="'$(CI)' == 'true'">true</TreatWarningsAsErrors>
<Deterministic>true</Deterministic>
```

- **Nullable reference types** are enabled project-wide — all `null` flows are explicit.
- **Warnings-as-errors** in CI ensures no quality regressions slip through.
- **Meziantou.Analyzer** enforces additional opinionated rules (e.g., `ConfigureAwait`, proper
  disposal patterns).
- **Deterministic builds** + **embedded debug info** + **SourceLink** ensure reproducible,
  debuggable release artifacts.

---

## Potential Concerns / Tech Debt

| Concern | Location | Notes |
|---------|----------|-------|
| No token refresh | `ClaudeCodeSessionProvider` | Expired tokens require the user to run `claude login` manually. The `RefreshToken` field is deserialized from the credentials file but is never used to auto-refresh. This is a known limitation. |
| `netstandard2.0` `UseClaudeCodeChatCompletion` absent | `KernelBuilderExtensions.cs` | Users on netstandard2.0 hosts must use the longer `AddClaudeCodeAuthentication` + manual client construction path. Worth documenting prominently. |
| Hard-coded `ClaudeCodeVersion = "2.1.45"` | `ClaudeCodeSessionHttpHandler.cs:30` | The `user-agent` header mimics `claude-cli/2.1.45`. If Anthropic changes the required CLI version in their OAuth validation, this string will need updating. |
| Single analyzer in PR reviewer | `PullRequestReviewer/Program.cs:139` | Only `DiffAnalyzer` is wired up. The `IAnalyzer` interface is ready for additional analyzers (e.g., linters, SAST tools) but none are included yet. |
| `find_pattern_in_files` capped at 50 matches | `CodeAnalysisPlugin.cs:232` | Large codebases may have patterns appearing in many files; the 50-match cap could cause the LLM to miss results. Consider making the cap configurable. |
| No retry/backoff on `GetTokenAsync` | `ClaudeCodeSessionProvider.cs` | Transient file-system errors (e.g., NTFS locks) will surface immediately as exceptions rather than being retried. |
