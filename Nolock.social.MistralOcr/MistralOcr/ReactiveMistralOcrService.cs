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

    public IObservable<MistralOcrResult> ProcessImageDataItems(IObservable<(string url, string mimeType)> dataItems)
    {
        return dataItems
            .Where(dataItem => dataItem.url != null)
            .Select((dataItem, index) => Observable.Defer(() =>
                Observable.FromAsync(ct => _ocrService.ProcessImageDataItemAsync(dataItem, ct))
                    .Do(_ => _logger.LogDebug("Successfully processed data URL {Index} with MIME type {MimeType}", index, dataItem.mimeType))
                    .Retry(_retryCount, _retryDelay, _scheduler)
                    .Catch<MistralOcrResult, Exception>(ex =>
                    {
                        _logger.LogError(ex, "Failed to process data URL {Index} with MIME type {MimeType} after {RetryCount} retries",
                            index, dataItem.mimeType, _retryCount);
                        return Observable.Return(MistralOcrResult.Empty);
                    })))
            .Merge(_maxConcurrency)
            .ObserveOn(_scheduler)
            .TakeUntil(_dispose);
    }

    public IObservable<MistralOcrResult> ProcessImageBytes(
        IObservable<(byte[] imageBytes, string mimeType)> images)
    {
        return images
            .Where(img => img.imageBytes?.Length > 0 && !string.IsNullOrWhiteSpace(img.mimeType))
            .Select((img, index) => Observable.Defer(() =>
                Observable.FromAsync(ct => _ocrService.ProcessImageBytesAsync(img.imageBytes, img.mimeType, ct))
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
        IObservable<(Stream imageStream, string mimeType)> streams)
    {
        return streams
            .Where(s => s.imageStream != null && !string.IsNullOrWhiteSpace(s.mimeType))
            .Select((s, index) => Observable.Defer(() =>
                Observable.FromAsync(ct => _ocrService.ProcessImageStreamAsync(s.imageStream, s.mimeType, ct))
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