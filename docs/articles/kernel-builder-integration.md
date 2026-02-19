# Kernel Builder Integration

`UseClaudeCodeChatCompletion()` is the primary extension point. It registers an
`IChatCompletionService` backed by Claude Code authentication into the Semantic Kernel service
collection.

> **Availability:** `net8.0` and `net10.0` only. `netstandard2.0` targets can use
> [`AddClaudeCodeAuthentication`](service-collection-integration.md) and build the kernel
> manually.

---

## Overloads

### Auto-resolved session (no arguments)

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion()
    .Build();
```

Reads credentials via the full [resolution chain](credential-resolution.md). Defaults to model
`claude-sonnet-4-6`.

---

### Custom model

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion("claude-opus-4-6")
    .Build();
```

The `modelId` parameter is the first positional argument.

---

### Explicit API key override

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(apiKey: "sk-ant-api...")
    .Build();
```

Skips credential file and environment variable lookup entirely.

---

### Explicit OAuth token override

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(apiKey: "sk-ant-oat...")
    .Build();
```

The handler detects the `sk-ant-oat` prefix and switches to Bearer + CLI header authentication
automatically.

---

### Custom options via delegate

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(configure: o =>
    {
        o.CredentialsPath = "/run/secrets/claude-credentials.json";
    })
    .Build();
```

The `configure` delegate receives a `ClaudeCodeSessionOptions` instance. It runs after `apiKey`
is applied, so you can combine both:

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion(
        modelId:   "claude-opus-4-6",
        apiKey:    Environment.GetEnvironmentVariable("MY_CLAUDE_KEY"),
        configure: o => o.CredentialsPath = "/custom/path")
    .Build();
```

---

## What the extension registers

Internally, `UseClaudeCodeChatCompletion` performs the following steps:

1. Builds a `ClaudeCodeSessionOptions` from the supplied arguments.
2. Creates a `ClaudeCodeSessionProvider` (not registered in DI — it is owned by the handler).
3. Creates an `HttpClient` with `ClaudeCodeSessionHttpHandler` as the message handler.
4. Constructs an `AnthropicClient` with a placeholder key (the handler strips and replaces it
   at request time).
5. Wraps the client with `ChatClientBuilder` (enabling automatic function invocation).
6. Calls `.AsChatCompletionService()` to bridge `Microsoft.Extensions.AI.IChatClient` →
   SK's `IChatCompletionService`.
7. Registers the result as a singleton `IChatCompletionService` in `builder.Services`.

---

## Using the registered service

Once the kernel is built, retrieve the service via SK's standard APIs:

```csharp
var chat = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();
history.AddUserMessage("Hello Claude!");
var response = await chat.GetChatMessageContentsAsync(history);
```

Or use `InvokePromptAsync` for simple one-shot calls:

```csharp
var result = await kernel.InvokePromptAsync("Summarise the Liskov Substitution Principle.");
```

---

## Chaining with other builder calls

`UseClaudeCodeChatCompletion` returns `IKernelBuilder` so it chains naturally:

```csharp
var kernel = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion("claude-sonnet-4-6", apiKey: myKey)
    .Plugins.AddFromType<MyPlugin>()
    ... // other builder calls
    .Build();
```

Wait — `Plugins` is not an extension method that returns `IKernelBuilder`. Use separate
statements for plugin registration:

```csharp
var builder = Kernel.CreateBuilder()
    .UseClaudeCodeChatCompletion("claude-sonnet-4-6", apiKey: myKey);

builder.Plugins.AddFromType<MyPlugin>();

var kernel = builder.Build();
```
