using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ClaudeCodeSessionHttpHandler"/> edge cases:
/// unknown token prefixes, header idempotency, and token format boundaries.
/// </summary>
public sealed class HttpHandlerEdgeCaseTests
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
    public async Task SendAsync_UnknownTokenPrefix_UsesApiKeyPath()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("custom-token-123");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        Assert.True(recorder.CapturedRequest!.Headers.Contains("x-api-key"));
        Assert.Null(recorder.CapturedRequest.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_OAuthToken_DoesNotLeakApiKeyHeader()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-oat-token");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        Assert.False(recorder.CapturedRequest!.Headers.Contains("x-api-key"));
    }

    [Fact]
    public async Task SendAsync_StandardKey_DoesNotInjectBetaHeaders()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-api-standard");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        Assert.False(recorder.CapturedRequest!.Headers.Contains("anthropic-beta"));
        Assert.False(recorder.CapturedRequest.Headers.Contains("x-app"));
    }

    [Fact]
    public async Task SendAsync_OAuthToken_OverwritesExistingBetaHeader()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-oat-token");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/messages");
        request.Headers.TryAddWithoutValidation("anthropic-beta", "old-beta-value");
        await client.SendAsync(request);

        var betaValues = recorder.CapturedRequest!.Headers.GetValues("anthropic-beta").ToList();
        Assert.Single(betaValues);
        Assert.Contains("claude-code-20250219", betaValues[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_StandardKey_OverwritesExistingApiKeyHeader()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-api-new-key");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/messages");
        request.Headers.TryAddWithoutValidation("x-api-key", "old-placeholder-key");
        await client.SendAsync(request);

        var apiKey = string.Join(",", recorder.CapturedRequest!.Headers.GetValues("x-api-key"));
        Assert.Equal("sk-ant-api-new-key", apiKey);
    }

    [Fact]
    public async Task SendAsync_OAuthToken_SetsAnthropicVersion()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-oat-token");
        using var handler = new ClaudeCodeSessionHttpHandler(provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.anthropic.com/v1/messages");

        var ua = recorder.CapturedRequest!.Headers.UserAgent.ToString();
        Assert.NotEmpty(ua);
    }

    [Fact]
    public async Task SendAsync_HttpUrl_ThrowsInvalidOperation()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-api123");
        using var handler = new ClaudeCodeSessionHttpHandler(
            provider, recorder);
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync("http://api.anthropic.com/v1/messages"));
    }

    [Fact]
    public async Task SendAsync_LocalhostHttp_Allowed()
    {
        var recorder = new RecordingHandler();
        var provider = ProviderWithToken("sk-ant-api123");
        using var handler = new ClaudeCodeSessionHttpHandler(
            provider, recorder);
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost:5000/v1/messages");

        Assert.NotNull(recorder.CapturedRequest);
    }

    /// <summary>Recording handler for capturing requests.</summary>
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
