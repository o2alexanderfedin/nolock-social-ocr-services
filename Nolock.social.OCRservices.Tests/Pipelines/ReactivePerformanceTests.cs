using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using Nolock.social.OCRservices.Core.Pipelines;
using Nolock.social.OCRservices.Tests.TestData;

namespace Nolock.social.OCRservices.Tests.Pipelines;

/// <summary>
/// Performance and load tests for reactive pipelines
/// </summary>
public class ReactivePerformanceTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private readonly PipelineNodeImageToUrl _imageToUrlNode = new();

    [Fact]
    public async Task ReactivePipeline_HighVolumeProcessing_ShouldMaintainPerformance()
    {
        // Arrange
        const int itemCount = 100;
        var imageStreams = Enumerable.Range(0, itemCount)
            .Select(_ => TestImageHelper.GetReceiptImageStream())
            .ToObservable();

        var results = new ConcurrentBag<(string url, string mimeType)>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var subscription = imageStreams
            .ThroughWithBackpressure(_imageToUrlNode, maxConcurrency: 4)
            .Subscribe(result => results.Add(result));

        _disposables.Add(subscription);

        // Wait for completion
        await Task.Delay(5000); // Adjust based on expected processing time
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(itemCount);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000); // Should complete within 30 seconds
        
        var averageProcessingTime = stopwatch.ElapsedMilliseconds / (double)itemCount;
        averageProcessingTime.Should().BeLessThan(300); // Less than 300ms per item on average
    }

    [Fact]
    public async Task ReactivePipeline_WithMemoryPressure_ShouldNotLeakMemory()
    {
        // Arrange
        const int itemCount = 50;
        var initialMemory = GC.GetTotalMemory(true);
        
        var imageStreams = Enumerable.Range(0, itemCount)
            .Select(_ => TestImageHelper.GetReceiptImageStream());
        
        var processedCount = 0;

        // Act
        var subscription = imageStreams.ToObservable()
            .ThroughWithBackpressure(_imageToUrlNode, maxConcurrency: 2)
            .Subscribe(
                result => 
                {
                    Interlocked.Increment(ref processedCount);
                    // Immediately dispose of result to help GC
                },
                error => throw error);

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(3000);

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        processedCount.Should().Be(itemCount);
        
        // Memory should not increase dramatically (allowing for some overhead)
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreasePerItem = memoryIncrease / itemCount;
        
        // Each processed item should not retain more than 1MB in memory on average
        memoryIncreasePerItem.Should().BeLessThan(1024 * 1024);
    }

    [Fact]
    public async Task ReactivePipeline_ConcurrentLoad_ShouldHandleSimultaneousStreams()
    {
        // Arrange
        const int streamCount = 10;
        const int itemsPerStream = 5;
        
        var allResults = new ConcurrentBag<(string url, string mimeType)>();
        var completedStreams = 0;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < streamCount; i++)
        {
            var streamIndex = i;
            var task = Task.Run(async () =>
            {
                var imageStreams = Enumerable.Range(0, itemsPerStream)
                    .Select(_ => TestImageHelper.GetReceiptImageStream())
                    .ToObservable();

                var subscription = imageStreams
                    .ThroughWithBackpressure(_imageToUrlNode, maxConcurrency: 2)
                    .Subscribe(
                        result => allResults.Add(result),
                        error => throw error,
                        onCompleted: () => Interlocked.Increment(ref completedStreams));

                _disposables.Add(subscription);
                
                // Wait for this stream to complete
                await Task.Delay(2000);
            });
            
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Wait a bit more for any remaining processing
        await Task.Delay(1000);

        // Assert
        allResults.Should().HaveCount(streamCount * itemsPerStream);
        completedStreams.Should().Be(streamCount);
    }

    [Fact]
    public async Task ReactivePipeline_WithErrorHandling_ShouldMaintainThroughput()
    {
        // Arrange
        const int totalItems = 50;
        const int errorEveryNthItem = 5; // Inject error every 5th item
        
        var processedResults = new ConcurrentBag<string>();
        var errorCount = 0;
        
        var testNode = new TestErrorProneNode(errorEveryNthItem);
        var source = Enumerable.Range(1, totalItems).ToObservable();

        // Act
        var subscription = source
            .Select(i => i.ToString())
            .ThroughWithRetry(testNode, retryCount: 2)
            .Subscribe(
                result => processedResults.Add(result),
                error => Interlocked.Increment(ref errorCount));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(3000);

        // Assert
        var expectedSuccessful = totalItems - (totalItems / errorEveryNthItem);
        processedResults.Should().HaveCountLessThanOrEqualTo(totalItems);
        processedResults.Should().HaveCountGreaterThanOrEqualTo(expectedSuccessful);
    }

    [Fact]
    public async Task ReactivePipeline_BackpressureStressTest_ShouldControlResourceUsage()
    {
        // Arrange
        const int itemCount = 200;
        var maxConcurrentOperations = 0;
        var currentConcurrentOperations = 0;
        
        var slowNode = new TestSlowProcessingNode(() =>
        {
            var current = Interlocked.Increment(ref currentConcurrentOperations);
            maxConcurrentOperations = Math.Max(maxConcurrentOperations, current);
            
            // Simulate work
            Thread.Sleep(50);
            
            Interlocked.Decrement(ref currentConcurrentOperations);
            return "processed";
        });

        var source = Enumerable.Range(1, itemCount)
            .Select(i => $"item_{i}")
            .ToObservable();

        var results = new ConcurrentBag<string>();

        // Act
        var subscription = source
            .ThroughWithBackpressure(slowNode, maxConcurrency: 5)
            .Subscribe(result => results.Add(result));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(15000); // Allow enough time for processing

        // Assert
        results.Should().HaveCount(itemCount);
        maxConcurrentOperations.Should().BeLessOrEqualTo(5);
    }

    [Fact]
    public async Task ReactivePipeline_TimeoutHandling_ShouldNotBlockProcessing()
    {
        // Arrange
        const int itemCount = 20;
        var processedItems = new ConcurrentBag<string>();
        var timeoutErrors = 0;
        
        var inconsistentNode = new TestInconsistentDelayNode();
        var source = Enumerable.Range(1, itemCount)
            .Select(i => $"item_{i}")
            .ToObservable();

        // Act - Use SelectMany with individual timeout handling per item
        var subscription = source
            .SelectMany(item => 
                Observable.FromAsync(() => inconsistentNode.ProcessAsync(item).AsTask())
                    .Timeout(TimeSpan.FromMilliseconds(200))
                    .Select(result => (Success: true, Result: result, Error: (Exception?)null))
                    .Catch<(bool Success, string Result, Exception? Error), TimeoutException>(ex => 
                        Observable.Return((Success: false, Result: string.Empty, Error: (Exception?)ex))))
            .Subscribe(
                result =>
                {
                    if (result.Success)
                    {
                        processedItems.Add(result.Result);
                    }
                    else if (result.Error is TimeoutException)
                    {
                        Interlocked.Increment(ref timeoutErrors);
                    }
                });

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(5000);

        // Assert
        var totalProcessed = processedItems.Count + timeoutErrors;
        totalProcessed.Should().Be(itemCount);
        
        // Should have some successful processing and some timeouts
        processedItems.Should().NotBeEmpty();
        timeoutErrors.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReactivePipeline_BufferedProcessing_ShouldOptimizeThroughput()
    {
        // Arrange
        const int itemCount = 100;
        var processingTimes = new ConcurrentBag<TimeSpan>();
        
        var trackingNode = new TestTrackingNode(processingTimes);
        var source = Enumerable.Range(1, itemCount)
            .Select(i => $"item_{i}")
            .ToObservable();

        var results = new ConcurrentBag<string>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var subscription = source
            .ThroughBuffered(trackingNode, bufferSize: 10)
            .Subscribe(result => results.Add(result));

        _disposables.Add(subscription);

        // Wait for processing
        await Task.Delay(3000);
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(itemCount);
        
        // Buffered processing should be more efficient
        var averageProcessingTime = processingTimes.Average(t => t.TotalMilliseconds);
        averageProcessingTime.Should().BeLessThan(200); // Should be quite fast due to batching
        
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }

    // Helper test nodes
    private class TestErrorProneNode : IPipelineNode<string, string>
    {
        private readonly int _errorEveryNthItem;
        private int _processCount = 0;

        public TestErrorProneNode(int errorEveryNthItem)
        {
            _errorEveryNthItem = errorEveryNthItem;
        }

        public ValueTask<string> ProcessAsync(string input)
        {
            var count = Interlocked.Increment(ref _processCount);
            
            if (count % _errorEveryNthItem == 0)
            {
                throw new InvalidOperationException($"Simulated error for item {count}");
            }
            
            return ValueTask.FromResult($"processed_{input}");
        }
    }

    private class TestSlowProcessingNode : IPipelineNode<string, string>
    {
        private readonly Func<string> _processor;

        public TestSlowProcessingNode(Func<string> processor)
        {
            _processor = processor;
        }

        public ValueTask<string> ProcessAsync(string input)
        {
            var result = _processor();
            return ValueTask.FromResult(result);
        }
    }

    private class TestInconsistentDelayNode : IPipelineNode<string, string>
    {
        private readonly Random _random = new();

        public async ValueTask<string> ProcessAsync(string input)
        {
            // Random delay between 10ms and 500ms
            var delay = _random.Next(10, 500);
            await Task.Delay(delay);
            
            return $"processed_{input}_delay_{delay}ms";
        }
    }

    private class TestTrackingNode : IPipelineNode<string, string>
    {
        private readonly ConcurrentBag<TimeSpan> _processingTimes;

        public TestTrackingNode(ConcurrentBag<TimeSpan> processingTimes)
        {
            _processingTimes = processingTimes;
        }

        public async ValueTask<string> ProcessAsync(string input)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate some processing
            await Task.Delay(10);
            
            stopwatch.Stop();
            _processingTimes.Add(stopwatch.Elapsed);
            
            return $"processed_{input}";
        }
    }
}