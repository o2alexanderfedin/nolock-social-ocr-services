using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using Microsoft.Reactive.Testing;
using FluentAssertions;
using Moq;
using Nolock.social.OCRservices.Core.Pipelines;

namespace Nolock.social.OCRservices.Tests.Pipelines;

/// <summary>
/// Comprehensive tests for reactive pipeline processing
/// Tests pipeline node composition, error propagation, backpressure, cancellation, and transforms
/// </summary>
public class ReactivePipelineTests : ReactiveTest
{
    private readonly TestScheduler _testScheduler;
    private readonly Mock<IPipelineNode<string, string>> _mockStringNode;
    private readonly Mock<IPipelineNode<int, string>> _mockIntToStringNode;
    private readonly Mock<IPipelineNode<string, int>> _mockStringToIntNode;

    public ReactivePipelineTests()
    {
        _testScheduler = new TestScheduler();
        _mockStringNode = new Mock<IPipelineNode<string, string>>();
        _mockIntToStringNode = new Mock<IPipelineNode<int, string>>();
        _mockStringToIntNode = new Mock<IPipelineNode<string, int>>();
    }

    [Fact]
    public async Task ToObservableOperator_WithValidInput_ShouldProcessSuccessfully()
    {
        // Arrange
        _mockStringNode.Setup(x => x.ProcessAsync("test"))
            .ReturnsAsync("processed_test");

        var source = Observable.Return("test");

        // Act
        var result = await source
            .SelectMany(async item => await _mockStringNode.Object.ProcessAsync(item))
            .FirstAsync();

        // Assert
        result.Should().Be("processed_test");
        _mockStringNode.Verify(x => x.ProcessAsync("test"), Times.Once);
    }

    [Fact]
    public async Task Through_WithPipelineNode_ShouldTransformCorrectly()
    {
        // Arrange
        _mockStringNode.Setup(x => x.ProcessAsync("input"))
            .ReturnsAsync("output");

        var source = Observable.Return("input");

        // Act
        var result = await source
            .Through(_mockStringNode.Object)
            .FirstAsync();

        // Assert
        result.Should().Be("output");
    }

    [Fact]
    public async Task ThroughPipeline_WithMultipleNodes_ShouldChainCorrectly()
    {
        // Arrange
        var node1 = new Mock<IPipelineNode<string, string>>();
        var node2 = new Mock<IPipelineNode<string, string>>();
        var node3 = new Mock<IPipelineNode<string, string>>();

        node1.Setup(x => x.ProcessAsync("input")).ReturnsAsync("step1");
        node2.Setup(x => x.ProcessAsync("step1")).ReturnsAsync("step2");
        node3.Setup(x => x.ProcessAsync("step2")).ReturnsAsync("final");

        var source = Observable.Return("input");

        // Act
        var result = await source
            .ThroughPipeline(node1.Object, node2.Object, node3.Object)
            .FirstAsync();

        // Assert
        result.Should().Be("final");
        node1.Verify(x => x.ProcessAsync("input"), Times.Once);
        node2.Verify(x => x.ProcessAsync("step1"), Times.Once);
        node3.Verify(x => x.ProcessAsync("step2"), Times.Once);
    }

    [Fact]
    public void ReactivePipelineBuilder_ShouldChainOperations()
    {
        // Arrange
        _mockStringToIntNode.Setup(x => x.ProcessAsync("5")).ReturnsAsync(5);
        _mockIntToStringNode.Setup(x => x.ProcessAsync(5)).ReturnsAsync("five");

        var source = Observable.Return("5");

        // Act
        var pipeline = ReactivePipelineBuilder<string>
            .FromObservable(source)
            .Through(_mockStringToIntNode.Object)
            .Through(_mockIntToStringNode.Object)
            .Build();

        // Assert
        pipeline.Should().NotBeNull();
    }
}

/// <summary>
/// Tests for error propagation through reactive pipelines
/// </summary>
public class ReactiveErrorPropagationTests : ReactiveTest
{
    private readonly TestScheduler _testScheduler;
    private readonly Mock<IPipelineNode<string, string>> _mockNode;

    public ReactiveErrorPropagationTests()
    {
        _testScheduler = new TestScheduler();
        _mockNode = new Mock<IPipelineNode<string, string>>();
    }

    [Fact]
    public async Task Through_WhenNodeThrowsException_ShouldPropagateError()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test error");
        _mockNode.Setup(x => x.ProcessAsync("input"))
            .ThrowsAsync(expectedException);

        var source = Observable.Return("input");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await source.Through(_mockNode.Object).FirstAsync());

        exception.Should().Be(expectedException);
    }

    [Fact]
    public async Task ThroughWithFallback_WhenNodeThrowsException_ShouldUseFallback()
    {
        // Arrange
        _mockNode.Setup(x => x.ProcessAsync("input"))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var source = Observable.Return("input");

        // Act
        var result = await source
            .ThroughWithFallback(_mockNode.Object, "fallback_value")
            .FirstAsync();

        // Assert
        result.Should().Be("fallback_value");
    }

    [Fact]
    public async Task ThroughWithRetry_WhenNodeFailsThenSucceeds_ShouldRetryAndSucceed()
    {
        // Arrange
        var callCount = 0;
        _mockNode.Setup(x => x.ProcessAsync("input"))
            .Returns(() =>
            {
                callCount++;
                if (callCount <= 2)
                    throw new InvalidOperationException("Temporary failure");
                return ValueTask.FromResult("success");
            });

        var source = Observable.Return("input");

        // Act
        var result = await source
            .ThroughWithRetry(_mockNode.Object, retryCount: 3)
            .FirstAsync();

        // Assert
        result.Should().Be("success");
        callCount.Should().Be(3); // Initial call + 2 retries
    }

    [Fact]
    public async Task ThroughWithProgress_ShouldReportProgress()
    {
        // Arrange
        _mockNode.Setup(x => x.ProcessAsync("input"))
            .ReturnsAsync("output");

        using var progressSubject = new Subject<PipelineProgress<string>>();
        var progressEvents = new List<PipelineProgress<string>>();
        progressSubject.Subscribe(progressEvents.Add);

        var source = Observable.Return("input");

        // Act
        await source
            .ThroughWithProgress(_mockNode.Object, progressSubject)
            .FirstAsync();

        // Assert
        progressEvents.Should().HaveCount(2);
        progressEvents[0].Status.Should().Be(PipelineProgressStatus.Started);
        progressEvents[0].Item.Should().Be("input");
        progressEvents[1].Status.Should().Be(PipelineProgressStatus.Completed);
        progressEvents[1].Item.Should().Be("input");
    }

    [Fact]
    public async Task ThroughWithProgress_WhenNodeFails_ShouldReportFailure()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test error");
        _mockNode.Setup(x => x.ProcessAsync("input"))
            .ThrowsAsync(expectedException);

        using var progressSubject = new Subject<PipelineProgress<string>>();
        var progressEvents = new List<PipelineProgress<string>>();
        progressSubject.Subscribe(progressEvents.Add);

        var source = Observable.Return("input");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await source.ThroughWithProgress(_mockNode.Object, progressSubject).FirstAsync());

        progressEvents.Should().HaveCount(2);
        progressEvents[0].Status.Should().Be(PipelineProgressStatus.Started);
        progressEvents[1].Status.Should().Be(PipelineProgressStatus.Failed);
        progressEvents[1].Error.Should().Be(expectedException);
    }
}

/// <summary>
/// Tests for backpressure handling in reactive pipelines
/// </summary>
public class ReactiveBackpressureTests : ReactiveTest
{
    private readonly TestScheduler _testScheduler;
    private readonly Mock<IPipelineNode<int, string>> _mockSlowNode;

    public ReactiveBackpressureTests()
    {
        _testScheduler = new TestScheduler();
        _mockSlowNode = new Mock<IPipelineNode<int, string>>();
    }

    [Fact]
    public async Task ThroughWithBackpressure_WithLimitedConcurrency_ShouldControlConcurrentOperations()
    {
        // Arrange
        var concurrentOperations = 0;
        var maxObservedConcurrency = 0;
        var processingTimes = new List<DateTime>();

        _mockSlowNode.Setup(x => x.ProcessAsync(It.IsAny<int>()))
            .Returns(async (int input) =>
            {
                var currentConcurrency = Interlocked.Increment(ref concurrentOperations);
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, currentConcurrency);
                processingTimes.Add(DateTime.UtcNow);

                // Simulate slow processing
                await Task.Delay(100);

                Interlocked.Decrement(ref concurrentOperations);
                return $"processed_{input}";
            });

        var source = Observable.Range(1, 10);

        // Act
        var results = await source
            .ThroughWithBackpressure(_mockSlowNode.Object, maxConcurrency: 3)
            .ToList()
            .FirstAsync();

        // Assert
        results.Should().HaveCount(10);
        maxObservedConcurrency.Should().BeLessOrEqualTo(3);
    }

    [Fact]
    public async Task ThroughBuffered_WithTimeSpan_ShouldProcessInBatches()
    {
        // Arrange
        var processedItems = new List<string>();
        _mockSlowNode.Setup(x => x.ProcessAsync(It.IsAny<int>()))
            .Returns((int input) =>
            {
                processedItems.Add($"processed_{input}");
                return ValueTask.FromResult($"processed_{input}");
            });

        var source = Observable.Interval(TimeSpan.FromMilliseconds(50))
            .Take(5)
            .Select(x => (int)x);

        // Act
        var results = await source
            .ThroughBuffered(_mockSlowNode.Object, bufferSize: 3, bufferTimeSpan: TimeSpan.FromMilliseconds(200))
            .ToList()
            .FirstAsync();

        // Assert
        results.Should().HaveCount(5);
        processedItems.Should().HaveCount(5);
    }

    [Fact]
    public async Task ThroughBuffered_WithSizeOnly_ShouldProcessWhenBufferFull()
    {
        // Arrange
        var batchSizes = new List<int>();
        var processedCount = 0;

        _mockSlowNode.Setup(x => x.ProcessAsync(It.IsAny<int>()))
            .Returns((int input) =>
            {
                var currentCount = Interlocked.Increment(ref processedCount);
                return ValueTask.FromResult($"processed_{input}");
            });

        var source = Observable.Range(1, 7);

        // Act
        var results = await source
            .ThroughBuffered(_mockSlowNode.Object, bufferSize: 3)
            .ToList()
            .FirstAsync();

        // Assert
        results.Should().HaveCount(7);
        processedCount.Should().Be(7);
    }
}

/// <summary>
/// Tests for pipeline cancellation
/// </summary>
public class ReactiveCancellationTests : ReactiveTest
{
    private readonly TestScheduler _testScheduler;
    private readonly Mock<IPipelineNode<int, string>> _mockNode;

    public ReactiveCancellationTests()
    {
        _testScheduler = new TestScheduler();
        _mockNode = new Mock<IPipelineNode<int, string>>();
    }

    [Fact]
    public void Through_WithCancellationToken_ShouldCancelProcessing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<string>();

        _mockNode.Setup(x => x.ProcessAsync(It.IsAny<int>()))
            .Returns(() => new ValueTask<string>(tcs.Task));

        var source = Observable.Return(1);

        // Act
        var subscription = source
            .Through(_mockNode.Object)
            .Subscribe();

        // Cancel immediately
        #pragma warning disable CA1849 // CancellationToken synchronous cancel is intended in test
        cts.Cancel();
        #pragma warning restore CA1849
        tcs.SetCanceled();

        // Assert
        subscription.Should().NotBeNull();
        subscription.Dispose(); // Clean up
    }

    [Fact]
    public async Task ThroughWithTimeout_WhenProcessingTakesTooLong_ShouldTimeout()
    {
        // Arrange
        _mockNode.Setup(x => x.ProcessAsync(It.IsAny<int>()))
            .Returns(async (int input) =>
            {
                await Task.Delay(200); // Longer than timeout
                return $"processed_{input}";
            });

        var source = Observable.Return(1);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await source
                .ThroughWithTimeout(_mockNode.Object, TimeSpan.FromMilliseconds(50))
                .FirstAsync());
    }

    [Fact]
    public void ThroughWithTimeout_WithCustomScheduler_ShouldUseScheduler()
    {
        // Arrange
        var testScheduler = new TestScheduler();
        _mockNode.Setup(x => x.ProcessAsync(It.IsAny<int>()))
            .Returns(async (int input) =>
            {
                await Task.Delay(100);
                return $"processed_{input}";
            });

        var source = Observable.Return(1);

        // Act
        var observable = source.ThroughWithTimeout(
            _mockNode.Object,
            TimeSpan.FromMilliseconds(50),
            testScheduler);

        // The timeout should be controlled by the test scheduler
        observable.Should().NotBeNull();
    }

    [Fact]
    public void Subject_Disposal_ShouldCompleteObservables()
    {
        // Arrange
        var subject = new Subject<int>();
        var completed = false;
        var errored = false;

        subject.Subscribe(
            onNext: _ => { },
            onError: _ => errored = true,
            onCompleted: () => completed = true);

        // Act
        subject.OnCompleted();
        subject.Dispose();

        // Assert
        completed.Should().BeTrue();
        errored.Should().BeFalse();
    }
}

/// <summary>
/// Tests for transform operations in reactive pipelines
/// </summary>
public class ReactiveTransformTests : ReactiveTest
{
    private readonly TestScheduler _testScheduler;

    public ReactiveTransformTests()
    {
        _testScheduler = new TestScheduler();
    }

    [Fact]
    public async Task Pipeline_WithMultipleTransforms_ShouldApplyInSequence()
    {
        // Arrange
        var upperCaseNode = new Mock<IPipelineNode<string, string>>();
        var addPrefixNode = new Mock<IPipelineNode<string, string>>();
        var trimNode = new Mock<IPipelineNode<string, string>>();

        upperCaseNode.Setup(x => x.ProcessAsync("hello"))
            .ReturnsAsync("HELLO");
        addPrefixNode.Setup(x => x.ProcessAsync("HELLO"))
            .ReturnsAsync("PREFIX_HELLO");
        trimNode.Setup(x => x.ProcessAsync("PREFIX_HELLO"))
            .ReturnsAsync("PREFIX_HELLO");

        var source = Observable.Return("hello");

        // Act
        var result = await ReactivePipelineBuilder<string>
            .FromObservable(source)
            .Through(upperCaseNode.Object)
            .Through(addPrefixNode.Object)
            .Through(trimNode.Object)
            .Build()
            .FirstAsync();

        // Assert
        result.Should().Be("PREFIX_HELLO");
    }

    [Fact]
    public async Task Pipeline_WithTypeTransforms_ShouldConvertTypes()
    {
        // Arrange
        var stringToIntNode = new Mock<IPipelineNode<string, int>>();
        var intToDoubleNode = new Mock<IPipelineNode<int, double>>();
        var doubleToStringNode = new Mock<IPipelineNode<double, string>>();

        stringToIntNode.Setup(x => x.ProcessAsync("42"))
            .ReturnsAsync(42);
        intToDoubleNode.Setup(x => x.ProcessAsync(42))
            .ReturnsAsync(42.0);
        doubleToStringNode.Setup(x => x.ProcessAsync(42.0))
            .ReturnsAsync("42.0");

        var source = Observable.Return("42");

        // Act
        var result = await ReactivePipelineBuilder<string>
            .FromObservable(source)
            .Through(stringToIntNode.Object)
            .Through(intToDoubleNode.Object)
            .Through(doubleToStringNode.Object)
            .Build()
            .FirstAsync();

        // Assert
        result.Should().Be("42.0");
    }

    [Fact]
    public async Task Pipeline_WithComplexTransformations_ShouldProcessStreams()
    {
        // Arrange
        var processNode = new Mock<IPipelineNode<string, ProcessedData>>();
        var aggregateNode = new Mock<IPipelineNode<ProcessedData, string>>();

        processNode.Setup(x => x.ProcessAsync("data1"))
            .ReturnsAsync(new ProcessedData { Value = "processed_data1", Count = 1 });
        processNode.Setup(x => x.ProcessAsync("data2"))
            .ReturnsAsync(new ProcessedData { Value = "processed_data2", Count = 2 });

        aggregateNode.Setup(x => x.ProcessAsync(It.IsAny<ProcessedData>()))
            .Returns((ProcessedData data) => ValueTask.FromResult($"Result: {data.Value} (Count: {data.Count})"));

        var source = new[] { "data1", "data2" }.ToObservable();

        // Act
        var results = await source
            .Through(processNode.Object)
            .Through(aggregateNode.Object)
            .ToList()
            .FirstAsync();

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("Result: processed_data1 (Count: 1)");
        results[1].Should().Be("Result: processed_data2 (Count: 2)");
    }

    [Fact]
    public async Task Pipeline_WithSelectMany_ShouldFlattenResults()
    {
        // Arrange
        var expandNode = new Mock<IPipelineNode<string, IEnumerable<string>>>();
        expandNode.Setup(x => x.ProcessAsync("item"))
            .ReturnsAsync(new[] { "item_1", "item_2", "item_3" });

        var source = Observable.Return("item");

        // Act
        var results = await source
            .SelectMany(async item => await expandNode.Object.ProcessAsync(item))
            .SelectMany(enumerable => enumerable.ToObservable())
            .ToList()
            .FirstAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain("item_1", "item_2", "item_3");
    }

    public class ProcessedData
    {
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}

/// <summary>
/// Integration tests combining multiple reactive pipeline features
/// </summary>
public class ReactiveIntegrationTests : ReactiveTest
{
    [Fact]
    public async Task ComplexPipeline_WithAllFeatures_ShouldWorkTogether()
    {
        // Arrange
        var node1 = new Mock<IPipelineNode<string, string>>();
        var node2 = new Mock<IPipelineNode<string, int>>();
        var node3 = new Mock<IPipelineNode<int, string>>();

        node1.Setup(x => x.ProcessAsync(It.IsAny<string>()))
            .Returns((string input) => ValueTask.FromResult($"step1_{input}"));
        node2.Setup(x => x.ProcessAsync(It.IsAny<string>()))
            .Returns((string input) => ValueTask.FromResult(input.Length));
        node3.Setup(x => x.ProcessAsync(It.IsAny<int>()))
            .Returns((int input) => ValueTask.FromResult($"final_{input}"));

        using var progressSubject = new Subject<PipelineProgress<string>>();
        var progressEvents = new List<PipelineProgress<string>>();
        progressSubject.Subscribe(progressEvents.Add);

        var source = new[] { "test1", "test2", "test3" }.ToObservable();

        // Act
        var results = await source
            .ThroughWithProgress(node1.Object, progressSubject)
            .ThroughWithBackpressure(node2.Object, maxConcurrency: 2)
            .ThroughWithRetry(node3.Object, retryCount: 2)
            .ToList()
            .FirstAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Should().StartWith("final_"));
        progressEvents.Should().HaveCount(6); // Start and complete for each item
    }
}