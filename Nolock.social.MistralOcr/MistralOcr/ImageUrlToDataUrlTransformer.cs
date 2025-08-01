using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nolock.social.MistralOcr;

/// <summary>
/// Transforms image URLs to data URLs by downloading and encoding images
/// </summary>
public class ImageUrlToDataUrlTransformer : IImageUrlToDataUrlTransformer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageUrlToDataUrlTransformer> _logger;
    private readonly int _maxConcurrency;
    private readonly int _retryCount;
    private readonly TimeSpan _retryDelay;
    private readonly TimeSpan _timeout;
    private const string HttpClientName = "ImageUrlToDataUrlTransformer";

    // Common file MIME types
    private static readonly Dictionary<string, string> MimeTypeMap = new()
    {
        // Image types
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },
        { ".tiff", "image/tiff" },
        { ".tif", "image/tiff" },
        // Document types
        { ".pdf", "application/pdf" }
    };

    public ImageUrlToDataUrlTransformer(
        IHttpClientFactory httpClientFactory,
        ILogger<ImageUrlToDataUrlTransformer> logger,
        int maxConcurrency = 4,
        int retryCount = 3,
        TimeSpan? retryDelay = null,
        TimeSpan? timeout = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxConcurrency = maxConcurrency;
        _retryCount = retryCount;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<string> TransformAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("Image URL cannot be null or empty", nameof(imageUrl));
        }

        // If it's already a data URL, return it as-is
        if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("URL is already a data URL, returning as-is");
            return imageUrl;
        }

        try
        {
            _logger.LogDebug("Downloading image from URL: {Url}", imageUrl);

            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            using var response = await httpClient.GetAsync(imageUrl, cts.Token);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(contentType))
            {
                // Try to detect from URL extension
                contentType = DetectMimeTypeFromUrl(imageUrl);
                _logger.LogDebug("Detected MIME type from URL: {MimeType}", contentType);
            }

            // Handle cases where content type is generic or missing
            if (string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream")
            {
                // For PDFs served as octet-stream, check the URL
                if (imageUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "application/pdf";
                }
            }
            
            if (string.IsNullOrEmpty(contentType))
            {
                throw new InvalidOperationException("Unable to determine content type");
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var base64String = Convert.ToBase64String(imageBytes);
            var dataUrl = $"data:{contentType};base64,{base64String}";

            _logger.LogInformation("Successfully transformed image URL to data URL. Size: {Size} bytes", imageBytes.Length);
            return dataUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transform image URL: {Url}", imageUrl);
            throw;
        }
    }

    public IObservable<string> Transform(IObservable<string> imageUrls)
    {
        return imageUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => Observable.FromAsync(ct => TransformAsync(url, ct))
                .Retry(_retryCount, _retryDelay)
                .Catch<string, Exception>(ex =>
                {
                    _logger.LogError(ex, "Failed to transform URL after {RetryCount} retries: {Url}", _retryCount, url);
                    return Observable.Empty<string>();
                }))
            .Merge(_maxConcurrency);
    }

    public IObservable<ImageTransformResult> TransformWithErrors(IObservable<string> imageUrls)
    {
        return imageUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url =>
            {
                var startTime = DateTime.UtcNow;
                return Observable.FromAsync(async ct =>
                {
                    try
                    {
                        // If already a data URL, return immediately
                        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            return new ImageTransformResult
                            {
                                OriginalUrl = url,
                                DataUrl = url,
                                ProcessingTime = TimeSpan.Zero
                            };
                        }

                        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
                        
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(_timeout);

                        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                        response.EnsureSuccessStatusCode();

                        var contentType = response.Content.Headers.ContentType?.MediaType;
                        var contentLength = response.Content.Headers.ContentLength;

                        if (string.IsNullOrEmpty(contentType))
                        {
                            contentType = DetectMimeTypeFromUrl(url);
                        }
                        
                        // Handle cases where content type is generic or missing
                        if (string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream")
                        {
                            // For PDFs served as octet-stream, check the URL
                            if (url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                contentType = "application/pdf";
                            }
                        }

                        var imageBytes = await response.Content.ReadAsByteArrayAsync(ct);
                        var base64String = Convert.ToBase64String(imageBytes);
                        var dataUrl = $"data:{contentType};base64,{base64String}";

                        return new ImageTransformResult
                        {
                            OriginalUrl = url,
                            DataUrl = dataUrl,
                            ProcessingTime = DateTime.UtcNow - startTime,
                            DetectedMimeType = contentType,
                            ContentLength = contentLength ?? imageBytes.Length
                        };
                    }
                    catch (Exception ex)
                    {
                        return new ImageTransformResult
                        {
                            OriginalUrl = url,
                            Error = ex,
                            ProcessingTime = DateTime.UtcNow - startTime
                        };
                    }
                })
                .Retry(_retryCount, _retryDelay)
                .Catch<ImageTransformResult, Exception>(ex =>
                {
                    _logger.LogError(ex, "Failed to transform URL after all retries: {Url}", url);
                    return Observable.Return(new ImageTransformResult
                    {
                        OriginalUrl = url,
                        Error = ex,
                        ProcessingTime = DateTime.UtcNow - startTime
                    });
                });
            })
            .Merge(_maxConcurrency);
    }

    private static string DetectMimeTypeFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var extension = Path.GetExtension(uri.LocalPath)?.ToLowerInvariant();
            
            if (!string.IsNullOrEmpty(extension) && MimeTypeMap.TryGetValue(extension, out var mimeType))
            {
                return mimeType;
            }
        }
        catch
        {
            // Ignore URI parsing errors
        }

        // Default to jpeg if we can't detect
        return "image/jpeg";
    }
}

/// <summary>
/// Extension methods for Observable retry with delay
/// </summary>
public static class ImageTransformerObservableExtensions
{
    public static IObservable<T> Retry<T>(
        this IObservable<T> source,
        int retryCount,
        TimeSpan delay)
    {
        return source.RetryWhen(failures => failures
            .Zip(Enumerable.Range(1, retryCount), (error, attempt) => attempt)
            .SelectMany(attempt => Observable.Timer(delay)));
    }
}