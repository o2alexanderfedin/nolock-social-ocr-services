using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;

namespace Nolock.social.MistralOcr;

/// <summary>
/// Advanced reactive pipeline for OCR processing with rate limiting, retries, and monitoring
/// </summary>
public sealed class ReactiveMistralOcrPipeline : IDisposable
{
    private readonly IMistralOcrService _ocrService;
    private readonly ILogger<ReactiveMistralOcrPipeline> _logger;
    private readonly Subject<ReactiveOcrRequest> _requestSubject = new();
    private readonly Subject<OcrResult> _resultSubject = new();
    private readonly Subject<OcrError> _errorSubject = new();
    private readonly CompositeDisposable _disposables = new();

    public ReactiveMistralOcrPipeline(
        IMistralOcrService ocrService,
        ILogger<ReactiveMistralOcrPipeline> logger,
        PipelineConfiguration? configuration = null)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var config = configuration ?? new PipelineConfiguration();

        // Build the processing pipeline
        var pipeline = BuildProcessingPipeline(config);

        // Subscribe to successful results
        _disposables.Add(pipeline
            .Where(result => result.Success)
            .Subscribe(
                result => _resultSubject.OnNext(result),
                ex => _logger.LogError(ex, "Pipeline error")));

        // Subscribe to errors
        _disposables.Add(pipeline
            .Where(result => !result.Success)
            .Subscribe(result =>
            {
                _errorSubject.OnNext(new OcrError(result.RequestId, result.Error!));
            }));
    }

    /// <summary>
    /// Submit a request to the pipeline
    /// </summary>
    /// <param name="request">The OCR request to process</param>
    public void SubmitRequest(ReactiveOcrRequest request)
    {
        _requestSubject.OnNext(request);
    }

    /// <summary>
    /// Observable stream of successful results
    /// </summary>
    public IObservable<OcrResult> Results => _resultSubject.AsObservable();

    /// <summary>
    /// Observable stream of errors
    /// </summary>
    public IObservable<OcrError> Errors => _errorSubject.AsObservable();

    /// <summary>
    /// Get pipeline statistics
    /// </summary>
    /// <param name="window">Time window for statistics updates</param>
    /// <returns>Observable stream of pipeline statistics</returns>
    public IObservable<PipelineStatistics> GetStatistics(TimeSpan window)
    {
        return Observable.Interval(window)
            .CombineLatest(
                Results.Scan(0, (count, _) => count + 1).StartWith(0),
                Errors.Scan(0, (count, _) => count + 1).StartWith(0),
                (_, successCount, errorCount) => new PipelineStatistics
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    SuccessCount = successCount,
                    ErrorCount = errorCount,
                    TotalRequests = successCount + errorCount,
                    SuccessRate = successCount > 0 ? (double)successCount / (successCount + errorCount) : 0
                });
    }

    private async Task<OcrResult> ProcessRequest(ReactiveOcrRequest request)
    {
        _logger.LogInformation("Processing OCR request: {RequestId}", request.RequestId);

        try
        {
            var startTime = DateTimeOffset.UtcNow;

            var result = request.InputType switch
            {
                OcrInputType.Url => await _ocrService.ProcessImageAsync(request.Input, request.Prompt),
                OcrInputType.DataUrl => await _ocrService.ProcessImageDataUrlAsync(request.Input, request.Prompt),
                OcrInputType.Base64 => await ProcessBase64Image(request.Input, request.MimeType!, request.Prompt),
                _ => throw new NotSupportedException($"Input type {request.InputType} is not supported")
            };

            var processingTime = DateTimeOffset.UtcNow - startTime;

            _logger.LogInformation("Successfully processed OCR request: {RequestId} in {Time}ms",
                request.RequestId, processingTime.TotalMilliseconds);

            return new OcrResult
            {
                RequestId = request.RequestId,
                Success = true,
                Text = result.Text,
                ProcessingTime = processingTime,
                ModelUsed = result.ModelUsed,
                TokensUsed = result.TotalTokens
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process OCR request: {RequestId}", request.RequestId);
            throw;
        }
    }

    private async Task<MistralOcrResult> ProcessBase64Image(string base64, string mimeType, string? prompt)
    {
        var imageBytes = Convert.FromBase64String(base64);
        return await _ocrService.ProcessImageBytesAsync(imageBytes, mimeType, prompt);
    }

    private IObservable<OcrResult> HandleError(ReactiveOcrRequest request, Exception exception)
    {
        _logger.LogError(exception, "Error processing request: {RequestId}", request.RequestId);

        return Observable.Return(new OcrResult
        {
            RequestId = request.RequestId,
            Success = false,
            Error = exception,
            ProcessingTime = TimeSpan.Zero
        });
    }

    /// <summary>
    /// Builds the reactive processing pipeline with rate limiting and concurrency control
    /// </summary>
    /// <param name="config">Pipeline configuration</param>
    /// <returns>Observable pipeline for processing OCR requests</returns>
    private IObservable<OcrResult> BuildProcessingPipeline(PipelineConfiguration config)
    {
        return _requestSubject
            .Do(req => _logger.LogDebug("Received OCR request: {RequestId}", req.RequestId))
            .GroupBy(req => req.Priority)
            .SelectMany(group => group
                .Window(config.RateLimitWindow, config.RateLimitCount)
                .SelectMany(window => window
                    .Select(req => CreateProcessingObservable(req, config))))
            .Merge(config.MaxConcurrency)
            .Publish()
            .RefCount();
    }

    /// <summary>
    /// Creates an observable for processing a single OCR request with retry and timeout
    /// </summary>
    /// <param name="request">The OCR request to process</param>
    /// <param name="config">Pipeline configuration</param>
    /// <returns>Observable that processes the request</returns>
    private IObservable<OcrResult> CreateProcessingObservable(ReactiveOcrRequest request, PipelineConfiguration config)
    {
        return Observable.Defer(() => Observable.FromAsync(() => ProcessRequest(request)))
            .Retry(config.RetryCount)
            .Timeout(config.RequestTimeout)
            .Catch<OcrResult, Exception>(ex => HandleError(request, ex));
    }

    public void Dispose()
    {
        _requestSubject.OnCompleted();
        _resultSubject.OnCompleted();
        _errorSubject.OnCompleted();

        _disposables.Dispose();
        _requestSubject.Dispose();
        _resultSubject.Dispose();
        _errorSubject.Dispose();
    }
}

/// <summary>
/// Configuration for the reactive pipeline
/// </summary>
public class PipelineConfiguration
{
    public int MaxConcurrency { get; set; } = 4;
    public int RetryCount { get; set; } = 3;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromSeconds(1);
    public int RateLimitCount { get; set; } = 10;
}

/// <summary>
/// Reactive OCR request model
/// </summary>
public class ReactiveOcrRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string Input { get; set; } = string.Empty;
    public OcrInputType InputType { get; set; }
    public string? MimeType { get; set; }
    public string? Prompt { get; set; }
    public int Priority { get; set; } = 0;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// OCR input types
/// </summary>
public enum OcrInputType
{
    Url,
    DataUrl,
    Base64
}

/// <summary>
/// OCR result model
/// </summary>
public class OcrResult
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Text { get; set; }
    public Exception? Error { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public string? ModelUsed { get; set; }
    public int? TokensUsed { get; set; }
}

/// <summary>
/// OCR error model
/// </summary>
public class OcrError
{
    public OcrError(string requestId, Exception exception)
    {
        RequestId = requestId;
        Exception = exception;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public string RequestId { get; }
    public Exception Exception { get; }
    public DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Pipeline statistics
/// </summary>
public class PipelineStatistics
{
    public DateTimeOffset Timestamp { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int TotalRequests { get; set; }
    public double SuccessRate { get; set; }
}