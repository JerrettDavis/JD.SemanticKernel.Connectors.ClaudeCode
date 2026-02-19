#if !NETSTANDARD2_0

using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// <see cref="IKernelBuilder"/> extensions for wiring Claude Code authentication into
/// Semantic Kernel using the <c>Anthropic.SDK</c> + <c>Microsoft.Extensions.AI</c> path.
/// </summary>
public static class KernelBuilderExtensions
{
    /// <summary>
    /// Registers an Anthropic chat completion service backed by Claude Code session authentication.
    /// Credentials are resolved automatically from <c>~/.claude/.credentials.json</c>,
    /// environment variables, or the options delegate — in that priority order.
    /// </summary>
    /// <param name="builder">The kernel builder to configure.</param>
    /// <param name="modelId">
    /// The Claude model to target. Defaults to <see cref="ClaudeModels.Default"/>
    /// (<c>claude-sonnet-4-6</c>). See <see cref="ClaudeModels"/> for well-known identifiers.
    /// </param>
    /// <param name="apiKey">
    /// Optional explicit API key or OAuth token override.  When supplied, credential file
    /// and environment variable lookup is skipped.
    /// </param>
    /// <param name="configure">
    /// Optional delegate for fine-grained control over <see cref="ClaudeCodeSessionOptions"/>,
    /// e.g. setting a custom <see cref="ClaudeCodeSessionOptions.CredentialsPath"/>.
    /// Takes effect after <paramref name="apiKey"/> is applied.
    /// </param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// // Minimal — reads from ~/.claude/.credentials.json
    /// var kernel = Kernel.CreateBuilder()
    ///     .UseClaudeCodeChatCompletion()
    ///     .Build();
    ///
    /// // With explicit API key
    /// var kernel = Kernel.CreateBuilder()
    ///     .UseClaudeCodeChatCompletion(apiKey: "sk-ant-api...")
    ///     .Build();
    ///
    /// // Custom model
    /// var kernel = Kernel.CreateBuilder()
    ///     .UseClaudeCodeChatCompletion("claude-opus-4-6")
    ///     .Build();
    /// </code>
    /// </example>
    public static IKernelBuilder UseClaudeCodeChatCompletion(
        this IKernelBuilder builder,
        string modelId = ClaudeModels.Default,
        string? apiKey = null,
        Action<ClaudeCodeSessionOptions>? configure = null)
    {
        var options = new ClaudeCodeSessionOptions();
        if (apiKey is not null) options.ApiKey = apiKey;
        configure?.Invoke(options);

        var provider = new ClaudeCodeSessionProvider(
            Options.Create(options),
            NullLogger<ClaudeCodeSessionProvider>.Instance);

        // Pass a placeholder key — ClaudeCodeSessionHttpHandler replaces it with the
        // correct auth header (Bearer for OAuth tokens, x-api-key for standard keys).
        var httpClient = new HttpClient(new ClaudeCodeSessionHttpHandler(provider));
        var anthropicClient = new AnthropicClient("placeholder-cleared-by-handler", httpClient);

        var chatClient = new ChatClientBuilder(anthropicClient.Messages)
            .ConfigureOptions(o => o.ModelId ??= modelId)
            .UseFunctionInvocation()
            .Build();

        builder.Services.AddSingleton<IChatCompletionService>(
            chatClient.AsChatCompletionService());

        return builder;
    }
}

#endif
