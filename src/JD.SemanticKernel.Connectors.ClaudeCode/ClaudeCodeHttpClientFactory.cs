using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// Creates pre-configured <see cref="HttpClient"/> instances that inject Claude Code
/// authentication headers on every request.
///
/// <para>
/// Use this factory when you want to bring your own Anthropic SDK integration but still
/// benefit from automatic Claude Code session or api-key resolution.  For example:
/// </para>
/// <code>
/// var httpClient = ClaudeCodeHttpClientFactory.Create();
/// var client = new AnthropicClient("placeholder", httpClient);
/// </code>
/// <para>
/// <strong>Important:</strong> The returned <see cref="HttpClient"/> owns the
/// underlying <see cref="ClaudeCodeSessionHttpHandler"/> and
/// <see cref="ClaudeCodeSessionProvider"/>. Dispose the <see cref="HttpClient"/>
/// when you are done to release all unmanaged resources.
/// </para>
/// </summary>
public static class ClaudeCodeHttpClientFactory
{
    /// <summary>
    /// Creates an <see cref="HttpClient"/> wired with <see cref="ClaudeCodeSessionHttpHandler"/>
    /// using auto-resolved credentials (credentials file → environment variables).
    /// </summary>
    public static HttpClient Create() => Create(configure: null);

    /// <summary>
    /// Creates an <see cref="HttpClient"/> authenticated with the supplied <paramref name="apiKey"/>.
    /// </summary>
    /// <param name="apiKey">
    /// An Anthropic API key (<c>sk-ant-api*</c>) or OAuth token (<c>sk-ant-oat*</c>).
    /// </param>
    public static HttpClient Create(string apiKey) =>
        Create(o => o.ApiKey = apiKey);

    /// <summary>
    /// Creates an <see cref="HttpClient"/> with options configured by <paramref name="configure"/>.
    /// </summary>
    /// <param name="configure">
    /// Optional delegate to customise <see cref="ClaudeCodeSessionOptions"/> before the
    /// <see cref="ClaudeCodeSessionProvider"/> is constructed.
    /// </param>
    public static HttpClient Create(Action<ClaudeCodeSessionOptions>? configure)
    {
        var options = new ClaudeCodeSessionOptions();
        configure?.Invoke(options);

        var provider = new ClaudeCodeSessionProvider(
            Options.Create(options),
            NullLogger<ClaudeCodeSessionProvider>.Instance);

        return new HttpClient(new ClaudeCodeSessionHttpHandler(provider));
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> backed by an existing <paramref name="provider"/>.
    /// Use this overload when the provider is already registered in a DI container.
    /// </summary>
    public static HttpClient Create(ClaudeCodeSessionProvider provider) =>
        new HttpClient(new ClaudeCodeSessionHttpHandler(
            provider ?? throw new ArgumentNullException(nameof(provider))));
}
