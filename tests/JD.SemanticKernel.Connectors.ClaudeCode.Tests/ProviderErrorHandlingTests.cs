using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for error handling paths in <see cref="ClaudeCodeSessionProvider"/>.
/// Covers malformed credentials files, missing fields, and edge cases.
/// </summary>
[Collection("EnvironmentVariables")]
public sealed class ProviderErrorHandlingTests : IDisposable
{
    private readonly string? _savedApiKey;
    private readonly string? _savedOAuthToken;

    public ProviderErrorHandlingTests()
    {
        _savedApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _savedOAuthToken = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        Environment.SetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", _savedApiKey);
        Environment.SetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN", _savedOAuthToken);
    }

    private static ClaudeCodeSessionProvider Build(Action<ClaudeCodeSessionOptions>? configure = null)
    {
        var options = new ClaudeCodeSessionOptions();
        configure?.Invoke(options);
        return new ClaudeCodeSessionProvider(
            Options.Create(options),
            NullLogger<ClaudeCodeSessionProvider>.Instance);
    }

    [Fact]
    public async Task GetTokenAsync_MalformedJson_ThrowsJsonException()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{ not valid json!!!");
            var provider = Build(o => o.CredentialsPath = path);

            await Assert.ThrowsAsync<JsonException>(
                () => provider.GetTokenAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetTokenAsync_EmptyJsonObject_ThrowsSessionException()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{}");
            var provider = Build(o => o.CredentialsPath = path);

            await Assert.ThrowsAsync<ClaudeCodeSessionException>(
                () => provider.GetTokenAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetTokenAsync_NullOAuthBlock_ThrowsSessionException()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """{"claudeAiOauth": null}""");
            var provider = Build(o => o.CredentialsPath = path);

            await Assert.ThrowsAsync<ClaudeCodeSessionException>(
                () => provider.GetTokenAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetTokenAsync_EmptyAccessToken_ReturnsEmptyString()
    {
        // Current behavior: empty access token is returned (no validation on token content)
        var futureMs = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var json = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "",
                "expiresAt": {{futureMs}}
              }
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            var provider = Build(o => o.CredentialsPath = path);

            var token = await provider.GetTokenAsync();
            Assert.Equal(string.Empty, token);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetTokenAsync_ExpiredCredentials_ThrowsSessionException()
    {
        var pastMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
        var json = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-expired",
                "expiresAt": {{pastMs}}
              }
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            var provider = Build(o => o.CredentialsPath = path);

            var ex = await Assert.ThrowsAsync<ClaudeCodeSessionException>(
                () => provider.GetTokenAsync());

            Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetTokenAsync_WhitespaceApiKey_SkipsToNextSource()
    {
        var provider = Build(o =>
        {
            o.ApiKey = "   ";
            o.OAuthToken = "sk-ant-oat-fallback";
        });

        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-oat-fallback", token);
    }

    [Fact]
    public async Task GetTokenAsync_WhitespaceOAuthToken_SkipsToNextSource()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-api-env-wins");
        var provider = Build(o => o.OAuthToken = "  ");

        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-api-env-wins", token);
    }

    [Fact]
    public async Task GetCredentialsAsync_MalformedJson_ThrowsJsonException()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "not json");
            var provider = Build(o => o.CredentialsPath = path);

            await Assert.ThrowsAsync<JsonException>(
                () => provider.GetCredentialsAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetCredentialsAsync_EmptyJsonObject_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{}");
            var provider = Build(o => o.CredentialsPath = path);

            var creds = await provider.GetCredentialsAsync();
            Assert.Null(creds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetCredentialsAsync_ValidFile_ReturnsCredentials()
    {
        var futureMs = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var json = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-test",
                "refreshToken": "refresh",
                "expiresAt": {{futureMs}},
                "scopes": ["s1"],
                "subscriptionType": "claude_pro"
              }
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            var provider = Build(o => o.CredentialsPath = path);

            var creds = await provider.GetCredentialsAsync();

            Assert.NotNull(creds);
            Assert.Equal("sk-ant-oat-test", creds!.AccessToken);
            Assert.Equal("claude_pro", creds.SubscriptionType);
            Assert.Single(creds.Scopes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetCredentialsAsync_CachesFreshCredentials()
    {
        var futureMs = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var json = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-cached",
                "expiresAt": {{futureMs}}
              }
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            var provider = Build(o => o.CredentialsPath = path);

            var first = await provider.GetCredentialsAsync();
            var second = await provider.GetCredentialsAsync();

            Assert.Same(first, second);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetTokenAsync_MissingFile_ThrowsWithGuidance()
    {
        var provider = Build(o => o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        var ex = await Assert.ThrowsAsync<ClaudeCodeSessionException>(
            () => provider.GetTokenAsync());

        Assert.Contains("claude", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTokenAsync_PrioritizesApiKeyOverOAuthToken()
    {
        var provider = Build(o =>
        {
            o.ApiKey = "sk-ant-api-priority";
            o.OAuthToken = "sk-ant-oat-secondary";
        });

        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-api-priority", token);
    }

    [Fact]
    public async Task GetTokenAsync_EnvVar_ApiKeyOverOAuth()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-api-env");
        Environment.SetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN", "sk-ant-oat-env");

        var provider = Build();
        var token = await provider.GetTokenAsync();

        Assert.Equal("sk-ant-api-env", token);
    }

    [Fact]
    public async Task GetTokenAsync_EnvVar_OAuthUsedWhenNoApiKey()
    {
        Environment.SetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN", "sk-ant-oat-env-only");

        var provider = Build();
        var token = await provider.GetTokenAsync();

        Assert.Equal("sk-ant-oat-env-only", token);
    }
}
