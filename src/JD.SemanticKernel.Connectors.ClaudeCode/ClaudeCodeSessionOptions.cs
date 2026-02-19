namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// Configuration options for Claude Code session authentication.
/// Bind from configuration section <c>"ClaudeSession"</c> or configure via
/// <see cref="ServiceCollectionExtensions.AddClaudeCodeAuthentication(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{ClaudeCodeSessionOptions})"/>.
/// </summary>
public sealed class ClaudeCodeSessionOptions
{
    /// <summary>The default configuration section name.</summary>
    public const string SectionName = "ClaudeSession";

    /// <summary>
    /// Explicit path to <c>.credentials.json</c>.
    /// When <see langword="null"/>, defaults to <c>~/.claude/.credentials.json</c>.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// Override: use an explicit Anthropic API key (<c>sk-ant-api*</c>) instead of session extraction.
    /// Takes priority over all other credential sources.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Override: use an explicit OAuth token (<c>sk-ant-oat*</c>) instead of reading from file.
    /// Useful for CI/CD environments where the token is injected as a secret.
    /// </summary>
    public string? OAuthToken { get; set; }

    /// <summary>
    /// When <see langword="true"/>, disables SSL/TLS certificate validation on outgoing HTTP
    /// requests.  This is intended <b>only</b> for enterprise environments behind TLS-intercepting
    /// proxies or self-signed certificates.
    /// <para>
    /// <b>Warning:</b> enabling this option exposes all traffic to potential
    /// man-in-the-middle attacks.  Do not enable in production.
    /// </para>
    /// </summary>
    public bool DangerouslyDisableSslValidation { get; set; }
}
