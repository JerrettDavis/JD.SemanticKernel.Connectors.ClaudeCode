using JD.SemanticKernel.Connectors.Abstractions;

namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ClaudeModelDiscovery"/>.
/// </summary>
public sealed class ClaudeModelDiscoveryTests
{
    [Fact]
    public async Task DiscoverModelsAsync_ReturnsThreeModels()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        Assert.Equal(3, models.Count);
    }

    [Fact]
    public async Task DiscoverModelsAsync_ContainsOpusSonnetHaiku()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        var ids = models.Select(m => m.Id).ToList();
        Assert.Contains(ClaudeModels.Opus, ids);
        Assert.Contains(ClaudeModels.Sonnet, ids);
        Assert.Contains(ClaudeModels.Haiku, ids);
    }

    [Fact]
    public async Task DiscoverModelsAsync_AllModelsHaveProvider()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        Assert.All(models, m => Assert.Equal("anthropic", m.Provider));
    }

    [Fact]
    public async Task DiscoverModelsAsync_AllModelsHaveNonEmptyIdAndName()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        Assert.All(models, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Id));
            Assert.False(string.IsNullOrWhiteSpace(m.Name));
        });
    }

    [Fact]
    public async Task DiscoverModelsAsync_ReturnsSameInstanceAcrossCalls()
    {
        var discovery = new ClaudeModelDiscovery();
        var first = await discovery.DiscoverModelsAsync();
        var second = await discovery.DiscoverModelsAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task DiscoverModelsAsync_RespectsNonCancelledToken()
    {
        using var cts = new CancellationTokenSource();
        var discovery = new ClaudeModelDiscovery();

        var models = await discovery.DiscoverModelsAsync(cts.Token);

        Assert.NotEmpty(models);
    }

    [Fact]
    public async Task DiscoverModelsAsync_CompletesSuccessfully_WithDefaultToken()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        Assert.IsAssignableFrom<IReadOnlyList<ModelInfo>>(models);
    }

    [Fact]
    public async Task DiscoverModelsAsync_OpusModel_HasExpectedName()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        var opus = models.Single(m => string.Equals(m.Id, ClaudeModels.Opus, StringComparison.Ordinal));
        Assert.Contains("Opus", opus.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverModelsAsync_SonnetModel_HasExpectedName()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        var sonnet = models.Single(m => string.Equals(m.Id, ClaudeModels.Sonnet, StringComparison.Ordinal));
        Assert.Contains("Sonnet", sonnet.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverModelsAsync_HaikuModel_HasExpectedName()
    {
        var discovery = new ClaudeModelDiscovery();
        var models = await discovery.DiscoverModelsAsync();

        var haiku = models.Single(m => string.Equals(m.Id, ClaudeModels.Haiku, StringComparison.Ordinal));
        Assert.Contains("Haiku", haiku.Name, StringComparison.Ordinal);
    }
}
