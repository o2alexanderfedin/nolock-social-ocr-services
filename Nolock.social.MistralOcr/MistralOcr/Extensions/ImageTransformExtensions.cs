using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nolock.social.MistralOcr.Extensions;

/// <summary>
/// Extension methods for image URL to data URL transformations in reactive pipelines
/// </summary>
public static class ImageTransformExtensions
{
    /// <summary>
    /// Transform image URLs to data URLs in a reactive stream
    /// </summary>
    /// <param name="source">Observable stream of image URLs</param>
    /// <param name="transformer">Image transformer instance</param>
    /// <returns>Observable stream of data URLs</returns>
    public static IObservable<string> ToDataUrls(
        this IObservable<string> source,
        IImageUrlToDataUrlTransformer transformer)
    {
        return transformer.Transform(source);
    }

    /// <summary>
    /// Transform image URLs to data URLs with error handling
    /// </summary>
    /// <param name="source">Observable stream of image URLs</param>
    /// <param name="transformer">Image transformer instance</param>
    /// <returns>Observable stream of transformation results</returns>
    public static IObservable<ImageTransformResult> ToDataUrlsWithErrors(
        this IObservable<string> source,
        IImageUrlToDataUrlTransformer transformer)
    {
        return transformer.TransformWithErrors(source);
    }

    /// <summary>
    /// Process images through OCR, automatically converting URLs to data URLs
    /// </summary>
    /// <param name="imageUrls">Observable stream of image URLs (can be regular URLs or data URLs)</param>
    /// <param name="ocrService">OCR service instance</param>
    /// <param name="transformer">Image transformer instance</param>
    /// <returns>Observable stream of OCR results</returns>
    public static IObservable<MistralOcrResult> ProcessImagesWithTransform(
        this IObservable<string> imageUrls,
        IReactiveMistralOcrService ocrService,
        IImageUrlToDataUrlTransformer transformer)
    {
        return imageUrls
            .ToDataUrlsWithErrors(transformer)
            .Where(result => result.Success && !string.IsNullOrEmpty(result.DataUrl))
            .Select(result => (new Uri(result.DataUrl!), result.DetectedMimeType ?? "application/octet-stream"))
            .SelectMany(dataItem => ocrService.ProcessImageDataItems(Observable.Return(dataItem)));
    }

    /// <summary>
    /// Process images through OCR with automatic URL transformation and error handling
    /// </summary>
    /// <param name="imageUrls">Observable stream of image URLs</param>
    /// <param name="ocrService">OCR service instance</param>
    /// <param name="transformer">Image transformer instance</param>
    /// <returns>Observable stream of OCR processing results with transformation info</returns>
    public static IObservable<ImageOcrProcessingResult> ProcessImagesWithTransformAndErrors(
        this IObservable<string> imageUrls,
        IReactiveMistralOcrService ocrService,
        IImageUrlToDataUrlTransformer transformer)
    {
        return imageUrls
            .ToDataUrlsWithErrors(transformer)
            .SelectMany(transformResult =>
            {
                if (!transformResult.Success || string.IsNullOrEmpty(transformResult.DataUrl))
                {
                    // Return error result
                    return Observable.Return(new ImageOcrProcessingResult
                    {
                        OriginalUrl = transformResult.OriginalUrl,
                        TransformResult = transformResult,
                        Success = false
                    });
                }

                // Process through OCR
                return ocrService.ProcessImageDataItems(Observable.Return((new Uri(transformResult.DataUrl), transformResult.DetectedMimeType ?? "application/octet-stream")))
                    .Select(ocrResult => new ImageOcrProcessingResult
                    {
                        OriginalUrl = transformResult.OriginalUrl,
                        TransformResult = transformResult,
                        OcrResult = ocrResult,
                        Success = true
                    })
                    .Catch<ImageOcrProcessingResult, Exception>(ex =>
                        Observable.Return(new ImageOcrProcessingResult
                        {
                            OriginalUrl = transformResult.OriginalUrl,
                            TransformResult = transformResult,
                            OcrError = ex,
                            Success = false
                        }));
            });
    }

    /// <summary>
    /// Add image URL to data URL transformation to the service collection
    /// </summary>
    public static IServiceCollection AddImageTransformation(
        this IServiceCollection services,
        Action<ImageTransformOptions>? configureOptions = null)
    {
        var options = new ImageTransformOptions();
        configureOptions?.Invoke(options);

        // Configure named HttpClient for the transformer
        services.AddHttpClient("ImageUrlToDataUrlTransformer", client =>
        {
            client.Timeout = options.HttpTimeout;
            client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
        });

        // Register the transformer as a singleton
        services.AddSingleton<IImageUrlToDataUrlTransformer>(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var logger = provider.GetRequiredService<ILogger<ImageUrlToDataUrlTransformer>>();

            return new ImageUrlToDataUrlTransformer(
                httpClientFactory,
                logger,
                options.MaxConcurrency,
                options.RetryCount,
                options.RetryDelay,
                options.RequestTimeout);
        });

        return services;
    }
}

/// <summary>
/// Options for image transformation
/// </summary>
public class ImageTransformOptions
{
    public int MaxConcurrency { get; set; } = 4;
    public int RetryCount { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public string UserAgent { get; set; } = "Nolock.social.MistralOcr/1.0";
}

/// <summary>
/// Combined result of image transformation and OCR processing
/// </summary>
public class ImageOcrProcessingResult
{
    public string OriginalUrl { get; set; } = string.Empty;
    public ImageTransformResult? TransformResult { get; set; }
    public MistralOcrResult? OcrResult { get; set; }
    public Exception? OcrError { get; set; }
    public bool Success { get; set; }
    
    public string? ExtractedText => OcrResult?.Text;
    public TimeSpan TotalProcessingTime => 
        (TransformResult?.ProcessingTime ?? TimeSpan.Zero) + 
        (OcrResult?.ProcessingTime ?? TimeSpan.Zero);
}