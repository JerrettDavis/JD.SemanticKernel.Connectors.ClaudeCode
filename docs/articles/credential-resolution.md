# Credential Resolution

`ClaudeCodeSessionProvider.GetTokenAsync()` walks through four credential sources in priority
order, returning the first non-empty token it finds.

## Resolution order

```
1. ClaudeCodeSessionOptions.ApiKey          (explicit option / configuration)
2. ANTHROPIC_API_KEY                        (environment variable)
3. ClaudeCodeSessionOptions.OAuthToken      (explicit option / configuration)
4. ~/.claude/.credentials.json              (Claude Code local session)
```

The first source that produces a non-whitespace value wins.

---

## Source 1 — explicit API key option

The highest-priority override. Set it programmatically or via
[`appsettings.json`](configuration-reference.md):

```csharp
services.AddClaudeCodeAuthentication(o => o.ApiKey = "sk-ant-api...");
```

---

## Source 2 — `ANTHROPIC_API_KEY` environment variable

The standard Anthropic SDK environment variable. Recommended for service/automation scenarios:

```shell
export ANTHROPIC_API_KEY=sk-ant-api...
```

---

## Source 3 — explicit OAuth option (opt-in)

OAuth support is disabled by default. To use an explicit OAuth token, you must opt in:

```csharp
services.AddClaudeCodeAuthentication(o =>
{
    o.EnableOAuthTokenSupport = true;
    o.OAuthToken = "sk-ant-oat...";
});
```

---

## Source 4 — `~/.claude/.credentials.json` (opt-in)

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

To enable file-based OAuth resolution:

```csharp
services.AddClaudeCodeAuthentication(o =>
{
    o.EnableOAuthTokenSupport = true;
    o.CredentialsPath = "/opt/claude/.credentials.json"; // optional override
});
```

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
    "EnableOAuthTokenSupport": true,
    "CredentialsPath": "/opt/claude/.credentials.json"
  }
}
```

---

## OAuth safety checks

- OAuth support must be explicitly enabled (`EnableOAuthTokenSupport=true`).
- OAuth requests must run in an interactive session.
- Non-interactive/unattended OAuth use is blocked intentionally.

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
