using FluentAssertions;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.IntegrationTests.Helpers;

namespace Nolock.social.MistralOcr.IntegrationTests;

/// <summary>
/// Advanced performance tests using the PerformanceTestHelper for more sophisticated measurements
/// </summary>
public class AdvancedPerformanceTests : TestBase
{
    public AdvancedPerformanceTests(MistralOcrTestFixture fixture) : base(fixture)
    {
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task PerformanceMetrics_SingleOperation_ShouldProvideDetailedMeasurements()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();

        // Act
        var metrics = await PerformanceTestHelper.MeasureAsync(async () =>
            await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png")));

        // Assert
        Console.WriteLine($"Execution Time: {metrics.ExecutionTimeMs:F2} ms");
        Console.WriteLine($"Memory Used: {metrics.MemoryUsedMB:F2} MB");
        Console.WriteLine($"Initial Memory: {metrics.InitialMemoryMB:F2} MB");
        Console.WriteLine($"Final Memory: {metrics.FinalMemoryMB:F2} MB");

        metrics.Result.Should().NotBeNull();
        metrics.ExecutionTime.Should().BeLessThan(TimeSpan.FromSeconds(60));
        
        // Memory usage should be reasonable
        Math.Abs(metrics.MemoryUsedMB).Should().BeLessThan(100, "Single operation should not use excessive memory");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task PerformanceMetrics_MultipleOperations_ShouldShowConsistentPerformance()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        const int iterations = 3;

        // Act
        var aggregatedMetrics = await PerformanceTestHelper.MeasureMultipleAsync(
            async () => await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png")),
            iterations,
            includeWarmup: true);

        // Assert
        Console.WriteLine($"Iterations: {aggregatedMetrics.Count}");
        Console.WriteLine($"Average Execution Time: {aggregatedMetrics.AverageExecutionTimeMs:F2} ms");
        Console.WriteLine($"Min Execution Time: {aggregatedMetrics.MinExecutionTimeMs:F2} ms");
        Console.WriteLine($"Max Execution Time: {aggregatedMetrics.MaxExecutionTimeMs:F2} ms");
        Console.WriteLine($"Standard Deviation: {aggregatedMetrics.StandardDeviationMs:F2} ms");
        Console.WriteLine($"Average Memory Used: {aggregatedMetrics.AverageMemoryUsedMB:F2} MB");

        aggregatedMetrics.Count.Should().Be(iterations);
        aggregatedMetrics.AverageExecutionTime.Should().BeLessThan(TimeSpan.FromSeconds(45));
        
        // Performance should be relatively consistent (low standard deviation relative to mean)
        var coefficientOfVariation = aggregatedMetrics.StandardDeviationMs / aggregatedMetrics.AverageExecutionTimeMs;
        coefficientOfVariation.Should().BeLessThan(0.5, "Performance should be relatively consistent");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task ThroughputMetrics_ConcurrentOperations_ShouldDemonstrateScaling()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        const int concurrency = 2;
        const int totalOperations = 4;

        // Act
        var throughputMetrics = await PerformanceTestHelper.MeasureThroughputAsync(
            concurrency,
            totalOperations,
            async () => await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png")));

        // Assert
        Console.WriteLine($"Total Operations: {throughputMetrics.TotalOperations}");
        Console.WriteLine($"Concurrency: {throughputMetrics.Concurrency}");
        Console.WriteLine($"Total Duration: {throughputMetrics.TotalDuration.TotalSeconds:F2} seconds");
        Console.WriteLine($"Operations Per Second: {throughputMetrics.OperationsPerSecond:F2}");

        throughputMetrics.Results.Should().HaveCount(totalOperations);
        throughputMetrics.OperationsPerSecond.Should().BeGreaterThan(0);
        throughputMetrics.CompletionTimes.Should().HaveCount(totalOperations);
        
        // All operations should complete within reasonable time
        throughputMetrics.TotalDuration.Should().BeLessThan(TimeSpan.FromMinutes(5));
    }

    [Fact(Skip = "Long running load test - enable manually")]
    public async Task LoadTest_ExtendedDuration_ShouldMaintainStability()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        var warmupDuration = TimeSpan.FromSeconds(10);
        var testDuration = TimeSpan.FromSeconds(30);
        var maxConcurrency = 2;
        var rampUpDuration = TimeSpan.FromSeconds(5);

        // Act
        var loadTestResults = await PerformanceTestHelper.RunLoadTestAsync(
            async () => await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png")),
            warmupDuration,
            testDuration,
            maxConcurrency,
            rampUpDuration);

        // Assert
        Console.WriteLine($"=== Load Test Results ===");
        Console.WriteLine($"Total Requests: {loadTestResults.TotalRequests}");
        Console.WriteLine($"Successful Requests: {loadTestResults.SuccessfulRequests}");
        Console.WriteLine($"Failed Requests: {loadTestResults.FailedRequests}");
        Console.WriteLine($"Success Rate: {loadTestResults.SuccessRate:P2}");
        Console.WriteLine($"Error Rate: {loadTestResults.ErrorRate:P2}");
        Console.WriteLine($"Average Response Time: {loadTestResults.AverageResponseTime:F2} ms");
        Console.WriteLine($"Median Response Time: {loadTestResults.MedianResponseTime:F2} ms");
        Console.WriteLine($"95th Percentile: {loadTestResults.Percentile95ResponseTime:F2} ms");
        Console.WriteLine($"99th Percentile: {loadTestResults.Percentile99ResponseTime:F2} ms");
        Console.WriteLine($"Throughput: {loadTestResults.ThroughputPerSecond:F2} requests/second");

        // Quality assertions
        loadTestResults.SuccessRate.Should().BeGreaterThan(0.95, "Success rate should be above 95%");
        loadTestResults.ErrorRate.Should().BeLessThan(0.05, "Error rate should be below 5%");
        loadTestResults.AverageResponseTime.Should().BeLessThan(30000, "Average response time should be under 30 seconds");
        loadTestResults.Percentile95ResponseTime.Should().BeLessThan(45000, "95th percentile should be under 45 seconds");
        loadTestResults.ThroughputPerSecond.Should().BeGreaterThan(0.1, "Should maintain minimum throughput");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task MemoryPressure_LargeImageProcessing_ShouldHandleGracefully()
    {
        // Arrange - Use a larger image for memory pressure testing
        var largeImageDataUrl = GetTestImageDataUrlWithText();
        const int iterations = 3;

        // Act
        var results = new List<PerformanceMetrics>();
        
        for (int i = 0; i < iterations; i++)
        {
            var metrics = await PerformanceTestHelper.MeasureAsync(async () =>
                await Fixture.MistralOcrService.ProcessImageDataItemAsync((largeImageDataUrl, "image/png")));
            
            results.Add(metrics);
            
            // Force garbage collection between iterations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            Console.WriteLine($"Iteration {i + 1}: {metrics.ExecutionTimeMs:F2} ms, {metrics.MemoryUsedMB:F2} MB");
        }

        // Assert
        var avgExecutionTime = TimeSpan.FromTicks((long)results.Average(r => r.ExecutionTime.Ticks));
        var avgMemoryUsed = results.Average(r => r.MemoryUsed);
        
        Console.WriteLine($"Average execution time: {avgExecutionTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Average memory used: {avgMemoryUsed / 1024 / 1024:F2} MB");

        // All operations should succeed
        results.Should().AllSatisfy(r => r.Result.Should().NotBeNull());
        
        // Memory usage should be consistent across iterations (no memory leaks)
        var memoryVariance = results.Select(r => Math.Pow(r.MemoryUsed - avgMemoryUsed, 2)).Average();
        var memoryStdDev = Math.Sqrt(memoryVariance);
        
        Console.WriteLine($"Memory usage standard deviation: {memoryStdDev / 1024 / 1024:F2} MB");
        
        // Memory usage should be consistent (low standard deviation indicates no memory leaks)
        memoryStdDev.Should().BeLessThan(50 * 1024 * 1024, "Memory usage should be consistent across iterations");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task ResponseTime_UnderLoad_ShouldDegradeGracefully()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        var baselineMetrics = await PerformanceTestHelper.MeasureAsync(
            async () => await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png")));

        // Act - Measure performance under concurrent load
        var loadMetrics = await PerformanceTestHelper.MeasureThroughputAsync(
            concurrency: 3,
            totalOperations: 6,
            async () => await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png")));

        // Assert
        var baselineTime = baselineMetrics.ExecutionTimeMs;
        var avgLoadTime = loadMetrics.TotalDuration.TotalMilliseconds / loadMetrics.TotalOperations;
        var degradationFactor = avgLoadTime / baselineTime;

        Console.WriteLine($"Baseline response time: {baselineTime:F2} ms");
        Console.WriteLine($"Average response time under load: {avgLoadTime:F2} ms");
        Console.WriteLine($"Performance degradation factor: {degradationFactor:F2}x");

        // Performance should degrade gracefully, not catastrophically
        degradationFactor.Should().BeLessThan(3.0, "Performance degradation under load should be reasonable");
        loadMetrics.Results.Should().HaveCount(loadMetrics.TotalOperations, "All operations should complete");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task ConnectionReuse_SequentialRequests_ShouldShowImprovement()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        var responseTimes = new List<TimeSpan>();

        // Act - Make several sequential requests to test connection reuse
        for (int i = 0; i < 4; i++)
        {
            var metrics = await PerformanceTestHelper.MeasureAsync(
                async () => await Fixture.MistralOcrService.ProcessImageDataItemAsync((dataUrl, "image/png")));
            
            responseTimes.Add(metrics.ExecutionTime);
            Console.WriteLine($"Request {i + 1}: {metrics.ExecutionTimeMs:F2} ms");
        }

        // Assert
        var firstRequestTime = responseTimes[0];
        var lastRequestTime = responseTimes[^1];
        var averageTime = TimeSpan.FromTicks((long)responseTimes.Average(t => t.Ticks));

        Console.WriteLine($"First request: {firstRequestTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Last request: {lastRequestTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Average: {averageTime.TotalMilliseconds:F2} ms");

        // Connection reuse should lead to more consistent performance
        // (First request might be slower due to connection establishment)
        var responseTimeVariance = responseTimes.Select(t => 
            Math.Pow(t.TotalMilliseconds - averageTime.TotalMilliseconds, 2)).Average();
        var standardDeviation = Math.Sqrt(responseTimeVariance);

        Console.WriteLine($"Response time standard deviation: {standardDeviation:F2} ms");
        
        // Standard deviation should be reasonable, indicating consistent performance
        var coefficientOfVariation = standardDeviation / averageTime.TotalMilliseconds;
        coefficientOfVariation.Should().BeLessThan(0.3, "Response times should be relatively consistent with connection reuse");
    }

    [Fact(Skip = "Performance tests are resource-intensive and should be run selectively")]
    public async Task StressTest_ErrorHandling_ShouldFailGracefully()
    {
        // Arrange - Test with invalid data to trigger errors
        var invalidDataUrl = "data:image/png;base64,invalid-base64-data";
        const int attempts = 3;
        var errorMetrics = new List<PerformanceMetrics>();

        // Act - Measure error handling performance
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                var metrics = await PerformanceTestHelper.MeasureAsync<object>(async () =>
                {
                    try
                    {
                        return await Fixture.MistralOcrService.ProcessImageDataItemAsync((invalidDataUrl, "image/png"));
                    }
                    catch (Exception ex)
                    {
                        // Return exception as result for measurement purposes
                        return ex;
                    }
                });
                
                errorMetrics.Add(metrics);
                Console.WriteLine($"Error handling attempt {i + 1}: {metrics.ExecutionTimeMs:F2} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in attempt {i + 1}: {ex.Message}");
            }
        }

        // Assert
        if (errorMetrics.Any())
        {
            var avgErrorHandlingTime = TimeSpan.FromTicks((long)errorMetrics.Average(m => m.ExecutionTime.Ticks));
            Console.WriteLine($"Average error handling time: {avgErrorHandlingTime.TotalMilliseconds:F2} ms");

            // Error handling should be fast (faster than successful requests)
            avgErrorHandlingTime.Should().BeLessThan(TimeSpan.FromSeconds(10), "Error handling should be fast");
            
            // All error handling attempts should complete (not hang or timeout)
            errorMetrics.Should().HaveCount(attempts, "All error handling attempts should complete");
        }
    }
}