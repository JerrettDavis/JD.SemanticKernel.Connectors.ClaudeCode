namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ClaudeCodeSessionException"/>.
/// </summary>
public sealed class ClaudeCodeSessionExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        var ex = new ClaudeCodeSessionException("test message");
        Assert.Equal("test message", ex.Message);
    }

    [Fact]
    public void InheritsFromInvalidOperationException()
    {
        var ex = new ClaudeCodeSessionException("msg");
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public void CanBeThrownAndCaught()
    {
        Action act = () => throw new ClaudeCodeSessionException("session expired");

        var ex = Record.Exception(act);

        Assert.IsType<ClaudeCodeSessionException>(ex);
        Assert.Contains("session expired", ex.Message, StringComparison.Ordinal);
    }
}
