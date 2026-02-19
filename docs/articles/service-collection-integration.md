# Service Collection Integration

`AddClaudeCodeAuthentication()` registers `ClaudeCodeSessionProvider` into the DI container.
Use this when you want to:

- Inject `ClaudeCodeSessionProvider` directly into your own services
- Build the SK kernel as part of a larger `IServiceCollection` registration (e.g., ASP.NET Core,
  Worker Service, Aspire)
- Target `netstandard2.0` where `IKernelBuilder` extensions are unavailable

---

## Overloads

### From `IConfiguration` (recommended for hosted apps)

```csharp
services.AddClaudeCodeAuthentication(configuration);
```

Binds from the `"ClaudeSession"` section of your configuration. See
[Configuration Reference](configuration-reference.md) for available keys.

---

### From a delegate

```csharp
services.AddClaudeCodeAuthentication(o =>
{
    o.ApiKey = "sk-ant-api...";
});
```

---

### No arguments (auto-resolve from environment / credentials file)

```csharp
services.AddClaudeCodeAuthentication();
```

---

## Combining with Semantic Kernel in a hosted app

```csharp
// Program.cs — ASP.NET Core or Worker Service
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddClaudeCodeAuthentication(builder.Configuration)
    .AddSingleton(sp =>
    {
        var sessionProvider = sp.GetRequiredService<ClaudeCodeSessionProvider>();
        var httpClient = ClaudeCodeHttpClientFactory.Create(sessionProvider);

        var anthropicClient = new AnthropicClient("placeholder", httpClient);
        var chatClient = new ChatClientBuilder(anthropicClient.Messages)
            .UseFunctionInvocation()
            .Build();

        return Kernel.CreateBuilder()
            .AddFromService<IChatCompletionService>(sp =>
                chatClient.AsChatCompletionService())
            .Build();
    });
```

> **Tip:** For simpler setups — especially console apps — prefer
> [`UseClaudeCodeChatCompletion()`](kernel-builder-integration.md) which does all of this
> in one call.

---

## Injecting `ClaudeCodeSessionProvider` directly

Once registered, the provider is available for injection anywhere in your DI graph:

```csharp
public class MyService
{
    private readonly ClaudeCodeSessionProvider _session;

    public MyService(ClaudeCodeSessionProvider session) => _session = session;

    public async Task<string> GetTokenAsync(CancellationToken ct)
        => await _session.GetTokenAsync(ct);
}
```

This is useful for:

- Displaying subscription/rate-limit tier information to users
  (`await provider.GetCredentialsAsync()`)
- Pre-flight session validation at startup
- Passing the token to a custom HTTP client or third-party SDK

---

## Singleton lifetime

`ClaudeCodeSessionProvider` is registered as a **singleton**. Credentials from the file are
cached in memory after the first read, so repeated calls to `GetTokenAsync` do not hit disk.
The cache is invalidated automatically when the cached token's `expiresAt` passes.
