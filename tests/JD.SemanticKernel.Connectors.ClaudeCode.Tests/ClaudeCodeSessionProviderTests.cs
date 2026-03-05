using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ClaudeCodeSessionProvider"/> credential resolution priority.
/// </summary>
[Collection("EnvironmentVariables")]
public sealed class ClaudeCodeSessionProviderTests : IDisposable
{
    // Save and restore env vars so tests don't bleed into each other.
    private readonly string? _savedApiKey;
    private readonly string? _savedOAuthToken;

    public ClaudeCodeSessionProviderTests()
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
        options.EnableOAuthTokenSupport = true;
        configure?.Invoke(options);
        var provider = new ClaudeCodeSessionProvider(
            Options.Create(options),
            NullLogger<ClaudeCodeSessionProvider>.Instance);
        provider.InteractiveSessionDetector = () => true;
        return provider;
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsExplicitApiKey_WhenSet()
    {
        var provider = Build(o => o.ApiKey = "sk-ant-api-test-key");
        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-api-test-key", token);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsOAuthToken_WhenApiKeyNotSet()
    {
        var provider = Build(o => o.OAuthToken = "sk-ant-oat-test-token");
        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-oat-test-token", token);
    }

    [Fact]
    public async Task GetTokenAsync_PrefersApiKeyOverOAuthToken()
    {
        var provider = Build(o =>
        {
            o.ApiKey = "sk-ant-api-wins";
            o.OAuthToken = "sk-ant-oat-loses";
        });
        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-api-wins", token);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsAnthropicApiKeyEnvVar()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-api-from-env");
        var provider = Build();
        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-api-from-env", token);
    }

    [Fact]
    public async Task GetTokenAsync_IgnoresClaudeCodeOAuthTokenEnvVar_WhenApiKeyEnvVarAbsent()
    {
        Environment.SetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN", "sk-ant-oat-from-env");
        var provider = Build(o => o.EnableOAuthTokenSupport = false);
        await Assert.ThrowsAsync<ClaudeCodeSessionException>(() => provider.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_PrefersAnthropicApiKeyEnvVar_OverOAuthEnvVar()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-api-wins");
        Environment.SetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN", "sk-ant-oat-loses");
        var provider = Build();
        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-api-wins", token);
    }

    [Fact]
    public async Task GetTokenAsync_OAuthDisabledByDefault_ThrowsForExplicitOAuthToken()
    {
        var provider = Build(o =>
        {
            o.EnableOAuthTokenSupport = false;
            o.OAuthToken = "sk-ant-oat-test-token";
        });

        await Assert.ThrowsAsync<ClaudeCodeSessionException>(() => provider.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_ThrowsForOAuthToken_WhenSessionIsNonInteractive()
    {
        var provider = Build(o => o.OAuthToken = "sk-ant-oat-test-token");
        provider.InteractiveSessionDetector = () => false;

        await Assert.ThrowsAsync<ClaudeCodeSessionException>(() => provider.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_Throws_WhenNoCredentialsFileAndNoOverrides()
    {
        // Point to a non-existent file and disable the Keychain fallback so neither
        // source can provide a token — the provider must throw.
        var provider = Build(o => o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        provider.KeychainReader = (_, _) => Task.FromResult<string?>(null);
        await Assert.ThrowsAsync<ClaudeCodeSessionException>(() => provider.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_Throws_WhenCredentialsFileHasExpiredToken()
    {
        // Write a credentials file with a token that expired in the past.
        var expiredMs = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();
        var json = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-expired",
                "refreshToken": "refresh",
                "expiresAt": {{expiredMs}},
                "scopes": []
              }
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            var provider = Build(o => o.CredentialsPath = path);
            await Assert.ThrowsAsync<ClaudeCodeSessionException>(() => provider.GetTokenAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsToken_WhenCredentialsFileHasValidToken()
    {
        var futureMs = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var json = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-valid-token",
                "refreshToken": "refresh",
                "expiresAt": {{futureMs}},
                "scopes": []
              }
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            var provider = Build(o => o.CredentialsPath = path);
            var token = await provider.GetTokenAsync();
            Assert.Equal("sk-ant-oat-valid-token", token);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetCredentialsAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        // Disable the Keychain fallback so neither source provides credentials.
        var provider = Build(o => o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        provider.KeychainReader = (_, _) => Task.FromResult<string?>(null);
        var creds = await provider.GetCredentialsAsync();
        Assert.Null(creds);
    }

    [Fact]
    public async Task GetTokenAsync_ThrowsOnCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = Build(o =>
            o.CredentialsPath = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetTokenAsync(cts.Token));
    }

    // ── macOS Keychain fallback ─────────────────────────────────

    [Fact]
    public async Task GetTokenAsync_ReturnsToken_FromKeychainWrappedFormat_WhenFileAbsent()
    {
        // Simulate macOS Keychain returning the full credentials-file JSON.
        var futureMs = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var keychainJson = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-from-keychain",
                "refreshToken": "refresh",
                "expiresAt": {{futureMs}},
                "scopes": []
              }
            }
            """;

        var provider = Build(o => o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        provider.KeychainReader = (_, _) => Task.FromResult<string?>(keychainJson);

        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-oat-from-keychain", token);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsToken_FromKeychainRawOAuthFormat_WhenFileAbsent()
    {
        // Claude Code may store just the OAuth object without the file wrapper.
        var futureMs = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var rawOAuthJson = $$"""
            {
              "accessToken": "sk-ant-oat-raw-oauth",
              "refreshToken": "refresh",
              "expiresAt": {{futureMs}},
              "scopes": []
            }
            """;

        var provider = Build(o => o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        provider.KeychainReader = (_, _) => Task.FromResult<string?>(rawOAuthJson);

        var token = await provider.GetTokenAsync();
        Assert.Equal("sk-ant-oat-raw-oauth", token);
    }

    [Fact]
    public async Task GetTokenAsync_Throws_WhenFileAbsentAndKeychainReturnsNull()
    {
        var provider = Build(o => o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        provider.KeychainReader = (_, _) => Task.FromResult<string?>(null);

        await Assert.ThrowsAsync<ClaudeCodeSessionException>(() => provider.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_Throws_WhenFileAbsentAndKeychainReturnsInvalidJson()
    {
        var provider = Build(o => o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        provider.KeychainReader = (_, _) => Task.FromResult<string?>("not-valid-json");

        await Assert.ThrowsAsync<ClaudeCodeSessionException>(() => provider.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_Throws_WhenFileAbsentAndKeychainHasExpiredToken()
    {
        var expiredMs = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();
        var keychainJson = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-expired",
                "refreshToken": "refresh",
                "expiresAt": {{expiredMs}},
                "scopes": []
              }
            }
            """;

        var provider = Build(o => o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        provider.KeychainReader = (_, _) => Task.FromResult<string?>(keychainJson);

        await Assert.ThrowsAsync<ClaudeCodeSessionException>(() => provider.GetTokenAsync());
    }

    [Fact]
    public async Task GetTokenAsync_PrefersCredentialsFile_OverKeychain()
    {
        // File has a valid token — Keychain should never be consulted.
        var futureMs = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var fileJson = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-from-file",
                "refreshToken": "refresh",
                "expiresAt": {{futureMs}},
                "scopes": []
              }
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, fileJson);

            var keychainCalled = false;
            var provider = Build(o => o.CredentialsPath = path);
            provider.KeychainReader = (_, _) =>
            {
                keychainCalled = true;
                return Task.FromResult<string?>("sk-ant-oat-from-keychain");
            };

            var token = await provider.GetTokenAsync();
            Assert.Equal("sk-ant-oat-from-file", token);
            Assert.False(keychainCalled, "Keychain should not be consulted when the credentials file is present");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetCredentialsAsync_ConcurrentCalls_ReturnSameResult()
    {
        var futureMs = DateTimeOffset.UtcNow
            .AddDays(1).ToUnixTimeMilliseconds();
        var json = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-concurrent",
                "refreshToken": "refresh",
                "expiresAt": {{futureMs}},
                "scopes": []
              }
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            var provider = Build(o => o.CredentialsPath = path);

            var tasks = Enumerable.Range(0, 10)
                .Select(_ => provider.GetCredentialsAsync());

            var results = await Task.WhenAll(tasks);

            Assert.All(results, r =>
                Assert.Equal("sk-ant-oat-concurrent",
                    r!.AccessToken));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
