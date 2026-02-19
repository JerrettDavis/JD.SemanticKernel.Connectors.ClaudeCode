using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Test helper that builds <see cref="ClaudeCodeSessionProvider"/> instances without DI.
/// </summary>
internal static class SessionProviderFactory
{
    internal static ClaudeCodeSessionProvider WithApiKey(string apiKey) =>
        Build(o => o.ApiKey = apiKey);

    internal static ClaudeCodeSessionProvider WithOAuthToken(string token) =>
        Build(o => o.OAuthToken = token);

    internal static ClaudeCodeSessionProvider Build(Action<ClaudeCodeSessionOptions>? configure = null)
    {
        var options = new ClaudeCodeSessionOptions();
        configure?.Invoke(options);
        return new ClaudeCodeSessionProvider(
            Options.Create(options),
            NullLogger<ClaudeCodeSessionProvider>.Instance);
    }
}
