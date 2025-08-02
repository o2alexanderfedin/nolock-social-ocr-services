using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using Moq;
using Nolock.social.OCRservices.Core.Pipelines;
using Nolock.social.OCRservices.Tests.TestData;

namespace Nolock.social.OCRservices.Tests.Pipelines;

/// <summary>
/// Simplified reactive pipeline tests that compile and run correctly
/// Tests core reactive pipeline functionality with proper error handling
/// </summary>
public class SimpleReactivePipelineTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private readonly PipelineNodeImageToUrl _imageToUrlNode = new();

    [Fact]
    public async Task ImageToUrlNode_WithReactiveExtensions_ShouldProcessSuccessfully()
    {
        // Arrange
        var imageStream = TestImageHelper.GetReceiptImageStream();
        var source = Observable.Return(imageStream);
        var results = new List<(string url, string mimeType)>();

        // Act
        var subscription = source
            .SelectMany(async stream => await _imageToUrlNode.ProcessAsync(stream))
            .Subscribe(result => results.Add(result));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(500);

        // Assert
        results.Should().HaveCount(1);
        results[0].url.Should().StartWith("data:");
        results[0].mimeType.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReactivePipeline_WithBackpressure_ShouldControlConcurrency()
    {
        // Arrange
        var imageStreams = Enumerable.Range(0, 5)
            .Select(_ => TestImageHelper.GetReceiptImageStream())
            .ToObservable();

        var results = new List<(string url, string mimeType)>();

        // Act
        var subscription = imageStreams
            .Select(stream => Observable.FromAsync(() => _imageToUrlNode.ProcessAsync(stream).AsTask()))
            .Merge(maxConcurrent: 2)
            .Subscribe(result => results.Add(result));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(2000);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(result =>
        {
            result.url.Should().StartWith("data:");
            result.mimeType.Should().BeOneOf("image/jpeg", "image/png", "image/gif", "image/bmp");
        });
    }

    [Fact]
    public async Task ReactivePipeline_WithErrorHandling_ShouldHandleFailures()
    {
        // Arrange
        var invalidStream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 }); // Invalid image data
        var source = Observable.Return(invalidStream);
        var errors = new List<Exception>();
        var results = new List<(string url, string mimeType)>();

        // Act
        var subscription = source
            .SelectMany(async stream => await _imageToUrlNode.ProcessAsync(stream))
            .Subscribe(
                result => results.Add(result),
                error => errors.Add(error));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(500);

        // Assert
        results.Should().BeEmpty();
        errors.Should().HaveCount(1);
        errors[0].Should().BeOfType<NotSupportedException>();
    }

    [Fact]
    public async Task ReactivePipeline_WithRetry_ShouldRetryOnFailure()
    {
        // Arrange
        var callCount = 0;
        var mockNode = new Mock<IPipelineNode<string, string>>();
        mockNode.Setup(x => x.ProcessAsync(It.IsAny<string>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount <= 2)
                    throw new InvalidOperationException("Temporary failure");
                return ValueTask.FromResult("success");
            });

        var source = Observable.Return("test");

        // Act
        var result = await source
            .SelectMany(async input =>
            {
                for (int i = 0; i <= 3; i++) // 3 retries
                {
                    try
                    {
                        return await mockNode.Object.ProcessAsync(input);
                    }
                    catch (InvalidOperationException) when (i < 3)
                    {
                        // Continue to next iteration for retry
                    }
                }
                throw new InvalidOperationException("Max retries exceeded");
            })
            .FirstAsync();

        // Assert
        result.Should().Be("success");
        callCount.Should().Be(3); // Initial + 2 retries
    }

    [Fact] 
    public async Task ReactivePipeline_WithTimeout_ShouldTimeoutSlowOperations()
    {
        // Arrange
        var slowNode = new Mock<IPipelineNode<string, string>>();
        slowNode.Setup(x => x.ProcessAsync(It.IsAny<string>()))
            .Returns(async (string input) =>
            {
                await Task.Delay(200); // Longer than timeout
                return "result";
            });

        var source = Observable.Return("test");

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await source
                .SelectMany(async input => await slowNode.Object.ProcessAsync(input))
                .Timeout(TimeSpan.FromMilliseconds(50))
                .FirstAsync());
    }

    [Fact]
    public async Task ReactivePipeline_WithProgress_ShouldTrackProgress()
    {
        // Arrange
        using var progressSubject = new Subject<string>();
        var progressEvents = new List<string>();
        progressSubject.Subscribe(progressEvents.Add);

        var imageStream = TestImageHelper.GetReceiptImageStream();
        var source = Observable.Return(imageStream);

        // Act
        var result = await source
            .Do(_ => progressSubject.OnNext("Started"))
            .SelectMany(async stream => await _imageToUrlNode.ProcessAsync(stream))
            .Do(_ => progressSubject.OnNext("Completed"))
            .FirstAsync();

        // Assert
        result.url.Should().StartWith("data:");
        progressEvents.Should().HaveCount(2);
        progressEvents[0].Should().Be("Started");
        progressEvents[1].Should().Be("Completed");
    }

    [Fact]
    public async Task ReactivePipeline_ChainedOperations_ShouldProcessSequentially()
    {
        // Arrange
        var transformNode = new Mock<IPipelineNode<(string url, string mimeType), string>>();
        transformNode.Setup(x => x.ProcessAsync(It.IsAny<(string, string)>()))
            .Returns((ValueTuple<string, string> input) => 
                ValueTask.FromResult($"Processed: {input.Item2}"));

        var imageStream = TestImageHelper.GetReceiptImageStream();
        var source = Observable.Return(imageStream);

        // Act
        var result = await source
            .SelectMany(async stream => await _imageToUrlNode.ProcessAsync(stream))
            .SelectMany(async dataUrl => await transformNode.Object.ProcessAsync(dataUrl))
            .FirstAsync();

        // Assert
        result.Should().StartWith("Processed:");
    }

    [Fact]
    public async Task ReactivePipeline_BufferedProcessing_ShouldProcessInBatches()
    {
        // Arrange
        var imageStreams = Enumerable.Range(0, 7)
            .Select(_ => TestImageHelper.GetReceiptImageStream())
            .ToObservable();

        var results = new List<(string url, string mimeType)>();

        // Act
        var subscription = imageStreams
            .Buffer(3) // Process in batches of 3
            .SelectMany(batch => batch.ToObservable())
            .SelectMany(async stream => await _imageToUrlNode.ProcessAsync(stream))
            .Subscribe(result => results.Add(result));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(2000);

        // Assert
        results.Should().HaveCount(7);
        results.Should().AllSatisfy(result =>
        {
            result.url.Should().StartWith("data:");
            result.mimeType.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task ReactivePipeline_ErrorRecovery_ShouldUseFallback()
    {
        // Arrange
        var invalidStream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });
        var fallbackValue = ("data:image/png;base64,fallback", "image/png");
        var source = Observable.Return(invalidStream);

        // Act
        var result = await source
            .SelectMany(async stream =>
            {
                try
                {
                    return await _imageToUrlNode.ProcessAsync(stream);
                }
                catch (NotSupportedException)
                {
                    return fallbackValue;
                }
            })
            .FirstAsync();

        // Assert
        result.Should().Be(fallbackValue);
    }

    [Fact]
    public async Task ReactivePipeline_ConcurrentProcessing_ShouldHandleLoad()
    {
        // Arrange
        const int itemCount = 10;
        var imageStreams = Enumerable.Range(0, itemCount)
            .Select(_ => TestImageHelper.GetReceiptImageStream())
            .ToObservable();

        var results = new List<(string url, string mimeType)>();

        // Act
        var subscription = imageStreams
            .Select(stream => Observable.FromAsync(() => _imageToUrlNode.ProcessAsync(stream).AsTask()))
            .Merge(maxConcurrent: 3)
            .Subscribe(result => results.Add(result));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(3000);

        // Assert
        results.Should().HaveCount(itemCount);
        results.Should().AllSatisfy(result =>
        {
            result.url.Should().StartWith("data:");
            result.mimeType.Should().NotBeNullOrEmpty();
        });
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }
}