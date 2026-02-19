using System.Text.Json;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ClaudeCodeOAuthCredentials"/> and <see cref="ClaudeCodeCredentialsFile"/>.
/// </summary>
public sealed class ClaudeCodeCredentialsTests
{
    // ── ClaudeCodeOAuthCredentials ──────────────────────────────

    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpiresAtInFuture()
    {
        var creds = new ClaudeCodeOAuthCredentials
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()
        };

        Assert.False(creds.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpiresAtWithinClockSkewMargin()
    {
        // Token expires in 10 seconds — within the 30-second safety margin.
        var creds = new ClaudeCodeOAuthCredentials
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds()
        };

        Assert.True(creds.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpiresAtInPast()
    {
        var creds = new ClaudeCodeOAuthCredentials
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds()
        };

        Assert.True(creds.IsExpired);
    }

    [Fact]
    public void ExpiresAtUtc_ConvertsMillisecondsCorrectly()
    {
        // 2025-06-01T00:00:00Z in milliseconds
        var epochMs = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var creds = new ClaudeCodeOAuthCredentials { ExpiresAt = epochMs };

        Assert.Equal(2025, creds.ExpiresAtUtc.Year);
        Assert.Equal(6, creds.ExpiresAtUtc.Month);
        Assert.Equal(1, creds.ExpiresAtUtc.Day);
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpiresAtExactlyNow()
    {
        // Boundary: ExpiresAt set to "now" should be treated as expired
        // (token is not valid if it expires at the current instant).
        var creds = new ClaudeCodeOAuthCredentials
        {
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Allow a tiny race — the assertion fires milliseconds
        // after construction, so the token should definitely be
        // expired (or at best exactly at the boundary).
        Assert.True(creds.IsExpired);
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var creds = new ClaudeCodeOAuthCredentials();

        Assert.Equal(string.Empty, creds.AccessToken);
        Assert.Equal(string.Empty, creds.RefreshToken);
        Assert.Equal(0, creds.ExpiresAt);
        Assert.Empty(creds.Scopes);
        Assert.Null(creds.SubscriptionType);
        Assert.Null(creds.RateLimitTier);
    }

    [Fact]
    public void Equality_WorksByValue()
    {
        var a = new ClaudeCodeOAuthCredentials { AccessToken = "tok", ExpiresAt = 123 };
        var b = new ClaudeCodeOAuthCredentials { AccessToken = "tok", ExpiresAt = 123 };

        Assert.Equal(a, b);
    }

    // ── ClaudeCodeCredentialsFile ──────────────────────────────

    [Fact]
    public void CredentialsFile_DefaultOAuth_IsNull()
    {
        var file = new ClaudeCodeCredentialsFile();
        Assert.Null(file.ClaudeAiOauth);
    }

    // ── JSON round-trip ────────────────────────────────────────

    [Fact]
    public void Deserialization_FullJson_MapsAllFields()
    {
        var json = """
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-abc",
                "refreshToken": "refresh-xyz",
                "expiresAt": 1762000000000,
                "scopes": ["scope1", "scope2"],
                "subscriptionType": "claude_pro",
                "rateLimitTier": "pro"
              }
            }
            """;

        var file = JsonSerializer.Deserialize<ClaudeCodeCredentialsFile>(json)!;

        Assert.NotNull(file.ClaudeAiOauth);
        Assert.Equal("sk-ant-oat-abc", file.ClaudeAiOauth!.AccessToken);
        Assert.Equal("refresh-xyz", file.ClaudeAiOauth.RefreshToken);
        Assert.Equal(1762000000000, file.ClaudeAiOauth.ExpiresAt);
        Assert.Equal(
            ["scope1", "scope2"], file.ClaudeAiOauth.Scopes);
        Assert.Equal("claude_pro", file.ClaudeAiOauth.SubscriptionType);
        Assert.Equal("pro", file.ClaudeAiOauth.RateLimitTier);
    }

    [Fact]
    public void Deserialization_MinimalJson_DefaultsOptionalFields()
    {
        var json = """
            {
              "claudeAiOauth": {
                "accessToken": "tok",
                "expiresAt": 0
              }
            }
            """;

        var file = JsonSerializer.Deserialize<ClaudeCodeCredentialsFile>(json)!;

        Assert.NotNull(file.ClaudeAiOauth);
        Assert.Equal("tok", file.ClaudeAiOauth!.AccessToken);
        Assert.Equal(string.Empty, file.ClaudeAiOauth.RefreshToken);
        Assert.Empty(file.ClaudeAiOauth.Scopes);
        Assert.Null(file.ClaudeAiOauth.SubscriptionType);
    }

    [Fact]
    public void Deserialization_EmptyObject_ReturnsNullOAuth()
    {
        var json = "{}";
        var file = JsonSerializer.Deserialize<ClaudeCodeCredentialsFile>(json)!;

        Assert.Null(file.ClaudeAiOauth);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesData()
    {
        var original = new ClaudeCodeCredentialsFile
        {
            ClaudeAiOauth = new ClaudeCodeOAuthCredentials
            {
                AccessToken = "sk-ant-oat-round-trip",
                RefreshToken = "refresh",
                ExpiresAt = 1762000000000,
                Scopes = new[] { "scope1" },
                SubscriptionType = "claude_pro",
                RateLimitTier = "pro"
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ClaudeCodeCredentialsFile>(json)!;

        Assert.Equal(original.ClaudeAiOauth!.AccessToken, deserialized.ClaudeAiOauth!.AccessToken);
        Assert.Equal(original.ClaudeAiOauth.ExpiresAt, deserialized.ClaudeAiOauth.ExpiresAt);
        Assert.Equal(original.ClaudeAiOauth.SubscriptionType, deserialized.ClaudeAiOauth.SubscriptionType);
    }
}
