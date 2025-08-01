using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;

namespace Nolock.social.MistralOcr;

/// <summary>
/// Reactive implementation of Mistral OCR service using Observable streams
/// </summary>
public sealed class ReactiveMistralOcrService : IReactiveMistralOcrService, IDisposable
{
    private readonly IMistralOcrService _ocrService;
    private readonly ILogger<ReactiveMistralOcrService> _logger;
    private readonly IScheduler _scheduler;
    private readonly Subject<Unit> _dispose = new();

    // Configuration for rate limiting and concurrency
    private readonly int _maxConcurrency;
    private readonly TimeSpan _rateLimitDelay;
    private readonly int _retryCount;
    private readonly TimeSpan _retryDelay;

    public ReactiveMistralOcrService(
        IMistralOcrService ocrService,
        ILogger<ReactiveMistralOcrService> logger,
        IScheduler? scheduler = null,
        int maxConcurrency = 4,
        TimeSpan? rateLimitDelay = null,
        int retryCount = 3,
        TimeSpan? retryDelay = null)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scheduler = scheduler ?? TaskPoolScheduler.Default;
        _maxConcurrency = maxConcurrency;
        _rateLimitDelay = rateLimitDelay ?? TimeSpan.FromMilliseconds(100);
        _retryCount = retryCount;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
    }

    public IObservable<MistralOcrResult> ProcessImageUrls(IObservable<string> imageUrls, string? prompt = null)
    {
        return imageUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select((url, index) => Observable.Defer(() =>
                Observable.FromAsync(ct => _ocrService.ProcessImageAsync(url, prompt, ct))
                    .Do(_ => _logger.LogDebug("Successfully processed image {Index}: {Url}", index, url))
                    .Retry(_retryCount, _retryDelay, _scheduler)
                    .Catch<MistralOcrResult, Exception>(ex =>
                    {
                        _logger.LogError(ex, "Failed to process image {Index}: {Url} after {RetryCount} retries",
                            index, url, _retryCount);
                        return Observable.Return(MistralOcrResult.Empty);
                    })))
            .Merge(_maxConcurrency)
            .ObserveOn(_scheduler)
            .TakeUntil(_dispose);
    }

    public IObservable<MistralOcrResult> ProcessImageDataUrls(IObservable<string> dataUrls, string? prompt = null)
    {
        return dataUrls
            .Where(dataUrl => !string.IsNullOrWhiteSpace(dataUrl))
            .Select((dataUrl, index) => Observable.Defer(() =>
                Observable.FromAsync(ct => _ocrService.ProcessImageDataUrlAsync(dataUrl, prompt, ct))
                    .Do(_ => _logger.LogDebug("Successfully processed data URL {Index}", index))
                    .Retry(_retryCount, _retryDelay, _scheduler)
                    .Catch<MistralOcrResult, Exception>(ex =>
                    {
                        _logger.LogError(ex, "Failed to process data URL {Index} after {RetryCount} retries",
                            index, _retryCount);
                        return Observable.Return(MistralOcrResult.Empty);
                    })))
            .Merge(_maxConcurrency)
            .ObserveOn(_scheduler)
            .TakeUntil(_dispose);
    }

    public IObservable<MistralOcrResult> ProcessImageBytes(
        IObservable<(byte[] imageBytes, string mimeType)> images,
        string? prompt = null)
    {
        return images
            .Where(img => img.imageBytes?.Length > 0 && !string.IsNullOrWhiteSpace(img.mimeType))
            .Select((img, index) => Observable.Defer(() =>
                Observable.FromAsync(ct => _ocrService.ProcessImageBytesAsync(img.imageBytes, img.mimeType, prompt, ct))
                    .Do(_ => _logger.LogDebug("Successfully processed image bytes {Index}", index))
                    .Retry(_retryCount, _retryDelay, _scheduler)
                    .Catch<MistralOcrResult, Exception>(ex =>
                    {
                        _logger.LogError(ex, "Failed to process image bytes {Index} after {RetryCount} retries",
                            index, _retryCount);
                        return Observable.Return(MistralOcrResult.Empty);
                    })))
            .Merge(_maxConcurrency)
            .ObserveOn(_scheduler)
            .TakeUntil(_dispose);
    }

    public IObservable<MistralOcrResult> ProcessImageStreams(
        IObservable<(Stream imageStream, string mimeType)> streams,
        string? prompt = null)
    {
        return streams
            .Where(s => s.imageStream != null && !string.IsNullOrWhiteSpace(s.mimeType))
            .Select((s, index) => Observable.Defer(() =>
                Observable.FromAsync(ct => _ocrService.ProcessImageStreamAsync(s.imageStream, s.mimeType, prompt, ct))
                    .Do(_ => _logger.LogDebug("Successfully processed image stream {Index}", index))
                    .Retry(_retryCount, _retryDelay, _scheduler)
                    .Catch<MistralOcrResult, Exception>(ex =>
                    {
                        _logger.LogError(ex, "Failed to process image stream {Index} after {RetryCount} retries",
                            index, _retryCount);
                        return Observable.Return(MistralOcrResult.Empty);
                    })))
            .Merge(_maxConcurrency)
            .ObserveOn(_scheduler)
            .TakeUntil(_dispose);
    }

    public IObservable<IList<MistralOcrResult>> ProcessImageUrlsBatch(
        IObservable<string> imageUrls,
        int batchSize,
        string? prompt = null)
    {
        return imageUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Buffer(batchSize)
            .Where(batch => batch.Count > 0)
            .Select(batch =>
            {
                _logger.LogInformation("Processing batch of {Count} images", batch.Count);

                return batch
                    .ToObservable()
                    .SelectMany(url => Observable.FromAsync(ct =>
                        _ocrService.ProcessImageAsync(url, prompt, ct))
                        .Retry(_retryCount, _retryDelay, _scheduler)
                        .Catch<MistralOcrResult, Exception>(ex =>
                        {
                            _logger.LogError(ex, "Failed to process {Url} in batch", url);
                            return Observable.Return(MistralOcrResult.Empty);
                        }))
                    .ToList()
                    .Do(results => _logger.LogInformation("Completed batch with {Count} results", results.Count));
            })
            .Merge(1) // Process batches sequentially
            .ObserveOn(_scheduler)
            .TakeUntil(_dispose);
    }

    public IObservable<(string ImageUrl, MistralOcrResult? Result, Exception? Error)> ProcessImageUrlsWithErrors(
        IObservable<string> imageUrls,
        string? prompt = null)
    {
        return imageUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => Observable.Defer(() =>
                Observable.FromAsync(ct => _ocrService.ProcessImageAsync(url, prompt, ct))
                    .Select(result => (url, Result: (MistralOcrResult?)result, Error: (Exception?)null))
                    .Catch<(string, MistralOcrResult?, Exception?), Exception>(ex =>
                        Observable.Return<(string, MistralOcrResult?, Exception?)>((url, null, ex)))))
            .Merge(_maxConcurrency)
            .ObserveOn(_scheduler)
            .TakeUntil(_dispose);
    }

    public void Dispose()
    {
        _dispose.OnNext(Unit.Default);
        _dispose.OnCompleted();
        _dispose.Dispose();
    }
}

/// <summary>
/// Extension methods for Observable retry with delay
/// </summary>
public static class ObservableExtensions
{
    public static IObservable<T> Retry<T>(
        this IObservable<T> source,
        int retryCount,
        TimeSpan delay,
        IScheduler scheduler)
    {
        return source.RetryWhen(failures => failures
            .Zip(Enumerable.Range(1, retryCount), (error, attempt) => attempt)
            .SelectMany(attempt => Observable.Timer(delay, scheduler)));
    }
}