using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ClaudeCodeSessionHttpHandler"/> header injection behaviour.
/// Uses a recording inner handler to capture outgoing requests without hitting the network.
/// </summary>
public sealed class ClaudeCodeSessionHttpHandlerTests
{
    private static ClaudeCodeSessionProvider ProviderWithToken(string token)
    {
        var options = token.Contains("sk-ant-oat")
            ? new ClaudeCodeSessionOptions { OAuthToken = token }
            : new ClaudeCodeSessionOptions { ApiKey = token };

        return new ClaudeCodeSessionProvider(
            Options.Create(options),
            NullLogger<ClaudeCodeSessionProvider>.Instance);
    }

    [Fact]
    public async Task SendAsync_OAuthToken_SetsBearerAuthorization()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-oat-my-oauth-token");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        Assert.NotNull(recorder.CapturedRequest);
        Assert.Equal("Bearer", recorder.CapturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("sk-ant-oat-my-oauth-token", recorder.CapturedRequest!.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SendAsync_OAuthToken_InjectsClaudeCodeBetaHeader()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-oat-my-oauth-token");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        var betaHeader = recorder.CapturedRequest!.Headers.TryGetValues("anthropic-beta", out var vals)
            ? string.Join(",", vals)
            : null;

        Assert.NotNull(betaHeader);
        Assert.Contains("claude-code-20250219", betaHeader, StringComparison.Ordinal);
        Assert.Contains("oauth-2025-04-20", betaHeader, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_OAuthToken_InjectsXAppCliHeader()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-oat-my-oauth-token");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        var xApp = recorder.CapturedRequest!.Headers.TryGetValues("x-app", out var vals)
            ? string.Join(",", vals)
            : null;

        Assert.Equal("cli", xApp);
    }

    [Fact]
    public async Task SendAsync_OAuthToken_InjectsClaudeCliUserAgent()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-oat-my-oauth-token");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        var userAgent = recorder.CapturedRequest!.Headers.UserAgent.ToString();
        Assert.Contains("claude-cli/", userAgent, StringComparison.Ordinal);
        Assert.Contains("external, cli", userAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_OAuthToken_RemovesXApiKeyHeader()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-oat-my-oauth-token");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/messages");
        request.Headers.TryAddWithoutValidation("x-api-key", "should-be-removed");
        await client.SendAsync(request);

        Assert.False(recorder.CapturedRequest!.Headers.Contains("x-api-key"));
    }

    [Fact]
    public async Task SendAsync_StandardApiKey_SetsXApiKeyHeader()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-api-standard-key");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        var apiKey = recorder.CapturedRequest!.Headers.TryGetValues("x-api-key", out var vals)
            ? string.Join(",", vals)
            : null;

        Assert.Equal("sk-ant-api-standard-key", apiKey);
    }

    [Fact]
    public async Task SendAsync_StandardApiKey_DoesNotSetBearerAuth()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-api-standard-key");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        Assert.Null(recorder.CapturedRequest!.Headers.Authorization);
    }

    /// <summary>Captures the outgoing request without making a real HTTP call.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
