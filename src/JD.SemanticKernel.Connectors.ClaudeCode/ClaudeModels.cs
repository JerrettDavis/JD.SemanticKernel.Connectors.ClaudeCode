namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// Well-known Claude model identifiers for use with the Anthropic API.
/// </summary>
public static class ClaudeModels
{
    // ── Opus (highest capability) ──────────────────────────────

    /// <summary>Claude Opus 4.6 — most capable model for complex reasoning and long-context tasks.</summary>
    public const string Opus = "claude-opus-4-6";

    // ── Sonnet (balanced) ──────────────────────────────────────

    /// <summary>Claude Sonnet 4.6 — balanced speed and intelligence for everyday workloads.</summary>
    public const string Sonnet = "claude-sonnet-4-6";

    // ── Haiku (fastest / cheapest) ─────────────────────────────

    /// <summary>Claude Haiku 4.5 — fastest and most cost-effective for lightweight tasks.</summary>
    public const string Haiku = "claude-haiku-4-5-20251001";

    /// <summary>
    /// The recommended default model (<see cref="Sonnet"/>).
    /// </summary>
    public const string Default = Sonnet;
}
