using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using Nolock.social.OCRservices.Core.Pipelines;
using Nolock.social.OCRservices.Tests.TestData;

namespace Nolock.social.OCRservices.Tests.Pipelines;

/// <summary>
/// Tests for reactive extensions applied to existing pipeline nodes
/// </summary>
public class ReactivePipelineNodeTests : IDisposable
{
    private readonly PipelineNodeImageToUrl _imageToUrlNode;
    private readonly List<IDisposable> _disposables = new();

    public ReactivePipelineNodeTests()
    {
        _imageToUrlNode = new PipelineNodeImageToUrl();
    }

    [Fact]
    public async Task ImageToUrlNode_WithReactiveStream_ShouldProcessMultipleImages()
    {
        // Arrange
        var imageStreams = new[]
        {
            TestImageHelper.GetReceiptImageStream(),
            TestImageHelper.GetCheckImageStream(),
            TestImageHelper.GetReceiptImageStream()
        };

        var source = imageStreams.ToObservable();
        var results = new List<(string url, string mimeType)>();

        // Act
        var subscription = source
            .Through(_imageToUrlNode)
            .Subscribe(
                result => results.Add(result),
                error => throw error);

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(1000);

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(result =>
        {
            result.url.Should().StartWith("data:");
            result.mimeType.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task ImageToUrlNode_WithBackpressure_ShouldControlConcurrency()
    {
        // Arrange
        var imageStreams = Enumerable.Range(0, 10)
            .Select(_ => TestImageHelper.GetReceiptImageStream())
            .ToArray();

        var source = imageStreams.ToObservable();
        var results = new List<(string url, string mimeType)>();
        var processingTimes = new List<DateTime>();

        // Act
        var subscription = source
            .ThroughWithBackpressure(_imageToUrlNode, maxConcurrency: 3)
            .Do(_ => processingTimes.Add(DateTime.UtcNow))
            .Subscribe(
                result => results.Add(result),
                error => throw error);

        _disposables.Add(subscription);

        // Wait for all processing
        await Task.Delay(2000);

        // Assert
        results.Should().HaveCount(10);
        results.Should().AllSatisfy(result =>
        {
            result.url.Should().StartWith("data:");
            result.mimeType.Should().BeOneOf("image/jpeg", "image/png", "image/gif", "image/bmp");
        });

        // Verify that processing was controlled (not all at once)
        processingTimes.Should().HaveCount(10);
    }

    [Fact]
    public async Task ImageToUrlNode_WithInvalidStream_ShouldHandleError()
    {
        // Arrange
        var invalidStream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 }); // Invalid image data
        var source = Observable.Return(invalidStream);
        var errors = new List<Exception>();

        // Act
        var subscription = source
            .Through(_imageToUrlNode)
            .Subscribe(
                result => { /* Should not receive results */ },
                error => errors.Add(error));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(500);

        // Assert
        errors.Should().HaveCount(1);
        errors[0].Should().BeOfType<NotSupportedException>();
        errors[0].Message.Should().Contain("Unable to detect MIME type");
    }

    [Fact]
    public async Task ImageToUrlNode_WithErrorFallback_ShouldUseFallbackValue()
    {
        // Arrange
        var invalidStream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });
        var source = Observable.Return(invalidStream);
        var fallbackValue = ("data:image/png;base64,fallback", "image/png");

        // Act
        var result = await source
            .ThroughWithFallback(_imageToUrlNode, fallbackValue)
            .FirstAsync();

        // Assert
        result.Should().Be(fallbackValue);
    }

    [Fact]
    public async Task ImageToUrlNode_WithRetry_ShouldRetryOnFailure()
    {
        // Arrange
        var callCount = 0;
        var mockNode = new TestPipelineNode<Stream, (string, string)>(stream =>
        {
            callCount++;
            if (callCount <= 2)
                throw new InvalidOperationException("Temporary failure");
            return ("data:image/png;base64,success", "image/png");
        });

        var source = Observable.Return(TestImageHelper.GetReceiptImageStream());

        // Act
        var result = await source
            .ThroughWithRetry(mockNode, retryCount: 3)
            .FirstAsync();

        // Assert
        result.Should().Be(("data:image/png;base64,success", "image/png"));
        callCount.Should().Be(3); // Initial + 2 retries
    }

    [Fact]
    public async Task ImageToUrlNode_WithTimeout_ShouldTimeoutOnSlowProcessing()
    {
        // Arrange
        var slowNode = new TestPipelineNode<Stream, (string, string)>(async stream =>
        {
            await Task.Delay(200); // Longer than timeout
            return ("data:image/png;base64,result", "image/png");
        });

        var source = Observable.Return(TestImageHelper.GetReceiptImageStream());

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await source
                .ThroughWithTimeout(slowNode, TimeSpan.FromMilliseconds(50))
                .FirstAsync());
    }

    [Fact]
    public async Task ImageToUrlNode_WithProgress_ShouldReportProgress()
    {
        // Arrange
        using var progressSubject = new Subject<PipelineProgress<Stream>>();
        var progressEvents = new List<PipelineProgress<Stream>>();
        progressSubject.Subscribe(progressEvents.Add);

        var source = Observable.Return(TestImageHelper.GetReceiptImageStream());

        // Act
        var result = await source
            .ThroughWithProgress(_imageToUrlNode, progressSubject)
            .FirstAsync();

        // Assert
        result.url.Should().StartWith("data:");
        result.mimeType.Should().NotBeNullOrEmpty();

        progressEvents.Should().HaveCount(2);
        progressEvents[0].Status.Should().Be(PipelineProgressStatus.Started);
        progressEvents[1].Status.Should().Be(PipelineProgressStatus.Completed);
    }

    [Fact]
    public async Task ImageToUrlNode_WithBuffering_ShouldProcessInBatches()
    {
        // Arrange
        var imageStreams = Enumerable.Range(0, 7)
            .Select(_ => TestImageHelper.GetReceiptImageStream());

        var source = Observable.Interval(TimeSpan.FromMilliseconds(10))
            .Take(7)
            .Zip(imageStreams, (_, stream) => stream);

        var results = new List<(string url, string mimeType)>();

        // Act
        var subscription = source
            .ThroughBuffered(_imageToUrlNode, bufferSize: 3)
            .Subscribe(result => results.Add(result));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(1500);

        // Assert
        results.Should().HaveCount(7);
        results.Should().AllSatisfy(result =>
        {
            result.url.Should().StartWith("data:");
            result.mimeType.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task PipelineComposition_WithMultipleNodes_ShouldChainCorrectly()
    {
        // Arrange
        var transformNode = new TestPipelineNode<(string url, string mimeType), ProcessedImage>(
            input => new ProcessedImage 
            { 
                DataUrl = input.url, 
                MimeType = input.mimeType, 
                ProcessedAt = DateTime.UtcNow 
            });

        var validationNode = new TestPipelineNode<ProcessedImage, ValidatedImage>(
            input => new ValidatedImage 
            { 
                Image = input, 
                IsValid = input.DataUrl.StartsWith("data:") && !string.IsNullOrEmpty(input.MimeType) 
            });

        var source = Observable.Return(TestImageHelper.GetReceiptImageStream());

        // Act
        var result = await ReactivePipelineBuilder<Stream>
            .FromObservable(source)
            .Through(_imageToUrlNode)
            .Through(transformNode)
            .Through(validationNode)
            .Build()
            .FirstAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Image.DataUrl.Should().StartWith("data:");
        result.Image.MimeType.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReactivePipeline_WithRealWorldScenario_ShouldHandleComplexProcessing()
    {
        // Arrange - Simulate a real OCR processing pipeline
        var imageProcessor = _imageToUrlNode;
        var ocrNode = new TestPipelineNode<(string url, string mimeType), OcrResult>(
            async input =>
            {
                // Simulate OCR processing delay
                await Task.Delay(50);
                return new OcrResult 
                { 
                    Text = "Extracted text from image", 
                    Confidence = 0.95f,
                    ProcessingTime = TimeSpan.FromMilliseconds(50)
                };
            });

        var validationNode = new TestPipelineNode<OcrResult, ValidatedOcrResult>(
            result => new ValidatedOcrResult 
            { 
                Result = result, 
                IsValid = result.Confidence > 0.8f 
            });

        var imageStreams = Enumerable.Range(0, 5)
            .Select(_ => TestImageHelper.GetReceiptImageStream())
            .ToObservable();

        var results = new List<ValidatedOcrResult>();
        var errors = new List<Exception>();

        // Act
        var subscription = imageStreams
            .ThroughWithBackpressure(imageProcessor, maxConcurrency: 2)
            .ThroughWithRetry(ocrNode, retryCount: 2)
            .Through(validationNode)
            .Subscribe(
                result => results.Add(result),
                error => errors.Add(error));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(2000);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(result =>
        {
            result.IsValid.Should().BeTrue();
            result.Result.Text.Should().NotBeNullOrEmpty();
            result.Result.Confidence.Should().BeGreaterThan(0.8f);
        });
        errors.Should().BeEmpty();
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }

    // Helper classes for testing
    private class TestPipelineNode<TIn, TOut> : IPipelineNode<TIn, TOut>
    {
        private readonly Func<TIn, ValueTask<TOut>> _processor;

        public TestPipelineNode(Func<TIn, TOut> processor)
        {
            _processor = input => ValueTask.FromResult(processor(input));
        }

        public TestPipelineNode(Func<TIn, ValueTask<TOut>> processor)
        {
            _processor = processor;
        }

        public ValueTask<TOut> ProcessAsync(TIn input) => _processor(input);
    }

    private class ProcessedImage
    {
        public string DataUrl { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }

    private class ValidatedImage
    {
        public ProcessedImage Image { get; set; } = new();
        public bool IsValid { get; set; }
    }

    private class OcrResult
    {
        public string Text { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    private class ValidatedOcrResult
    {
        public OcrResult Result { get; set; } = new();
        public bool IsValid { get; set; }
    }
}