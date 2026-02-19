# HTTP Client Factory

`ClaudeCodeHttpClientFactory` creates `HttpClient` instances pre-configured with
`ClaudeCodeSessionHttpHandler`. Use this when you want to bring your own Anthropic SDK
integration but still benefit from automatic Claude Code credential resolution.

> **Availability:** All TFMs including `netstandard2.0`.

---

## Why use the factory directly?

`UseClaudeCodeChatCompletion()` covers the common Semantic Kernel path. Use the factory when:

- You are using the Anthropic SDK directly (not via SK)
- You need to pass an authenticated `HttpClient` to another library or service
- You are on `netstandard2.0` and the SK extensions are unavailable
- You want a single authenticated client shared across multiple SDK instances

---

## Overloads

### Auto-resolve from default credential chain

```csharp
var httpClient = ClaudeCodeHttpClientFactory.Create();
```

### Explicit API key

```csharp
var httpClient = ClaudeCodeHttpClientFactory.Create("sk-ant-api...");
```

### Options delegate

```csharp
var httpClient = ClaudeCodeHttpClientFactory.Create(o =>
{
    o.CredentialsPath = "/run/secrets/claude-credentials.json";
});
```

### From a DI-resolved provider

When `ClaudeCodeSessionProvider` is already in your DI container (via
[`AddClaudeCodeAuthentication`](service-collection-integration.md)), pass it directly
to avoid creating a second provider instance:

```csharp
var httpClient = ClaudeCodeHttpClientFactory.Create(provider);
```

---

## Using the client with Anthropic.SDK directly

```csharp
using Anthropic.SDK;
using JD.SemanticKernel.Connectors.ClaudeCode;

var httpClient = ClaudeCodeHttpClientFactory.Create();

// "placeholder" is stripped and replaced by ClaudeCodeSessionHttpHandler at request time
var client = new AnthropicClient("placeholder", httpClient);

var response = await client.Messages.CreateAsync(new MessageParameters
{
    Model = "claude-sonnet-4-6",
    MaxTokens = 1024,
    Messages = [new Message(RoleType.User, "Hello!")]
});
```

---

## Using the client with Semantic Kernel manually

```csharp
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using JD.SemanticKernel.Connectors.ClaudeCode;

var httpClient = ClaudeCodeHttpClientFactory.Create();
var anthropicClient = new AnthropicClient("placeholder", httpClient);

var chatClient = new ChatClientBuilder(anthropicClient.Messages)
    .UseFunctionInvocation()
    .Build();

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddSingleton<IChatCompletionService>(
    chatClient.AsChatCompletionService());

var kernel = kernelBuilder.Build();
```

This is equivalent to what `UseClaudeCodeChatCompletion()` does internally — useful when you
need fine-grained control over the `ChatClientBuilder` pipeline.

---

## Lifecycle and disposal

Each call to `ClaudeCodeHttpClientFactory.Create()` returns a **new** `HttpClient` instance.
Callers are responsible for disposal. For long-lived applications, prefer:

- Registering via DI and resolving a single shared instance
- Using `IHttpClientFactory` if your application already uses it for pooling

Do **not** dispose the `HttpClient` if it is shared across multiple `AnthropicClient` instances.
