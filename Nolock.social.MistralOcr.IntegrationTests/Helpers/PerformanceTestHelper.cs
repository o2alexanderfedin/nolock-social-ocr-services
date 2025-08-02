using System.Diagnostics;

namespace Nolock.social.MistralOcr.IntegrationTests.Helpers;

/// <summary>
/// Helper class for performance testing utilities
/// </summary>
public static class PerformanceTestHelper
{
    /// <summary>
    /// Measures execution time and memory usage of an async operation
    /// </summary>
    public static async Task<PerformanceMetrics> MeasureAsync<T>(Func<Task<T>> operation)
    {
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        var result = await operation();

        stopwatch.Stop();
        
        // Force garbage collection after operation
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);

        return new PerformanceMetrics
        {
            ExecutionTime = stopwatch.Elapsed,
            MemoryUsed = finalMemory - initialMemory,
            InitialMemory = initialMemory,
            FinalMemory = finalMemory,
            Result = result
        };
    }

    /// <summary>
    /// Measures execution time and memory usage of a synchronous operation
    /// </summary>
    public static PerformanceMetrics Measure<T>(Func<T> operation)
    {
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        var result = operation();

        stopwatch.Stop();
        
        // Force garbage collection after operation
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);

        return new PerformanceMetrics
        {
            ExecutionTime = stopwatch.Elapsed,
            MemoryUsed = finalMemory - initialMemory,
            InitialMemory = initialMemory,
            FinalMemory = finalMemory,
            Result = result
        };
    }

    /// <summary>
    /// Runs multiple iterations of an async operation and returns aggregated metrics
    /// </summary>
    public static async Task<AggregatedPerformanceMetrics> MeasureMultipleAsync<T>(
        Func<Task<T>> operation, 
        int iterations = 5,
        bool includeWarmup = true)
    {
        var metrics = new List<PerformanceMetrics>();

        // Warmup run if requested
        if (includeWarmup)
        {
            await operation();
        }

        // Measure iterations
        for (int i = 0; i < iterations; i++)
        {
            var metric = await MeasureAsync(operation);
            metrics.Add(metric);
        }

        return new AggregatedPerformanceMetrics(metrics);
    }

    /// <summary>
    /// Runs multiple iterations of a synchronous operation and returns aggregated metrics
    /// </summary>
    public static AggregatedPerformanceMetrics MeasureMultiple<T>(
        Func<T> operation, 
        int iterations = 5,
        bool includeWarmup = true)
    {
        var metrics = new List<PerformanceMetrics>();

        // Warmup run if requested
        if (includeWarmup)
        {
            operation();
        }

        // Measure iterations
        for (int i = 0; i < iterations; i++)
        {
            var metric = Measure(operation);
            metrics.Add(metric);
        }

        return new AggregatedPerformanceMetrics(metrics);
    }

    /// <summary>
    /// Measures throughput for concurrent operations
    /// </summary>
    public static async Task<ThroughputMetrics> MeasureThroughputAsync<T>(
        int concurrency,
        int totalOperations,
        Func<Task<T>> operation)
    {
        using var semaphore = new SemaphoreSlim(concurrency, concurrency);
        var results = new List<T>();
        var completionTimes = new List<DateTime>();
        var startTime = DateTime.UtcNow;

        var tasks = Enumerable.Range(0, totalOperations).Select(async _ =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await operation();
                var completionTime = DateTime.UtcNow;
                
                lock (results)
                {
                    results.Add(result);
                    completionTimes.Add(completionTime);
                }
                
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        var endTime = DateTime.UtcNow;

        return new ThroughputMetrics
        {
            TotalOperations = totalOperations,
            Concurrency = concurrency,
            TotalDuration = endTime - startTime,
            OperationsPerSecond = totalOperations / (endTime - startTime).TotalSeconds,
            CompletionTimes = completionTimes.ToList(),
            Results = results.Cast<object>().ToList()
        };
    }

    /// <summary>
    /// Creates a load test scenario with ramping
    /// </summary>
    public static async Task<LoadTestResults> RunLoadTestAsync<T>(
        Func<Task<T>> operation,
        TimeSpan warmupDuration,
        TimeSpan testDuration,
        int maxConcurrency,
        TimeSpan rampUpDuration)
    {
        var results = new List<LoadTestResult>();
        var startTime = DateTime.UtcNow;
        var testEndTime = startTime.Add(warmupDuration).Add(testDuration);
        
        // Warmup phase
        Console.WriteLine("Starting warmup phase...");
        var warmupEndTime = startTime.Add(warmupDuration);
        while (DateTime.UtcNow < warmupEndTime)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warmup error: {ex.Message}");
            }
            
            await Task.Delay(1000); // 1 second intervals during warmup
        }

        // Main load test phase
        Console.WriteLine("Starting load test phase...");
        var loadTestTasks = new List<Task>();
        using var cts = new CancellationTokenSource();
        
        // Gradually ramp up concurrency
        var rampUpInterval = rampUpDuration.TotalMilliseconds / maxConcurrency;
        
        for (int i = 0; i < maxConcurrency; i++)
        {
            var taskStartDelay = TimeSpan.FromMilliseconds(i * rampUpInterval);
            loadTestTasks.Add(RunWorkerAsync(operation, testEndTime, taskStartDelay, results, cts.Token));
        }

        // Wait for test completion
        await Task.Delay(testDuration);
        await cts.CancelAsync();
        
        try
        {
            await Task.WhenAll(loadTestTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }

        return new LoadTestResults
        {
            TotalRequests = results.Count,
            SuccessfulRequests = results.Count(r => r.Success),
            FailedRequests = results.Count(r => !r.Success),
            AverageResponseTime = results.Where(r => r.Success).Average(r => r.ResponseTime.TotalMilliseconds),
            MedianResponseTime = CalculateMedian(results.Where(r => r.Success).Select(r => r.ResponseTime.TotalMilliseconds)),
            Percentile95ResponseTime = CalculatePercentile(results.Where(r => r.Success).Select(r => r.ResponseTime.TotalMilliseconds), 95),
            Percentile99ResponseTime = CalculatePercentile(results.Where(r => r.Success).Select(r => r.ResponseTime.TotalMilliseconds), 99),
            ThroughputPerSecond = results.Count / testDuration.TotalSeconds,
            TestDuration = testDuration,
            ErrorRate = results.Count(r => !r.Success) / (double)results.Count
        };
    }

    private static async Task RunWorkerAsync<T>(
        Func<Task<T>> operation,
        DateTime endTime,
        TimeSpan startDelay,
        List<LoadTestResult> results,
        CancellationToken cancellationToken)
    {
        await Task.Delay(startDelay, cancellationToken);
        
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            var requestStart = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await operation();
                stopwatch.Stop();
                
                lock (results)
                {
                    results.Add(new LoadTestResult
                    {
                        Success = true,
                        ResponseTime = stopwatch.Elapsed,
                        Timestamp = requestStart
                    });
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                lock (results)
                {
                    results.Add(new LoadTestResult
                    {
                        Success = false,
                        ResponseTime = stopwatch.Elapsed,
                        Timestamp = requestStart,
                        Error = ex.Message
                    });
                }
            }
            
            // Small delay to prevent overwhelming the service
            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private static double CalculateMedian(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        var count = sorted.Length;
        
        if (count == 0) return 0;
        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        return sorted[count / 2];
    }

    private static double CalculatePercentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        var count = sorted.Length;
        
        if (count == 0) return 0;
        
        var index = (percentile / 100.0) * (count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        
        if (lower == upper)
        {
            return sorted[lower];
        }
        
        var weight = index - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}

/// <summary>
/// Performance metrics for a single operation
/// </summary>
public class PerformanceMetrics
{
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsed { get; set; }
    public long InitialMemory { get; set; }
    public long FinalMemory { get; set; }
    public object? Result { get; set; }
    
    public double ExecutionTimeMs => ExecutionTime.TotalMilliseconds;
    public double MemoryUsedMB => MemoryUsed / 1024.0 / 1024.0;
    public double InitialMemoryMB => InitialMemory / 1024.0 / 1024.0;
    public double FinalMemoryMB => FinalMemory / 1024.0 / 1024.0;
}

/// <summary>
/// Aggregated performance metrics for multiple operations
/// </summary>
public class AggregatedPerformanceMetrics
{
    private readonly List<PerformanceMetrics> _metrics;

    public AggregatedPerformanceMetrics(List<PerformanceMetrics> metrics)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public int Count => _metrics.Count;
    
    public TimeSpan AverageExecutionTime => TimeSpan.FromTicks((long)_metrics.Average(m => m.ExecutionTime.Ticks));
    public TimeSpan MinExecutionTime => TimeSpan.FromTicks(_metrics.Min(m => m.ExecutionTime.Ticks));
    public TimeSpan MaxExecutionTime => TimeSpan.FromTicks(_metrics.Max(m => m.ExecutionTime.Ticks));
    
    public long AverageMemoryUsed => (long)_metrics.Average(m => m.MemoryUsed);
    public long MinMemoryUsed => _metrics.Min(m => m.MemoryUsed);
    public long MaxMemoryUsed => _metrics.Max(m => m.MemoryUsed);
    
    public double AverageExecutionTimeMs => AverageExecutionTime.TotalMilliseconds;
    public double MinExecutionTimeMs => MinExecutionTime.TotalMilliseconds;
    public double MaxExecutionTimeMs => MaxExecutionTime.TotalMilliseconds;
    
    public double AverageMemoryUsedMB => AverageMemoryUsed / 1024.0 / 1024.0;
    public double MinMemoryUsedMB => MinMemoryUsed / 1024.0 / 1024.0;
    public double MaxMemoryUsedMB => MaxMemoryUsed / 1024.0 / 1024.0;

    public double StandardDeviationMs
    {
        get
        {
            var mean = AverageExecutionTimeMs;
            var variance = _metrics.Average(m => Math.Pow(m.ExecutionTimeMs - mean, 2));
            return Math.Sqrt(variance);
        }
    }
}

/// <summary>
/// Throughput metrics for concurrent operations
/// </summary>
public class ThroughputMetrics
{
    public int TotalOperations { get; set; }
    public int Concurrency { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double OperationsPerSecond { get; set; }
    public List<DateTime> CompletionTimes { get; set; } = new();
    public List<object> Results { get; set; } = new();
}

/// <summary>
/// Load test result for a single request
/// </summary>
public class LoadTestResult
{
    public bool Success { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Aggregated load test results
/// </summary>
public class LoadTestResults
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public double MedianResponseTime { get; set; }
    public double Percentile95ResponseTime { get; set; }
    public double Percentile99ResponseTime { get; set; }
    public double ThroughputPerSecond { get; set; }
    public TimeSpan TestDuration { get; set; }
    public double ErrorRate { get; set; }
    
    public double SuccessRate => 1.0 - ErrorRate;
}