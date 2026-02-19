using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions"/>.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddClaudeCodeAuthentication_WithDelegate_RegistersProvider()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(o => o.ApiKey = "sk-ant-api-test");

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ClaudeCodeSessionProvider>());
    }

    [Fact]
    public void AddClaudeCodeAuthentication_NoArgs_RegistersProvider()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication();

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ClaudeCodeSessionProvider>());
    }

    [Fact]
    public void AddClaudeCodeAuthentication_WithDelegate_BindsOptions()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(o =>
            {
                o.ApiKey = "sk-ant-api-bound";
                o.CredentialsPath = "/custom/path";
            });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ClaudeCodeSessionOptions>>().Value;

        Assert.Equal("sk-ant-api-bound", options.ApiKey);
        Assert.Equal("/custom/path", options.CredentialsPath);
    }

    [Fact]
    public void AddClaudeCodeAuthentication_ProviderIsSingleton()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddClaudeCodeAuthentication(o => o.ApiKey = "sk-ant-api-test");

        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<ClaudeCodeSessionProvider>();
        var b = sp.GetRequiredService<ClaudeCodeSessionProvider>();

        Assert.Same(a, b);
    }
}
