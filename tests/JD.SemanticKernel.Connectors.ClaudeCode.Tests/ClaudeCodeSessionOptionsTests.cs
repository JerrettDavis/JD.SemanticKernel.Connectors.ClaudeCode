namespace JD.SemanticKernel.Connectors.ClaudeCode.Tests;

/// <summary>
/// Tests for <see cref="ClaudeCodeSessionOptions"/> defaults and constants.
/// </summary>
public sealed class ClaudeCodeSessionOptionsTests
{
    [Fact]
    public void SectionName_IsClaudeSession()
    {
        Assert.Equal("ClaudeSession", ClaudeCodeSessionOptions.SectionName);
    }

    [Fact]
    public void Defaults_AllPropertiesAreNull()
    {
        var options = new ClaudeCodeSessionOptions();

        Assert.Null(options.ApiKey);
        Assert.Null(options.OAuthToken);
        Assert.Null(options.CredentialsPath);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var options = new ClaudeCodeSessionOptions
        {
            ApiKey = "sk-ant-api-test",
            OAuthToken = "sk-ant-oat-test",
            CredentialsPath = "/custom/path"
        };

        Assert.Equal("sk-ant-api-test", options.ApiKey);
        Assert.Equal("sk-ant-oat-test", options.OAuthToken);
        Assert.Equal("/custom/path", options.CredentialsPath);
    }
}
