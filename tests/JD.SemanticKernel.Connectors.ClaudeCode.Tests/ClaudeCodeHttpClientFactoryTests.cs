namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ClaudeCodeHttpClientFactory"/>.
/// </summary>
public sealed class ClaudeCodeHttpClientFactoryTests
{
    [Fact]
    public void Create_WithApiKey_ReturnsConfiguredHttpClient()
    {
        using var client = ClaudeCodeHttpClientFactory.Create("sk-ant-api-test");
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_WithConfigure_ReturnsConfiguredHttpClient()
    {
        using var client = ClaudeCodeHttpClientFactory.Create(o => o.ApiKey = "sk-ant-api-test");
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_NoArgs_ReturnsHttpClient()
    {
        using var client = ClaudeCodeHttpClientFactory.Create();
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_WithProvider_ReturnsHttpClient()
    {
        var provider = SessionProviderFactory.WithApiKey("sk-ant-api-test");
        using var client = ClaudeCodeHttpClientFactory.Create(provider);
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_ReturnsDifferentInstancesEachCall()
    {
        using var a = ClaudeCodeHttpClientFactory.Create("sk-ant-api-a");
        using var b = ClaudeCodeHttpClientFactory.Create("sk-ant-api-b");
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Create_WithNullProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ClaudeCodeHttpClientFactory.Create((ClaudeCodeSessionProvider)null!));
    }

    [Fact]
    public void Create_WithInsecureSsl_ReturnsHttpClient()
    {
        using var client = ClaudeCodeHttpClientFactory.Create(
            o =>
            {
                o.ApiKey = "sk-ant-api-test";
                o.DangerouslyDisableSslValidation = true;
            });
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_WithProviderAndInsecureSsl_ReturnsHttpClient()
    {
        var provider = SessionProviderFactory.WithApiKey("sk-ant-api-test");
        using var client = ClaudeCodeHttpClientFactory.Create(provider, dangerouslyDisableSslValidation: true);
        Assert.NotNull(client);
    }
}
