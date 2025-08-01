using System.Reactive;

namespace Nolock.social.MistralOcr;

/// <summary>
/// Interface for transforming image URLs to data URLs
/// </summary>
public interface IImageUrlToDataUrlTransformer
{
    /// <summary>
    /// Transform a single image URL to a data URL
    /// </summary>
    /// <param name="imageUrl">The image URL to transform</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Data URL string</returns>
    Task<string> TransformAsync(string imageUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transform multiple image URLs to data URLs using reactive streams
    /// </summary>
    /// <param name="imageUrls">Observable stream of image URLs</param>
    /// <returns>Observable stream of data URLs</returns>
    IObservable<string> Transform(IObservable<string> imageUrls);

    /// <summary>
    /// Transform image URLs with error handling
    /// </summary>
    /// <param name="imageUrls">Observable stream of image URLs</param>
    /// <returns>Observable stream of results with error information</returns>
    IObservable<ImageTransformResult> TransformWithErrors(IObservable<string> imageUrls);
}

/// <summary>
/// Result of image URL to data URL transformation
/// </summary>
public class ImageTransformResult
{
    public string OriginalUrl { get; set; } = string.Empty;
    public string? DataUrl { get; set; }
    public Exception? Error { get; set; }
    public bool Success => Error == null && DataUrl != null;
    public TimeSpan ProcessingTime { get; set; }
    public string? DetectedMimeType { get; set; }
    public long? ContentLength { get; set; }
}