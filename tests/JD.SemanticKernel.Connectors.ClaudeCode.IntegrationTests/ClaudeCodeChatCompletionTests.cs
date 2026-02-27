using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.SemanticKernel.Connectors.ClaudeCode.IntegrationTests;

/// <summary>
/// Integration tests for Claude Code chat completion.
/// Requires <c>CLAUDE_INTEGRATION_TESTS=true</c> and valid local credentials.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClaudeCodeChatCompletionTests
{
    private static bool CanRun =>
        string.Equals(
            Environment.GetEnvironmentVariable("CLAUDE_INTEGRATION_TESTS"),
            "true", StringComparison.OrdinalIgnoreCase) &&
        HasCredentials;

    private static bool HasCredentials =>
        File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", ".credentials.json")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    [SkippableFact]
    public async Task SimpleChatCompletion_ReturnsResponse()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true with valid credentials");

        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion()
            .Build();

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage("Reply with exactly: INTEGRATION_TEST_OK");

        var response = await chat.GetChatMessageContentAsync(history);

        Assert.NotNull(response.Content);
        Assert.NotEmpty(response.Content!);
    }

    [SkippableFact]
    public async Task ChatCompletion_WithSonnetModel_ReturnsResponse()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true with valid credentials");

        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion(ClaudeModels.Sonnet)
            .Build();

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage("Reply with exactly: MODEL_TEST_OK");

        var response = await chat.GetChatMessageContentAsync(history);

        Assert.NotNull(response.Content);
        Assert.NotEmpty(response.Content!);
    }

    [SkippableFact]
    public async Task ChatCompletion_WithHaikuModel_ReturnsResponse()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true with valid credentials");

        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion(ClaudeModels.Haiku)
            .Build();

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage("Reply with exactly: HAIKU_TEST_OK");

        var response = await chat.GetChatMessageContentAsync(history);

        Assert.NotNull(response.Content);
        Assert.NotEmpty(response.Content!);
    }

    [SkippableFact]
    public async Task ChatCompletion_WithSystemMessage_ReturnsResponse()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true with valid credentials");

        var kernel = Kernel.CreateBuilder()
            .UseClaudeCodeChatCompletion()
            .Build();

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage("You are a helpful assistant. Always respond with valid JSON.");
        history.AddUserMessage("Return a JSON object with a single key 'status' set to 'ok'.");

        var response = await chat.GetChatMessageContentAsync(history);

        Assert.NotNull(response.Content);
        Assert.Contains("ok", response.Content!, StringComparison.OrdinalIgnoreCase);
    }
}
