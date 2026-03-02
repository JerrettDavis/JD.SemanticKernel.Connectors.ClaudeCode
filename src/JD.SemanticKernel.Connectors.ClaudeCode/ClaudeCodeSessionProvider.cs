using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using JD.SemanticKernel.Connectors.Abstractions;
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
///   <item><description><c>~/.claude/.credentials.json</c> — Claude Code local session (Linux/Windows)</description></item>
///   <item><description>macOS system Keychain — Claude Code v2.1.63+ stores credentials there instead of a file on macOS</description></item>
/// </list>
/// </summary>
public sealed class ClaudeCodeSessionProvider : ISessionProvider, IDisposable
{
    // macOS Keychain service name used by Claude Code to store OAuth credentials.
    private const string MacOsKeychainService = "Claude Code-credentials";

    private readonly ClaudeCodeSessionOptions _options;
    private readonly ILogger<ClaudeCodeSessionProvider> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private volatile ClaudeCodeOAuthCredentials? _cached;

    /// <summary>
    /// Pluggable macOS Keychain reader. Defaults to invoking the <c>security</c> CLI tool.
    /// Tests can inject a stub to exercise the Keychain code path without a live macOS Keychain.
    /// </summary>
    internal Func<string, CancellationToken, Task<string?>> KeychainReader
    {
        get => _keychainReader;
        set
        {
            _keychainReader = value;
            _keychainReaderOverridden = true;
        }
    }

    private Func<string, CancellationToken, Task<string?>> _keychainReader
        = DefaultMacOsKeychainReader;

    private bool _keychainReaderOverridden;

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

            var file = await ReadCredentialsFileAsync(ct).ConfigureAwait(false);
            file ??= await TryReadFromMacOsKeychainAsync(ct).ConfigureAwait(false);
            _cached = file?.ClaudeAiOauth;
            return _cached;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Explicit implementation of <see cref="ISessionProvider.GetCredentialsAsync"/>.
    /// Returns a normalised <see cref="SessionCredentials"/> wrapping the resolved token.
    /// </summary>
    async Task<SessionCredentials> ISessionProvider.GetCredentialsAsync(CancellationToken ct)
    {
        var token = await GetTokenAsync(ct).ConfigureAwait(false);
        var oauthCreds = _cached;
        var expiresAt = oauthCreds?.ExpiresAtUtc;
        return new SessionCredentials(token, expiresAt);
    }

    /// <inheritdoc/>
    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        try
        {
            await GetTokenAsync(ct).ConfigureAwait(false);
            return true;
        }
#pragma warning disable CA1031 // Intentional: check-don't-throw API
        catch
#pragma warning restore CA1031
        {
            return false;
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

        if (string.IsNullOrWhiteSpace(creds.AccessToken))
            throw new ClaudeCodeSessionException(
                "Claude credentials file is present but the access token is empty. " +
                "Run 'claude login' to obtain a new token.");

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
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Permission denied reading credentials at {Path}", path);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex,
                "I/O error reading credentials at {Path}", path);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Malformed JSON in credentials file at {Path}", path);
            return null;
        }
    }

    // Attempts to load credentials from the macOS Keychain where Claude Code v2.1.63+
    // stores OAuth tokens instead of the plain-text .credentials.json file.
    private async Task<ClaudeCodeCredentialsFile?> TryReadFromMacOsKeychainAsync(CancellationToken ct)
    {
        // Skip the OS gate when a test stub has been injected.
        if (!_keychainReaderOverridden)
        {
#if NET5_0_OR_GREATER
            if (!OperatingSystem.IsMacOS())
                return null;
#else
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return null;
#endif
        }

        _logger.LogDebug(
            "Credentials unavailable from file; trying macOS Keychain service '{Service}'",
            MacOsKeychainService);

        var raw = await KeychainReader(MacOsKeychainService, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogDebug("macOS Keychain item '{Service}' not found or empty", MacOsKeychainService);
            return null;
        }

        try
        {
            // Claude Code may store the full credentials file JSON or just the OAuth object.
            var file = JsonSerializer.Deserialize<ClaudeCodeCredentialsFile>(raw!);
            if (file?.ClaudeAiOauth is not null)
                return file;

            var oauth = JsonSerializer.Deserialize<ClaudeCodeOAuthCredentials>(raw!);
            if (oauth is not null && !string.IsNullOrWhiteSpace(oauth.AccessToken))
            {
                _logger.LogDebug("macOS Keychain: credentials parsed as raw OAuth object");
                return new ClaudeCodeCredentialsFile { ClaudeAiOauth = oauth };
            }

            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "macOS Keychain item '{Service}' contained unparseable data", MacOsKeychainService);
            return null;
        }
    }

    // Runs `security find-generic-password -s "<service>" -w` and returns the password value,
    // or null if the item does not exist or the command fails.
    private static async Task<string?> DefaultMacOsKeychainReader(string serviceName, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "security",
                    Arguments = $"find-generic-password -s \"{serviceName}\" -w",
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

#if NETSTANDARD2_0
            using (var registration = ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch (InvalidOperationException) { }
            }))
            {
                var output = await Task
                    .Run(() =>
                    {
                        var result = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        ct.ThrowIfCancellationRequested();
                        return result;
                    })
                    .ConfigureAwait(false);

                return process.ExitCode == 0 ? output?.Trim() : null;
            }
#else
            var readTask = process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var output = await readTask.ConfigureAwait(false);

            return process.ExitCode == 0 ? output?.Trim() : null;
#endif
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Intentional: swallow all other failures from the security CLI
        catch
#pragma warning restore CA1031
        {
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
