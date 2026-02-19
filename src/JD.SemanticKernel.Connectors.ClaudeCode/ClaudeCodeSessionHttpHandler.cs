using System.Net.Http.Headers;

namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects Claude Code session credentials into every
/// outgoing HTTP request destined for the Anthropic API.
///
/// <para>
/// For OAuth tokens (<c>sk-ant-oat*</c>) it mimics the Claude Code CLI request signature so that
/// <c>api.anthropic.com</c> accepts the Bearer token:
/// <list type="bullet">
///   <item><description><c>Authorization: Bearer {token}</c></description></item>
///   <item><description><c>anthropic-beta: claude-code-20250219,oauth-2025-04-20,...</c></description></item>
///   <item><description><c>user-agent: claude-cli/{version} (external, cli)</c></description></item>
///   <item><description><c>x-app: cli</c></description></item>
/// </list>
/// </para>
///
/// <para>For standard API keys (<c>sk-ant-api*</c>) it falls back to the normal <c>x-api-key</c> header.</para>
///
/// <para>Credit: technique discovered from <see href="https://github.com/badlogic/pi-mono">pi-mono</see>.</para>
/// </summary>
public sealed class ClaudeCodeSessionHttpHandler : DelegatingHandler
{
    // Beta features required to authorize an OAuth token against api.anthropic.com.
    private const string OAuthBetaHeader =
        "claude-code-20250219,oauth-2025-04-20,fine-grained-tool-streaming-2025-05-14";

    private const string ClaudeCodeVersion = "2.1.45";

    private readonly ClaudeCodeSessionProvider _provider;

    /// <summary>
    /// Initialises a new handler backed by the given <paramref name="provider"/>.
    /// </summary>
    public ClaudeCodeSessionHttpHandler(ClaudeCodeSessionProvider provider)
        : base(new HttpClientHandler())
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Initialises a handler with an explicit inner handler — intended for unit testing.
    /// </summary>
    internal ClaudeCodeSessionHttpHandler(ClaudeCodeSessionProvider provider, HttpMessageHandler innerHandler)
        : base(innerHandler ?? throw new ArgumentNullException(nameof(innerHandler)))
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                request.RequestUri?.Scheme,
                Uri.UriSchemeHttps, StringComparison.Ordinal)
            && !string.Equals(
                request.RequestUri?.Host,
                "localhost", StringComparison.Ordinal)
            && !string.Equals(
                request.RequestUri?.Host,
                "127.0.0.1", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Only HTTPS requests are allowed when using " +
                "Claude Code authentication.");

        var token = await _provider
            .GetTokenAsync(cancellationToken)
            .ConfigureAwait(false);

        if (token.StartsWith("sk-ant-oat", StringComparison.Ordinal))
        {
            // OAuth path: strip any SDK-injected placeholder key and use Bearer auth.
            request.Headers.Remove("x-api-key");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            SetOrReplace(request, "anthropic-beta", OAuthBetaHeader);
            SetOrReplace(request, "user-agent", $"claude-cli/{ClaudeCodeVersion} (external, cli)");
            SetOrReplace(request, "x-app", "cli");
        }
        else
        {
            // Standard API key path.
            request.Headers.Remove("x-api-key");
            request.Headers.TryAddWithoutValidation("x-api-key", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static void SetOrReplace(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }
}
