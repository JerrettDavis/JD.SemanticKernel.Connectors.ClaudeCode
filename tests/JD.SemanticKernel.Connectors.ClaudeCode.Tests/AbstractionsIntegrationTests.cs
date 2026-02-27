using JD.SemanticKernel.Connectors.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for the shared Abstractions interface implementations
/// (<see cref="ISessionProvider"/>, <see cref="IModelDiscoveryProvider"/>,
/// and <see cref="SessionOptionsBase"/>).
/// </summary>
[Collection("EnvironmentVariables")]
public sealed class AbstractionsIntegrationTests : IDisposable
{
    private readonly string? _savedApiKey;
    private readonly string? _savedOAuthToken;

    public AbstractionsIntegrationTests()
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

    // ── ISessionProvider ───────────────────────────────────────────

    [Fact]
    public void SessionProvider_ImplementsISessionProvider()
    {
        var provider = Build(o => o.ApiKey = "test");
        Assert.IsAssignableFrom<ISessionProvider>(provider);
    }

    [Fact]
    public async Task ISessionProvider_GetCredentials_ReturnsSessionCredentials()
    {
        ISessionProvider provider = Build(o => o.ApiKey = "sk-ant-api-test");
        var creds = await provider.GetCredentialsAsync();

        Assert.NotNull(creds);
        Assert.Equal("sk-ant-api-test", creds.Token);
        Assert.False(creds.IsExpired);
    }

    [Fact]
    public async Task ISessionProvider_GetCredentials_IncludesExpiry_WhenOAuthFile()
    {
        var futureMs = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var json = $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat-valid",
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
            ISessionProvider provider = Build(o => o.CredentialsPath = path);
            var creds = await provider.GetCredentialsAsync();

            Assert.NotNull(creds);
            Assert.Equal("sk-ant-oat-valid", creds.Token);
            Assert.NotNull(creds.ExpiresAt);
            Assert.False(creds.IsExpired);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task IsAuthenticatedAsync_ReturnsTrue_WhenTokenAvailable()
    {
        var provider = Build(o => o.ApiKey = "sk-ant-api-test");
        Assert.True(await provider.IsAuthenticatedAsync());
    }

    [Fact]
    public async Task IsAuthenticatedAsync_ReturnsFalse_WhenNoCredentials()
    {
        var provider = Build(o =>
            o.CredentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        Assert.False(await provider.IsAuthenticatedAsync());
    }

    // ── SessionOptionsBase ─────────────────────────────────────────

    [Fact]
    public void Options_ExtendsSessionOptionsBase()
    {
        var options = new ClaudeCodeSessionOptions();
        Assert.IsAssignableFrom<SessionOptionsBase>(options);
    }

    [Fact]
    public void Options_InheritsDangerouslyDisableSslValidation()
    {
        var options = new ClaudeCodeSessionOptions
        {
            DangerouslyDisableSslValidation = true
        };
        SessionOptionsBase baseOptions = options;
        Assert.True(baseOptions.DangerouslyDisableSslValidation);
    }

    [Fact]
    public void Options_InheritsCustomEndpoint()
    {
        var options = new ClaudeCodeSessionOptions
        {
            CustomEndpoint = "https://custom.api.com"
        };
        SessionOptionsBase baseOptions = options;
        Assert.Equal("https://custom.api.com", baseOptions.CustomEndpoint);
    }

    // ── IModelDiscoveryProvider ────────────────────────────────────

    [Fact]
    public void ModelDiscovery_ImplementsIModelDiscoveryProvider()
    {
        var discovery = new ClaudeModelDiscovery();
        Assert.IsAssignableFrom<IModelDiscoveryProvider>(discovery);
    }

    [Fact]
    public async Task ModelDiscovery_ReturnsKnownModels()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        Assert.NotEmpty(models);
        Assert.Contains(models, m => string.Equals(m.Id, ClaudeModels.Opus, StringComparison.Ordinal));
        Assert.Contains(models, m => string.Equals(m.Id, ClaudeModels.Sonnet, StringComparison.Ordinal));
        Assert.Contains(models, m => string.Equals(m.Id, ClaudeModels.Haiku, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ModelDiscovery_AllModelsHaveAnthropicProvider()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        Assert.All(models, m => Assert.Equal("anthropic", m.Provider));
    }

    [Fact]
    public async Task ModelDiscovery_ModelsHaveNonEmptyNames()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        Assert.All(models, m => Assert.False(string.IsNullOrWhiteSpace(m.Name)));
    }
}
