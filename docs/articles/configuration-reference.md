# Configuration Reference

## `ClaudeCodeSessionOptions` properties

| Property | Type | Default | Description |
|---|---|---|---|
| `ApiKey` | `string?` | `null` | Explicit Anthropic API key (`sk-ant-api*`). Highest priority — overrides all other sources. |
| `OAuthToken` | `string?` | `null` | Explicit OAuth token (`sk-ant-oat*`). Requires `EnableOAuthTokenSupport=true`. |
| `EnableOAuthTokenSupport` | `bool` | `false` | Enables OAuth token paths for local interactive usage. |
| `CredentialsPath` | `string?` | `null` | Custom path to `.credentials.json`. When `null`, defaults to `~/.claude/.credentials.json`. |

The configuration section name is `"ClaudeSession"` (`ClaudeCodeSessionOptions.SectionName`).

---

## JSON configuration (`appsettings.json`)

```json
{
  "ClaudeSession": {
    "ApiKey": "",
    "OAuthToken": "",
    "EnableOAuthTokenSupport": false,
    "CredentialsPath": ""
  }
}
```

Bind via `IConfiguration`:

```csharp
services.AddClaudeCodeAuthentication(configuration);
// reads configuration.GetSection("ClaudeSession")
```

Typical production setup — leave all three empty and let the package resolve credentials from
the environment or credentials file:

```json
{
  "ClaudeSession": {}
}
```

---

## Environment variables

| Variable | Equivalent option | Description |
|---|---|---|
| `ANTHROPIC_API_KEY` | `ApiKey` | Standard Anthropic API key. Checked after explicit options. |

---

## Full credential resolution order (reminder)

```
1. ClaudeCodeSessionOptions.ApiKey
2. ANTHROPIC_API_KEY  (environment)
3. ClaudeCodeSessionOptions.OAuthToken  (requires EnableOAuthTokenSupport)
4. ~/.claude/.credentials.json  (requires EnableOAuthTokenSupport)
```

See [Credential Resolution](credential-resolution.md) for detailed behaviour.

---

## Default model

The `modelId` parameter on `UseClaudeCodeChatCompletion()` defaults to `"claude-sonnet-4-6"`.
It is passed at the call site rather than in `ClaudeCodeSessionOptions`, keeping auth concerns
separate from model selection.

```csharp
// Use default model
builder.UseClaudeCodeChatCompletion();

// Override model at the call site
builder.UseClaudeCodeChatCompletion("claude-opus-4-6");
```

---

## Secrets management

For production deployments, avoid storing tokens in `appsettings.json`. Instead:

- **Azure Key Vault** — use `builder.Configuration.AddAzureKeyVault(...)` and map secrets to
  `ClaudeSession:ApiKey`
- **Docker secrets / Kubernetes secrets** — mount as env vars (`ANTHROPIC_API_KEY`) or as a
  secure secret provider and map to `ClaudeSession:ApiKey`
- **ASP.NET Core user-secrets** (development only) — `dotnet user-secrets set "ClaudeSession:ApiKey" "sk-ant-api..."`

---

## OAuth opt-in example (local interactive use only)

```json
{
  "ClaudeSession": {
    "EnableOAuthTokenSupport": true
  }
}
```

Use API keys for CI/CD and unattended automation.
