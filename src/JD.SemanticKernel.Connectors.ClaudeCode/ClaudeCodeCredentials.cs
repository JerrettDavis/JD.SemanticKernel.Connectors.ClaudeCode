using System.Text.Json.Serialization;

namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>Maps the top-level structure of <c>~/.claude/.credentials.json</c>.</summary>
public sealed record ClaudeCodeCredentialsFile
{
    /// <summary>The Claude.ai OAuth credential block, if present.</summary>
    [JsonPropertyName("claudeAiOauth")]
    public ClaudeCodeOAuthCredentials? ClaudeAiOauth { get; init; }
}

/// <summary>OAuth credentials extracted from the Claude Code local installation.</summary>
public sealed record ClaudeCodeOAuthCredentials
{
    /// <summary>The bearer access token used to authenticate API calls.</summary>
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Refresh token for obtaining a new access token after expiry.</summary>
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>Unix epoch milliseconds at which the access token expires.</summary>
    [JsonPropertyName("expiresAt")]
    public long ExpiresAt { get; init; }

    /// <summary>OAuth scopes granted to this token.</summary>
    [JsonPropertyName("scopes")]
    public string[] Scopes { get; init; } = Array.Empty<string>();

    /// <summary>Subscription type (e.g. <c>"claude_pro"</c>, <c>"free"</c>).</summary>
    [JsonPropertyName("subscriptionType")]
    public string? SubscriptionType { get; init; }

    /// <summary>Rate limit tier associated with this account.</summary>
    [JsonPropertyName("rateLimitTier")]
    public string? RateLimitTier { get; init; }

    /// <summary>The expiry time as a <see cref="DateTimeOffset"/>.</summary>
    public DateTimeOffset ExpiresAtUtc => DateTimeOffset.FromUnixTimeMilliseconds(ExpiresAt);

    /// <summary><see langword="true"/> if the token has passed its expiry time.</summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;
}
