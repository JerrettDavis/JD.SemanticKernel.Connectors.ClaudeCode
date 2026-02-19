# Credential Resolution

`ClaudeCodeSessionProvider.GetTokenAsync()` walks through five credential sources in priority
order, returning the first non-empty token it finds.

## Resolution order

```
1. ClaudeCodeSessionOptions.ApiKey          (explicit option / configuration)
2. ClaudeCodeSessionOptions.OAuthToken      (explicit option / configuration)
3. ANTHROPIC_API_KEY                        (environment variable)
4. CLAUDE_CODE_OAUTH_TOKEN                  (environment variable)
5. ~/.claude/.credentials.json              (Claude Code local session)
```

The first source that produces a non-whitespace value wins. Sources 1–4 are returned as-is with
no further validation. Source 5 is validated for expiry before use.

---

## Source 1 & 2 — explicit options

The highest-priority overrides. Set these programmatically or via
[`appsettings.json`](configuration-reference.md):

```csharp
// API key wins over everything else
services.AddClaudeCodeAuthentication(o => o.ApiKey = "sk-ant-api...");

// OAuth token (useful when injecting from a secret manager)
services.AddClaudeCodeAuthentication(o => o.OAuthToken = "sk-ant-oat...");
```

`ApiKey` takes priority over `OAuthToken` if both are set.

---

## Source 3 — `ANTHROPIC_API_KEY` environment variable

The standard Anthropic SDK environment variable. Useful for CI/CD pipelines:

```shell
export ANTHROPIC_API_KEY=sk-ant-api...
```

---

## Source 4 — `CLAUDE_CODE_OAUTH_TOKEN` environment variable

Used when you want to inject an OAuth token via the environment — for example in a Docker
container where you pass the host machine's token in:

```shell
export CLAUDE_CODE_OAUTH_TOKEN=sk-ant-oat...
```

---

## Source 5 — `~/.claude/.credentials.json`

The Claude Code local installation stores OAuth credentials here after `claude login`.
The file structure is:

```json
{
  "claudeAiOauth": {
    "accessToken": "sk-ant-oat...",
    "refreshToken": "...",
    "expiresAt": 1762000000000,
    "scopes": ["..."],
    "subscriptionType": "claude_pro",
    "rateLimitTier": "pro"
  }
}
```

The `expiresAt` field is a Unix timestamp in **milliseconds**. If the token is expired,
`ClaudeCodeSessionException` is thrown with a message directing the user to run `claude login`.

### Custom path

Override the default credentials file location via options:

```csharp
services.AddClaudeCodeAuthentication(o =>
    o.CredentialsPath = "/opt/claude/.credentials.json");
```

Or in `appsettings.json`:

```json
{
  "ClaudeSession": {
    "CredentialsPath": "/opt/claude/.credentials.json"
  }
}
```

---

## Token type detection

Once a token is resolved, `ClaudeCodeSessionHttpHandler` inspects the token string to determine
the correct authentication strategy:

| Token prefix | Auth strategy |
|---|---|
| `sk-ant-oat*` | OAuth — `Authorization: Bearer {token}` + Claude Code CLI headers |
| `sk-ant-api*` | Standard — `x-api-key: {token}` |

This happens automatically — you do not need to know which token type you have.

---

## Caching

OAuth credentials read from the credentials file are cached in memory by
`ClaudeCodeSessionProvider` and re-read only after the cached token expires. Explicit options and
environment variables are not cached (they are re-read on each call, but are typically stable).
