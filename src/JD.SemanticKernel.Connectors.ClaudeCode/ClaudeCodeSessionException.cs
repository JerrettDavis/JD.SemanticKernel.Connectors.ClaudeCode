namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// Thrown when Claude Code credentials are unavailable, expired, or not configured.
/// The <see cref="Exception.Message"/> is safe to display directly to end users.
/// </summary>
public sealed class ClaudeCodeSessionException : InvalidOperationException
{
    /// <inheritdoc cref="InvalidOperationException()"/>
    public ClaudeCodeSessionException() { }

    /// <inheritdoc cref="InvalidOperationException(string)"/>
    public ClaudeCodeSessionException(string message) : base(message) { }

    /// <inheritdoc cref="InvalidOperationException(string, Exception)"/>
    public ClaudeCodeSessionException(string message, Exception innerException)
        : base(message, innerException) { }
}
