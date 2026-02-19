using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JD.SemanticKernel.Connectors.ClaudeCode;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering Claude Code authentication services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ClaudeCodeSessionProvider"/> and binds
    /// <see cref="ClaudeCodeSessionOptions"/> from the <c>"ClaudeSession"</c> configuration section.
    /// </summary>
    /// <remarks>
    /// After calling this method you can inject <see cref="ClaudeCodeSessionProvider"/> or
    /// <see cref="ClaudeCodeSessionHttpHandler"/> (constructed manually) into your own services.
    /// For a one-call Semantic Kernel setup see
    /// <c>IKernelBuilder.UseClaudeCodeChatCompletion()</c> in the net8.0+ target.
    /// </remarks>
    public static IServiceCollection AddClaudeCodeAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
#if NETSTANDARD2_0
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));
#else
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
#endif

        services.Configure<ClaudeCodeSessionOptions>(
            configuration.GetSection(ClaudeCodeSessionOptions.SectionName));

        services.AddSingleton<ClaudeCodeSessionProvider>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="ClaudeCodeSessionProvider"/> and configures
    /// <see cref="ClaudeCodeSessionOptions"/> via the provided <paramref name="configure"/> delegate.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddClaudeCodeAuthentication(o => o.ApiKey = "sk-ant-api...");
    /// </code>
    /// </example>
    public static IServiceCollection AddClaudeCodeAuthentication(
        this IServiceCollection services,
        Action<ClaudeCodeSessionOptions>? configure = null)
    {
#if NETSTANDARD2_0
        if (services is null) throw new ArgumentNullException(nameof(services));
#else
        ArgumentNullException.ThrowIfNull(services);
#endif

        if (configure is not null)
            services.Configure<ClaudeCodeSessionOptions>(configure);
        else
            services.Configure<ClaudeCodeSessionOptions>(static _ => { });

        services.AddSingleton<ClaudeCodeSessionProvider>();
        return services;
    }
}
