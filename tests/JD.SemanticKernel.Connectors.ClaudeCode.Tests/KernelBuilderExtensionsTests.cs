using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="KernelBuilderExtensions.UseClaudeCodeChatCompletion"/>.
/// </summary>
public sealed class KernelBuilderExtensionsTests
{
    [Fact]
    public void UseClaudeCodeChatCompletion_RegistersIChatCompletionService()
    {
        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion(apiKey: "sk-ant-api-test")
            .Build();

        var service = kernel.Services.GetService<IChatCompletionService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void UseClaudeCodeChatCompletion_WithDefaultModel_RegistersService()
    {
        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion(apiKey: "sk-ant-api-test")
            .Build();

        Assert.NotNull(kernel.Services.GetService<IChatCompletionService>());
    }

    [Fact]
    public void UseClaudeCodeChatCompletion_WithCustomModel_RegistersService()
    {
        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion("claude-opus-4-6", apiKey: "sk-ant-api-test")
            .Build();

        Assert.NotNull(kernel.Services.GetService<IChatCompletionService>());
    }

    [Fact]
    public void UseClaudeCodeChatCompletion_WithConfigure_RegistersService()
    {
        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion(configure: o => o.ApiKey = "sk-ant-api-test")
            .Build();

        Assert.NotNull(kernel.Services.GetService<IChatCompletionService>());
    }

    [Fact]
    public void UseClaudeCodeChatCompletion_WithOAuthToken_RegistersService()
    {
        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion(apiKey: "sk-ant-oat-test-oauth-token")
            .Build();

        Assert.NotNull(kernel.Services.GetService<IChatCompletionService>());
    }

    [Fact]
    public void UseClaudeCodeChatCompletion_ReturnsBuilderForChaining()
    {
        var builder = Kernel.CreateBuilder();
        var returned = builder.UseClaudeCodeChatCompletion(apiKey: "sk-ant-api-test");
        Assert.Same(builder, returned);
    }
}
