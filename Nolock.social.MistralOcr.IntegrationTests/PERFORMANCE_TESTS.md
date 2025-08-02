# MistralOcr Performance Tests

This document describes the comprehensive performance and stress testing suite for the MistralOcr service.

## Overview

The performance test suite includes:

1. **Response Time Benchmarks** - Measure and validate API response times
2. **Memory Usage Validation** - Monitor memory consumption and detect leaks
3. **Throughput Testing** - Measure requests per second under various loads
4. **Connection Pooling Efficiency** - Verify HTTP connection reuse and pooling
5. **Stream Processing Performance** - Test reactive and dataflow processing

## Test Files

- `PerformanceTests.cs` - Main performance test suite using xUnit
- `AdvancedPerformanceTests.cs` - Advanced tests using the PerformanceTestHelper
- `Helpers/PerformanceTestHelper.cs` - Utility class for performance measurements
- `appsettings.performance.json` - Performance test configuration

## Configuration

### Environment Variables

Set the following environment variable before running tests:

```bash
export MISTRAL_API_KEY="your-mistral-api-key"
```

### Performance Thresholds

The tests use configurable thresholds defined in `appsettings.performance.json`:

```json
{
  "PerformanceTest": {
    "Thresholds": {
      "MaxResponseTimeMs": 30000,
      "MaxMemoryUsageMB": 100,
      "MinThroughputRps": 0.1,
      "MaxErrorRate": 0.05,
      "MaxCoefficientOfVariation": 0.3
    }
  }
}
```

## Running the Tests

### Standard Performance Tests

Run all performance tests:

```bash
dotnet test --filter "FullyQualifiedName~PerformanceTests"
```

Run specific test categories:

```bash
# Response time benchmarks
dotnet test --filter "FullyQualifiedName~ResponseTime"

# Memory usage tests
dotnet test --filter "FullyQualifiedName~MemoryUsage"

# Throughput tests
dotnet test --filter "FullyQualifiedName~Throughput"

# Connection pooling tests
dotnet test --filter "FullyQualifiedName~ConnectionPooling"

# Stream processing tests
dotnet test --filter "FullyQualifiedName~StreamProcessing"
```

### Load Tests

Most load tests are skipped by default. To run them manually:

```bash
# Enable long-running load tests (remove Skip attribute first)
dotnet test --filter "FullyQualifiedName~LoadTest"
```

### Advanced Performance Tests

```bash
dotnet test --filter "FullyQualifiedName~AdvancedPerformanceTests"
```

## Test Descriptions

### Response Time Benchmarks

- **SingleImageProcessing** - Measures response time for individual OCR requests
- **DifferentImageSizes** - Compares performance across different image sizes
- **UnderLoad** - Tests response time degradation under concurrent load

### Memory Usage Validation

- **SingleImageProcessing** - Monitors memory usage for single operations
- **StreamProcessing** - Validates proper disposal of streams and resources
- **LargeImageProcessing** - Tests memory pressure with larger images

### Throughput Testing

- **SequentialProcessing** - Measures sequential request throughput
- **ConcurrentProcessing** - Tests parallel request handling
- **DataflowPipeline** - Validates dataflow pipeline efficiency

### Connection Pooling Efficiency

- **MultipleServices** - Tests connection reuse across service instances
- **HighConcurrency** - Validates connection pool behavior under load
- **SequentialRequests** - Measures connection reuse improvements

### Stream Processing Performance

- **ReactiveService** - Tests reactive streams with backpressure handling
- **LargeDataSet** - Validates performance with batch processing

## Performance Metrics

The tests capture and report:

- **Execution Time** - Request/response latency
- **Memory Usage** - Heap allocation and garbage collection impact
- **Throughput** - Requests per second
- **Error Rates** - Success/failure ratios
- **Percentiles** - 95th and 99th percentile response times
- **Standard Deviation** - Performance consistency metrics

## NBomber Integration

For advanced load testing, the suite includes NBomber integration:

```csharp
[Fact(Skip = "Long running load test - enable manually")]
public async Task LoadTest_SustainedTraffic_ShouldMaintainPerformance()
{
    // NBomber scenario configuration
    var scenario = Scenario.Create("mistral_ocr_load_test", async context =>
    {
        // Test implementation
    })
    .WithLoadSimulations(
        Simulation.InjectPerSec(rate: 1, during: TimeSpan.FromSeconds(10)), // Warmup
        Simulation.InjectPerSec(rate: 2, during: TimeSpan.FromSeconds(30))  // Load test
    );
}
```

## Interpreting Results

### Good Performance Indicators

- Response times under 30 seconds average
- Memory usage under 100MB per operation
- Throughput above 0.1 requests/second
- Error rate below 5%
- Coefficient of variation below 0.3 (consistent performance)

### Warning Signs

- Increasing response times over iterations (possible service degradation)
- Growing memory usage (potential memory leaks)
- High error rates (service instability)
- High standard deviation (inconsistent performance)

## Troubleshooting

### Common Issues

1. **API Key Not Set**
   ```
   Error: Mistral API key not configured
   Solution: Set MISTRAL_API_KEY environment variable
   ```

2. **Tests Timing Out**
   ```
   Increase timeout values in appsettings.performance.json
   ```

3. **High Memory Usage**
   ```
   Check for proper disposal of streams and HTTP clients
   ```

4. **Connection Errors**
   ```
   Verify network connectivity and API service availability
   ```

### Performance Optimization Tips

1. **Use Connection Pooling** - Reuse HttpClient instances
2. **Implement Proper Disposal** - Use `using` statements for streams
3. **Monitor Memory Usage** - Regular garbage collection in long-running tests
4. **Configure Timeouts** - Set appropriate request timeouts
5. **Handle Backpressure** - Use semaphores to limit concurrent requests

## CI/CD Integration

Add performance tests to your build pipeline:

```yaml
- name: Run Performance Tests
  run: |
    export MISTRAL_API_KEY="${{ secrets.MISTRAL_API_KEY }}"
    dotnet test --filter "FullyQualifiedName~PerformanceTests&Category!=LongRunning" \
                --logger "trx;LogFileName=performance-results.trx" \
                --collect:"XPlat Code Coverage"
```

## Reporting

Performance test results are captured in:

- xUnit test output (console)
- TRX test result files
- NBomber HTML reports (when enabled)
- Custom performance metrics logs

## Contributing

When adding new performance tests:

1. Use the `PerformanceTestHelper` for consistent measurements
2. Include appropriate assertions with meaningful thresholds
3. Add proper test categorization and documentation
4. Consider both positive and negative test scenarios
5. Include performance regression tests for critical paths