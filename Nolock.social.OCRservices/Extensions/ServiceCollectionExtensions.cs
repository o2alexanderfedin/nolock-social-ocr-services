namespace Nolock.social.OCRservices.Extensions;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add OCR to Model pipeline services
    /// </summary>
    public static IServiceCollection AddOcrToModelPipeline(
        this IServiceCollection services)
    {
        services.AddScoped<Pipelines.OcrToModelPipeline>();
        return services;
    }
}