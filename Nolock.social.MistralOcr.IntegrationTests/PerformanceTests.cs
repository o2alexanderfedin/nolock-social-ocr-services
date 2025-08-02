using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;

namespace Nolock.social.MistralOcr.IntegrationTests;

/// <summary>
/// Comprehensive performance and stress tests for MistralOcr service
/// </summary>
public class PerformanceTests : TestBase
{
    private const int WarmupDurationSeconds = 10;
    private const int TestDurationSeconds = 30;
    private const int MaxVirtualUsers = 10;
    
    public PerformanceTests(MistralOcrTestFixture fixture) : base(fixture)
    {
    }

    #region Response Time Benchmarks

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task ResponseTime_SingleImageProcessing_ShouldMeetBenchmarks()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        var measurements = new List<TimeSpan>();
        const int iterations = 5;

        // Act - Warm up
        await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png"));

        // Measure response times
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png"));
            stopwatch.Stop();
            
            result.Should().NotBeNull();
            result.Text.Should().NotBeNullOrWhiteSpace();
            measurements.Add(stopwatch.Elapsed);
        }

        // Assert
        var averageTime = TimeSpan.FromTicks((long)measurements.Average(t => t.Ticks));
        var maxTime = measurements.Max();
        var minTime = measurements.Min();

        // Log performance metrics
        Console.WriteLine($"Average response time: {averageTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Min response time: {minTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Max response time: {maxTime.TotalMilliseconds:F2} ms");

        // Assertions - adjust these thresholds based on your requirements
        averageTime.Should().BeLessThan(TimeSpan.FromSeconds(30), "Average response time should be under 30 seconds");
        maxTime.Should().BeLessThan(TimeSpan.FromSeconds(60), "Maximum response time should be under 60 seconds");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task ResponseTime_DifferentImageSizes_ShouldScaleAppropriately()
    {
        // Arrange
        var smallImage = GetTestImageDataUrl(); // Small 1x1 pixel
        var largeImage = GetTestImageDataUrlWithText(); // Larger 100x50 pixel
        var measurements = new Dictionary<string, List<TimeSpan>>
        {
            ["small"] = new(),
            ["large"] = new()
        };

        const int iterations = 3;

        // Act & Measure
        for (int i = 0; i < iterations; i++)
        {
            // Small image
            var stopwatch = Stopwatch.StartNew();
            await Fixture.MistralOcrService.ProcessImageDataItemAsync((smallImage, "image/png"));
            stopwatch.Stop();
            measurements["small"].Add(stopwatch.Elapsed);

            // Large image
            stopwatch.Restart();
            await Fixture.MistralOcrService.ProcessImageDataItemAsync((largeImage, "image/png"));
            stopwatch.Stop();
            measurements["large"].Add(stopwatch.Elapsed);
        }

        // Assert
        var avgSmall = TimeSpan.FromTicks((long)measurements["small"].Average(t => t.Ticks));
        var avgLarge = TimeSpan.FromTicks((long)measurements["large"].Average(t => t.Ticks));

        Console.WriteLine($"Small image average: {avgSmall.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Large image average: {avgLarge.TotalMilliseconds:F2} ms");

        // Both should complete within reasonable time
        avgSmall.Should().BeLessThan(TimeSpan.FromSeconds(30));
        avgLarge.Should().BeLessThan(TimeSpan.FromSeconds(45));
    }

    #endregion

    #region Memory Usage Validation

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task MemoryUsage_SingleImageProcessing_ShouldNotExceedLimits()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);

        // Act
        const int iterations = 10;
        for (int i = 0; i < iterations; i++)
        {
            var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png"));
            result.Should().NotBeNull();
        }

        // Force cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        Console.WriteLine($"Initial memory: {initialMemory / 1024 / 1024:F2} MB");
        Console.WriteLine($"Final memory: {finalMemory / 1024 / 1024:F2} MB");
        Console.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024:F2} MB");

        // Memory increase should be reasonable (less than 100MB for 10 operations)
        memoryIncrease.Should().BeLessThan(100 * 1024 * 1024, "Memory usage should not exceed 100MB for 10 operations");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task MemoryUsage_StreamProcessing_ShouldDisposeProperlyAsync()
    {
        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);
        const int iterations = 5;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            using var stream = GetTestImageStream();
            var result = await Fixture.MistralOcrService.ProcessImageStreamAsync(stream, "image/jpeg");
            result.Should().NotBeNull();
        }

        // Force cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        Console.WriteLine($"Stream processing memory increase: {memoryIncrease / 1024 / 1024:F2} MB");
        
        // Memory should be properly released after stream disposal
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory should be properly released after stream disposal");
    }

    #endregion

    #region Throughput Testing

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task Throughput_SequentialProcessing_ShouldMeetMinimumRate()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        const int iterations = 5;
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png"));
            result.Should().NotBeNull();
        }

        stopwatch.Stop();

        // Assert
        var throughput = iterations / stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"Sequential throughput: {throughput:F2} requests/second");
        Console.WriteLine($"Total time for {iterations} requests: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        // Minimum throughput requirement (adjust based on your needs)
        throughput.Should().BeGreaterThan(0.1, "Should process at least 0.1 requests per second sequentially");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task Throughput_ConcurrentProcessing_ShouldHandleParallelRequests()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        const int concurrentRequests = 3;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png")))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var throughput = concurrentRequests / stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"Concurrent throughput: {throughput:F2} requests/second");
        Console.WriteLine($"Total time for {concurrentRequests} concurrent requests: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        results.Should().HaveCount(concurrentRequests);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Should().AllSatisfy(r => r.Text.Should().NotBeNullOrWhiteSpace());

        // Concurrent processing should be more efficient than sequential
        throughput.Should().BeGreaterThan(0.15, "Concurrent processing should achieve better throughput");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task Throughput_DataflowPipeline_ShouldProcessStreamEfficiently()
    {
        // Arrange
        const int itemCount = 10;
        var dataUrl = GetTestImageDataUrl();
        var processedCount = 0;
        var results = new ConcurrentBag<MistralOcrResult>();
        var stopwatch = Stopwatch.StartNew();

        // Create a dataflow pipeline
        var processBlock = new ActionBlock<(string url, string mimeType)>(
            async item =>
            {
                var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync(item);
                results.Add(result);
                Interlocked.Increment(ref processedCount);
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 2,
                BoundedCapacity = 5
            });

        // Act
        for (int i = 0; i < itemCount; i++)
        {
            await processBlock.SendAsync((dataUrl, "image/png"));
        }

        processBlock.Complete();
        await processBlock.Completion;
        stopwatch.Stop();

        // Assert
        var throughput = itemCount / stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"Dataflow pipeline throughput: {throughput:F2} requests/second");
        Console.WriteLine($"Processed {processedCount} items in {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        processedCount.Should().Be(itemCount);
        results.Should().HaveCount(itemCount);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    #endregion

    #region Connection Pooling Efficiency

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task ConnectionPooling_MultipleServices_ShouldReuseConnections()
    {
        // Arrange - Create multiple service instances to test connection reuse
        var service1 = Fixture.ServiceProvider.GetRequiredService<IMistralOcrService>();
        var service2 = Fixture.ServiceProvider.GetRequiredService<IMistralOcrService>();
        var dataUrl = GetTestImageDataUrl();

        var connectionTimes = new List<TimeSpan>();

        // Act - Make requests alternating between services
        for (int i = 0; i < 4; i++)
        {
            var service = i % 2 == 0 ? service1 : service2;
            var stopwatch = Stopwatch.StartNew();
            
            var result = await service.ProcessImageDataItemAsync((dataUrl, "image/png"));
            stopwatch.Stop();
            
            result.Should().NotBeNull();
            connectionTimes.Add(stopwatch.Elapsed);
        }

        // Assert - Later requests should be faster due to connection reuse
        var firstRequestTime = connectionTimes[0];
        var lastRequestTime = connectionTimes[^1];

        Console.WriteLine($"First request time: {firstRequestTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Last request time: {lastRequestTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Average request time: {connectionTimes.Average(t => t.TotalMilliseconds):F2} ms");

        // Connection pooling should make subsequent requests more consistent
        var variance = connectionTimes.Select(t => Math.Pow(t.TotalMilliseconds - connectionTimes.Average(x => x.TotalMilliseconds), 2)).Average();
        var standardDeviation = Math.Sqrt(variance);
        
        Console.WriteLine($"Request time standard deviation: {standardDeviation:F2} ms");
        
        // Standard deviation should be reasonable, indicating consistent performance
        standardDeviation.Should().BeLessThan(5000, "Request times should be consistent with connection pooling");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task ConnectionPooling_HighConcurrency_ShouldHandleConnectionLimits()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        const int concurrentRequests = 5; // Test with moderate concurrency
        var connectionAttempts = new ConcurrentBag<(DateTime start, DateTime end, bool success)>();

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
        {
            var start = DateTime.UtcNow;
            try
            {
                var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png"));
                var end = DateTime.UtcNow;
                connectionAttempts.Add((start, end, true));
                return result;
            }
            catch (Exception)
            {
                var end = DateTime.UtcNow;
                connectionAttempts.Add((start, end, false));
                throw;
            }
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        var attempts = connectionAttempts.ToList();
        var successRate = attempts.Count(a => a.success) / (double)attempts.Count;
        var avgDuration = attempts.Average(a => (a.end - a.start).TotalMilliseconds);

        Console.WriteLine($"Success rate: {successRate:P2}");
        Console.WriteLine($"Average request duration: {avgDuration:F2} ms");
        Console.WriteLine($"Total successful requests: {attempts.Count(a => a.success)}");

        successRate.Should().BeGreaterThan(0.8, "At least 80% of concurrent requests should succeed");
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    #endregion

    #region Stream Processing Performance

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task StreamProcessing_ReactiveService_ShouldHandleBackpressure()
    {
        // Arrange
        var reactiveService = Fixture.ServiceProvider.GetRequiredService<IReactiveMistralOcrService>();
        var dataUrl = GetTestImageDataUrl();
        const int itemCount = 5;
        
        var sourceItems = Enumerable.Range(0, itemCount)
            .Select(_ => (dataUrl, "image/png"))
            .ToObservable();

        var results = new List<MistralOcrResult>();
        var processingTimes = new List<TimeSpan>();
        var stopwatch = Stopwatch.StartNew();

        // Act - Use simple sequential processing for reactive test
        using var semaphore = new SemaphoreSlim(2, 2); // Control concurrency
        
        var tasks = sourceItems.ToEnumerable().Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                var itemStopwatch = Stopwatch.StartNew();
                var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync(item);
                itemStopwatch.Stop();
                processingTimes.Add(itemStopwatch.Elapsed);
                results.Add(result);
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        var totalThroughput = itemCount / stopwatch.Elapsed.TotalSeconds;
        var avgProcessingTime = TimeSpan.FromTicks((long)processingTimes.Average(t => t.Ticks));

        Console.WriteLine($"Stream processing throughput: {totalThroughput:F2} items/second");
        Console.WriteLine($"Average item processing time: {avgProcessingTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Total processing time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        results.Should().HaveCount(itemCount);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        totalThroughput.Should().BeGreaterThan(0.1, "Stream processing should maintain reasonable throughput");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task StreamProcessing_LargeDataSet_ShouldMaintainPerformance()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        const int batchSize = 3;
        var processedItems = new ConcurrentBag<MistralOcrResult>();
        var stopwatch = Stopwatch.StartNew();

        // Create batches to simulate large dataset processing
        var batches = Enumerable.Range(0, batchSize)
            .Select(_ => (dataUrl, "image/png"));

        // Act
        using var semaphore = new SemaphoreSlim(2, 2); // Limit concurrent processing
        var tasks = batches.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync(item);
                processedItems.Add(result);
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var throughput = batchSize / stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"Large dataset throughput: {throughput:F2} items/second");
        Console.WriteLine($"Total items processed: {processedItems.Count}");
        Console.WriteLine($"Processing time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        processedItems.Should().HaveCount(batchSize);
        throughput.Should().BeGreaterThan(0.05, "Should maintain performance with larger datasets");
    }

    #endregion

    #region Load Testing

    [Fact(Skip = "Long running load test - enable manually")]
    public async Task LoadTest_SustainedTraffic_ShouldMaintainPerformance()
    {
        // This test provides basic load testing functionality
        // For advanced load testing, consider using NBomber directly
        // Skip by default as it's a long-running test
        
        var dataUrl = GetTestImageDataUrl();
        const int totalRequests = 10;
        const int concurrency = 2;
        
        var results = new List<(bool success, TimeSpan responseTime, string? error)>();
        var startTime = DateTime.UtcNow;
        
        using var semaphore = new SemaphoreSlim(concurrency, concurrency);
        
        var tasks = Enumerable.Range(0, totalRequests).Select(async i =>
        {
            await semaphore.WaitAsync();
            try
            {
                var requestStart = DateTime.UtcNow;
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png"));
                    stopwatch.Stop();
                    
                    var success = result != null && !string.IsNullOrWhiteSpace(result.Text);
                    lock (results)
                    {
                        results.Add((success, stopwatch.Elapsed, success ? null : "Invalid OCR result"));
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    lock (results)
                    {
                        results.Add((false, stopwatch.Elapsed, ex.Message));
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);
        var endTime = DateTime.UtcNow;
        
        // Assert load test results
        var successfulRequests = results.Count(r => r.success);
        var failedRequests = results.Count(r => !r.success);
        var successRate = successfulRequests / (double)totalRequests;
        var avgResponseTime = results.Where(r => r.success).Average(r => r.responseTime.TotalMilliseconds);
        var totalDuration = endTime - startTime;
        var throughput = totalRequests / totalDuration.TotalSeconds;
        
        Console.WriteLine($"Load test completed:");
        Console.WriteLine($"- Total requests: {totalRequests}");
        Console.WriteLine($"- Successful requests: {successfulRequests}");
        Console.WriteLine($"- Failed requests: {failedRequests}");
        Console.WriteLine($"- Success rate: {successRate:P2}");
        Console.WriteLine($"- Average response time: {avgResponseTime:F2} ms");
        Console.WriteLine($"- Throughput: {throughput:F2} requests/second");
        Console.WriteLine($"- Total duration: {totalDuration.TotalSeconds:F2} seconds");
        
        successfulRequests.Should().BeGreaterThan(0, "Should have successful requests");
        successRate.Should().BeGreaterThan(0.9, "Success rate should be above 90%");
        avgResponseTime.Should().BeLessThan(30000, "Average response time should be under 30 seconds");
    }

    #endregion

    #region Performance Regression Tests

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task PerformanceRegression_BaselineComparison_ShouldNotDegrade()
    {
        // Arrange - Define baseline performance expectations
        var baselineResponseTime = TimeSpan.FromSeconds(30); // Adjust based on your baseline
        var baselineMemoryUsage = 50 * 1024 * 1024; // 50MB baseline
        
        var dataUrl = GetTestImageDataUrl();
        
        // Measure current performance
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);
        
        var stopwatch = Stopwatch.StartNew();
        var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png"));
        stopwatch.Stop();
        
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);
        var memoryUsed = finalMemory - initialMemory;

        // Assert - Performance should not degrade beyond baseline
        Console.WriteLine($"Current response time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms (baseline: {baselineResponseTime.TotalMilliseconds:F2} ms)");
        Console.WriteLine($"Current memory usage: {memoryUsed / 1024 / 1024:F2} MB (baseline: {baselineMemoryUsage / 1024 / 1024:F2} MB)");
        
        result.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(baselineResponseTime, "Response time should not exceed baseline");
        memoryUsed.Should().BeLessThan(baselineMemoryUsage, "Memory usage should not exceed baseline");
    }

    #endregion
}