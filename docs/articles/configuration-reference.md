# Configuration Reference

## `ClaudeCodeSessionOptions` properties

| Property | Type | Default | Description |
|---|---|---|---|
| `ApiKey` | `string?` | `null` | Explicit Anthropic API key (`sk-ant-api*`). Highest priority — overrides all other sources. |
| `OAuthToken` | `string?` | `null` | Explicit OAuth token (`sk-ant-oat*`). Overrides environment variables and credentials file. |
| `CredentialsPath` | `string?` | `null` | Custom path to `.credentials.json`. When `null`, defaults to `~/.claude/.credentials.json`. |

The configuration section name is `"ClaudeSession"` (`ClaudeCodeSessionOptions.SectionName`).

---

## JSON configuration (`appsettings.json`)

```json
{
  "ClaudeSession": {
    "ApiKey": "",
    "OAuthToken": "",
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
| `CLAUDE_CODE_OAUTH_TOKEN` | `OAuthToken` | OAuth token injected from outside the credentials file. |

`ANTHROPIC_API_KEY` takes priority over `CLAUDE_CODE_OAUTH_TOKEN` when both are set.

---

## Full credential resolution order (reminder)

```
1. ClaudeCodeSessionOptions.ApiKey
2. ClaudeCodeSessionOptions.OAuthToken
3. ANTHROPIC_API_KEY  (environment)
4. CLAUDE_CODE_OAUTH_TOKEN  (environment)
5. ~/.claude/.credentials.json  (Claude Code local session)
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
  file (`CredentialsPath` pointing to a mounted secret file)
- **ASP.NET Core user-secrets** (development only) — `dotnet user-secrets set "ClaudeSession:ApiKey" "sk-ant-api..."`

---

## CI/CD example

```yaml
# GitHub Actions
env:
  ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
```

No code changes required — the package reads `ANTHROPIC_API_KEY` automatically.
