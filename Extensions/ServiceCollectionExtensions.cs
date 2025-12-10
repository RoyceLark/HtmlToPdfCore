using HtmlToPdfCore.Interfaces;
using HtmlToPdfCore.Renderers;
using HtmlToPdfCore.Services;
using HtmlToPdfCore.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace HtmlToPdfCore.Extensions;

/// <summary>
/// Dependency injection extensions for HtmlToPdfCore
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds HtmlToPdfCore services to the service collection
    /// </summary>
    public static IServiceCollection AddHtmlToPdfCore(
        this IServiceCollection services,
        Action<HtmlToPdfCoreOptions>? configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var options = new HtmlToPdfCoreOptions();
        configure?.Invoke(options);

        // Register core services
        services.TryAddSingleton<IHtmlToPdfRenderer, HtmlToPdfRenderer>();
        services.TryAddSingleton<IPdfManipulator, PdfManipulator>();

        // Register AI/ML optimizer if enabled
        if (options.EnableAIOptimization)
        {
            services.TryAddSingleton<IMemoryOptimizer, MemoryOptimizer>();
        }

        // Register logging if not already configured
        services.TryAddSingleton(typeof(ILogger<>), typeof(Logger<>));

        return services;
    }

    /// <summary>
    /// Adds HtmlToPdfCore services with custom configuration
    /// </summary>
    public static IServiceCollection AddHtmlToPdfCore(
        this IServiceCollection services,
        HtmlToPdfCoreOptions options)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (options == null)
            throw new ArgumentNullException(nameof(options));

        return services.AddHtmlToPdfCore(opts =>
        {
            opts.EnableAIOptimization = options.EnableAIOptimization;
            opts.MaxConcurrentRenders = options.MaxConcurrentRenders;
            opts.DefaultRenderTimeout = options.DefaultRenderTimeout;
        });
    }
}

/// <summary>
/// Configuration options for HtmlToPdfCore
/// </summary>
public class HtmlToPdfCoreOptions
{
    /// <summary>
    /// Enable AI-powered memory optimization
    /// </summary>
    public bool EnableAIOptimization { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent render operations
    /// </summary>
    public int MaxConcurrentRenders { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Default render timeout in milliseconds
    /// </summary>
    public int DefaultRenderTimeout { get; set; } = 30000;

    /// <summary>
    /// Enable detailed logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Object pool size for memory streams
    /// </summary>
    public int ObjectPoolSize { get; set; } = 10;
}
