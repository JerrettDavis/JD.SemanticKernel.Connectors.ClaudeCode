using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// Resolves Claude API credentials from multiple sources in priority order:
/// <list type="number">
///   <item><description><c>ClaudeSession:ApiKey</c> in options/configuration</description></item>
///   <item><description><c>ClaudeSession:OAuthToken</c> in options/configuration</description></item>
///   <item><description><c>ANTHROPIC_API_KEY</c> environment variable</description></item>
///   <item><description><c>CLAUDE_CODE_OAUTH_TOKEN</c> environment variable</description></item>
///   <item><description><c>~/.claude/.credentials.json</c> — Claude Code local session</description></item>
/// </list>
/// </summary>
public sealed class ClaudeCodeSessionProvider : IDisposable
{
    private readonly ClaudeCodeSessionOptions _options;
    private readonly ILogger<ClaudeCodeSessionProvider> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private volatile ClaudeCodeOAuthCredentials? _cached;

    /// <summary>
    /// Initialises the provider with DI-injected options and logger.
    /// Use <see cref="ClaudeCodeHttpClientFactory"/> or the
    /// <c>IKernelBuilder.UseClaudeCodeChatCompletion()</c> extension (net8.0+) for scenarios without a DI container.
    /// </summary>
    public ClaudeCodeSessionProvider(
        IOptions<ClaudeCodeSessionOptions> options,
        ILogger<ClaudeCodeSessionProvider> logger)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns the best available API token for the current session.
    /// </summary>
    /// <exception cref="ClaudeCodeSessionException">
    /// Thrown when no valid token is found or when the session has expired.
    /// </exception>
    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogDebug("Using explicit API key from options");
            return _options.ApiKey!;
        }

        if (!string.IsNullOrWhiteSpace(_options.OAuthToken))
        {
            _logger.LogDebug("Using explicit OAuth token from options");
            return _options.OAuthToken!;
        }

        var envApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            _logger.LogDebug("Using ANTHROPIC_API_KEY environment variable");
            return envApiKey!;
        }

        var envOAuth = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        if (!string.IsNullOrWhiteSpace(envOAuth))
        {
            _logger.LogDebug("Using CLAUDE_CODE_OAUTH_TOKEN environment variable");
            return envOAuth!;
        }

        return await ExtractFromCredentialsFileAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the full OAuth credential details, or <see langword="null"/> when the active
    /// credential source is an API key or environment variable.
    /// </summary>
    public async Task<ClaudeCodeOAuthCredentials?> GetCredentialsAsync(CancellationToken ct = default)
    {
        var snapshot = _cached;
        if (snapshot is not null && !snapshot.IsExpired)
            return snapshot;

        await _cacheLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock.
            if (_cached is not null && !_cached.IsExpired)
                return _cached;

            var file = await ReadCredentialsFileAsync(ct)
                .ConfigureAwait(false);
            _cached = file?.ClaudeAiOauth;
            return _cached;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<string> ExtractFromCredentialsFileAsync(CancellationToken ct)
    {
        var creds = await GetCredentialsAsync(ct).ConfigureAwait(false);

        if (creds is null)
            throw new ClaudeCodeSessionException(
                "No Claude credentials found. " +
                "Install Claude Code from https://claude.ai/download and run 'claude login'.");

        if (creds.IsExpired)
            throw new ClaudeCodeSessionException(
                $"Claude session expired at {creds.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC. " +
                "Run 'claude login' to refresh your session.");

        _logger.LogInformation(
            "Extracted Claude session token (expires {ExpiresAt}, subscription: {Sub}, tier: {Tier})",
            creds.ExpiresAtUtc,
            creds.SubscriptionType ?? "unknown",
            creds.RateLimitTier ?? "unknown");

        return creds.AccessToken;
    }

    private async Task<ClaudeCodeCredentialsFile?> ReadCredentialsFileAsync(CancellationToken ct)
    {
        var path = ResolveCredentialsPath();

        _logger.LogDebug("Reading credentials from {Path}", path);

        try
        {
#if NETSTANDARD2_0
            var json = await Task
                .Run(() => File.ReadAllText(path), ct)
                .ConfigureAwait(false);
            return JsonSerializer
                .Deserialize<ClaudeCodeCredentialsFile>(json);
#else
            await using var stream = File.OpenRead(path);
            return await JsonSerializer
                .DeserializeAsync<ClaudeCodeCredentialsFile>(
                    stream, cancellationToken: ct)
                .ConfigureAwait(false);
#endif
        }
        catch (FileNotFoundException)
        {
            _logger.LogDebug(
                "Credentials file not found at {Path}", path);
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            _logger.LogDebug(
                "Credentials directory not found for {Path}", path);
            return null;
        }
    }

    private string ResolveCredentialsPath() =>
        !string.IsNullOrWhiteSpace(_options.CredentialsPath)
            ? _options.CredentialsPath!
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", ".credentials.json");

    /// <inheritdoc/>
    public void Dispose() => _cacheLock.Dispose();
}
