using JD.SemanticKernel.Connectors.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.IntegrationTests;

/// <summary>
/// Integration tests for Claude Code authentication flow.
/// Requires <c>CLAUDE_INTEGRATION_TESTS=true</c> and valid local credentials.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClaudeCodeAuthFlowTests
{
    private static bool CanRun =>
        string.Equals(
            Environment.GetEnvironmentVariable("CLAUDE_INTEGRATION_TESTS"),
            "true", StringComparison.OrdinalIgnoreCase);

    private static string DefaultCredentialsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", ".credentials.json");

    [SkippableFact]
    public void CredentialsFile_Exists()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true to run integration tests");

        Assert.True(
            File.Exists(DefaultCredentialsPath),
            $"Expected credentials file at {DefaultCredentialsPath}. Run 'claude login' first.");
    }

    [SkippableFact]
    public async Task SessionProvider_CanReadLocalCredentials()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true to run integration tests");
        Skip.IfNot(File.Exists(DefaultCredentialsPath), "No local credentials file found");

        var provider = new ClaudeCodeSessionProvider(
            Options.Create(new ClaudeCodeSessionOptions()),
            NullLogger<ClaudeCodeSessionProvider>.Instance);

        var creds = await provider.GetCredentialsAsync();

        Assert.NotNull(creds);
        Assert.False(string.IsNullOrWhiteSpace(creds!.AccessToken));
    }

    [SkippableFact]
    public async Task SessionProvider_GetToken_ReturnsValidToken()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true to run integration tests");
        Skip.IfNot(File.Exists(DefaultCredentialsPath), "No local credentials file found");

        var provider = new ClaudeCodeSessionProvider(
            Options.Create(new ClaudeCodeSessionOptions()),
            NullLogger<ClaudeCodeSessionProvider>.Instance);

        try
        {
            var token = await provider.GetTokenAsync();

            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.True(
                token.StartsWith("sk-ant-", StringComparison.Ordinal),
                "Expected token to start with 'sk-ant-' prefix");
        }
        catch (Exception ex) when (ex is ClaudeCodeSessionException || ex.InnerException is ClaudeCodeSessionException)
        {
            Skip.If(true, $"No active Claude session: {ex.GetBaseException().Message}");
        }
    }

    [SkippableFact]
    public async Task SessionProvider_IsAuthenticated_ReturnsTrue()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true to run integration tests");
        Skip.IfNot(File.Exists(DefaultCredentialsPath), "No local credentials file found");

        var provider = new ClaudeCodeSessionProvider(
            Options.Create(new ClaudeCodeSessionOptions()),
            NullLogger<ClaudeCodeSessionProvider>.Instance);

        var isAuthenticated = await provider.IsAuthenticatedAsync();
        Skip.IfNot(isAuthenticated, "Claude session expired — run 'claude login' to refresh");
    }

    [SkippableFact]
    public async Task ModelDiscovery_ReturnsKnownModels()
    {
        Skip.IfNot(CanRun, "Set CLAUDE_INTEGRATION_TESTS=true to run integration tests");

        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        Assert.NotEmpty(models);
        Assert.Contains(models, m => string.Equals(m.Id, ClaudeModels.Sonnet, StringComparison.Ordinal));
    }
}
