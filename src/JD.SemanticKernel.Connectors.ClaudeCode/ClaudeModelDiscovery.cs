using JD.SemanticKernel.Connectors.Abstractions;

namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// Returns the well-known Claude model catalogue.
/// Because the Anthropic API does not expose a <c>/models</c> endpoint for OAuth sessions,
/// this implementation returns a static list derived from <see cref="ClaudeModels"/>.
/// </summary>
public sealed class ClaudeModelDiscovery : IModelDiscoveryProvider
{
    private static readonly IReadOnlyList<ModelInfo> KnownModels =
    [
        new(ClaudeModels.Opus, "Claude Opus 4.6", "anthropic"),
        new(ClaudeModels.Sonnet, "Claude Sonnet 4.6", "anthropic"),
        new(ClaudeModels.Haiku, "Claude Haiku 4.5", "anthropic"),
    ];

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModelInfo>> DiscoverModelsAsync(CancellationToken ct = default) =>
        Task.FromResult(KnownModels);
}
