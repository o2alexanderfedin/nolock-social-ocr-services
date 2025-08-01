using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace Nolock.social.MistralOcr.Extensions;

/// <summary>
/// Extension methods for registering reactive Mistral OCR services
/// </summary>
public static class ReactiveMistralOcrServiceExtensions
{
    /// <summary>
    /// Add reactive Mistral OCR services to the service collection
    /// </summary>
    public static IServiceCollection AddReactiveMistralOcr(
        this IServiceCollection services,
        Action<ReactiveMistralOcrOptions>? configureOptions = null)
    {
        // Ensure base OCR service is registered
        services.TryAddSingleton<IMistralOcrService, MistralOcrApiService>();

        // Configure options
        var options = new ReactiveMistralOcrOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // Register scheduler
        services.TryAddSingleton(sp => options.Scheduler ?? TaskPoolScheduler.Default);

        // Register reactive service
        services.AddSingleton<IReactiveMistralOcrService>(sp =>
        {
            var ocrService = sp.GetRequiredService<IMistralOcrService>();
            var logger = sp.GetRequiredService<ILogger<ReactiveMistralOcrService>>();
            var scheduler = sp.GetRequiredService<IScheduler>();

            return new ReactiveMistralOcrService(
                ocrService,
                logger,
                scheduler,
                options.MaxConcurrency,
                options.RateLimitDelay,
                options.RetryCount,
                options.RetryDelay);
        });

        // Register pipeline
        services.AddSingleton(sp =>
        {
            var ocrService = sp.GetRequiredService<IMistralOcrService>();
            var logger = sp.GetRequiredService<ILogger<ReactiveMistralOcrPipeline>>();

            return new ReactiveMistralOcrPipeline(
                ocrService,
                logger,
                options.PipelineConfiguration);
        });

        return services;
    }
}

/// <summary>
/// Options for reactive Mistral OCR services
/// </summary>
public class ReactiveMistralOcrOptions
{
    /// <summary>
    /// Scheduler to use for reactive operations
    /// </summary>
    public IScheduler? Scheduler { get; set; }

    /// <summary>
    /// Maximum number of concurrent OCR operations
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Delay between rate-limited requests
    /// </summary>
    public TimeSpan RateLimitDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Number of retry attempts for failed operations
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Configuration for the reactive pipeline
    /// </summary>
    public PipelineConfiguration PipelineConfiguration { get; set; } = new();
}