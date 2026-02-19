using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions.AddClaudeCodeAuthentication(IServiceCollection, IConfiguration)"/>
/// — the IConfiguration binding overload.
/// </summary>
public sealed class ConfigurationBindingTests
{
    [Fact]
    public void AddClaudeCodeAuthentication_BindsApiKeyFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
(StringComparer.Ordinal)
            {
                ["ClaudeSession:ApiKey"] = "sk-ant-api-from-config"
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(config);

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ClaudeCodeSessionOptions>>().Value;

        Assert.Equal("sk-ant-api-from-config", options.ApiKey);
    }

    [Fact]
    public void AddClaudeCodeAuthentication_BindsOAuthTokenFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
(StringComparer.Ordinal)
            {
                ["ClaudeSession:OAuthToken"] = "sk-ant-oat-from-config"
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(config);

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ClaudeCodeSessionOptions>>().Value;

        Assert.Equal("sk-ant-oat-from-config", options.OAuthToken);
    }

    [Fact]
    public void AddClaudeCodeAuthentication_BindsCredentialsPathFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
(StringComparer.Ordinal)
            {
                ["ClaudeSession:CredentialsPath"] = "/custom/credentials.json"
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(config);

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ClaudeCodeSessionOptions>>().Value;

        Assert.Equal("/custom/credentials.json", options.CredentialsPath);
    }

    [Fact]
    public void AddClaudeCodeAuthentication_BindsAllPropertiesFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
(StringComparer.Ordinal)
            {
                ["ClaudeSession:ApiKey"] = "sk-ant-api-all",
                ["ClaudeSession:OAuthToken"] = "sk-ant-oat-all",
                ["ClaudeSession:CredentialsPath"] = "/all/path",
                ["ClaudeSession:DangerouslyDisableSslValidation"] = "true"
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(config);

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ClaudeCodeSessionOptions>>().Value;

        Assert.Equal("sk-ant-api-all", options.ApiKey);
        Assert.Equal("sk-ant-oat-all", options.OAuthToken);
        Assert.Equal("/all/path", options.CredentialsPath);
        Assert.True(options.DangerouslyDisableSslValidation);
    }

    [Fact]
    public void AddClaudeCodeAuthentication_EmptySection_LeavesDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal))
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(config);

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ClaudeCodeSessionOptions>>().Value;

        Assert.Null(options.ApiKey);
        Assert.Null(options.OAuthToken);
        Assert.Null(options.CredentialsPath);
        Assert.False(options.DangerouslyDisableSslValidation);
    }

    [Fact]
    public void AddClaudeCodeAuthentication_WithConfiguration_RegistersProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
(StringComparer.Ordinal)
            {
                ["ClaudeSession:ApiKey"] = "sk-ant-api-test"
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(config);

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ClaudeCodeSessionProvider>());
    }
}
