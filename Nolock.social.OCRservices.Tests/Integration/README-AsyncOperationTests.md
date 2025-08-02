# Async Operation Integration Tests

This document describes the comprehensive integration tests for async operations in the OCR services located in `AsyncOperationTests.cs`.

## Test Categories

### 1. Async Endpoint Response Handling Tests

- **`AsyncEndpoint_WithValidRequest_ReturnsAsyncResponse`**: Tests that async endpoints properly handle valid requests for both check and receipt document types, including delayed processing scenarios.
- **`AsyncEndpoint_WithSlowProcessing_HandlesDelayedResponses`**: Validates that the system can handle slow OCR processing (2+ second delays) and still return valid responses.

### 2. Polling Mechanism Validation Tests

- **`PollingMechanism_WithReactiveStream_HandlesMultipleStatusUpdates`**: Tests reactive streams that emit multiple status updates during processing, ensuring the final result contains the latest status.
- **`PollingMechanism_WithCancellation_HandlesGracefulShutdown`**: Validates graceful handling of cancellation requests during async processing.

### 3. Timeout Scenarios Tests

- **`TimeoutScenario_WithLongRunningOperation_HandlesTimeout`**: Tests client timeout handling when operations exceed expected duration (1 second client timeout vs 5 second operation).
- **`TimeoutScenario_WithPartialProcessing_ReturnsPartialResults`**: Validates that partial results are returned when extraction service times out but OCR text is available.

### 4. Concurrent Async Operations Tests

- **`ConcurrentOperations_WithMultipleRequests_ProcessesIndependently`**: Tests that multiple concurrent requests (5 simultaneous) are processed independently with unique results.
- **`ConcurrentOperations_WithResourceContention_HandlesBackpressure`**: Validates backpressure handling under heavy load (10 concurrent requests) with proper resource management.

### 5. Status Transition Validation Tests

- **`StatusTransition_FromPendingToProcessingToComplete_ValidatesCorrectFlow`**: Tests the complete status transition flow from pending → processing → extracting → completed.
- **`StatusTransition_WithFailureDuringProcessing_HandlesErrorStates`**: Validates error handling when failures occur during different processing stages.
- **`StatusTransition_WithRetryLogic_RecoversFromTransientFailures`**: Tests retry logic for transient failures (simulates 3 attempts with success on final attempt).

## Key Features Tested

### Reactive Streams Integration
- Uses `System.Reactive` patterns with `Subject<T>` and `Observable` for status updates
- Tests both successful completion and error scenarios
- Validates proper disposal of reactive resources

### Resource Management
- Proper disposal of `ByteArrayContent` and HTTP responses
- Memory stream management for concurrent operations
- Clean shutdown of background tasks

### Error Handling
- Timeout scenarios with partial and complete failures
- Network errors and transient failures
- Graceful degradation with meaningful error messages

### Performance and Concurrency
- Thread-safe operation tracking
- Backpressure handling under load
- Independent processing of concurrent requests

## Test Infrastructure

### Mocking Strategy
- **`IReactiveMistralOcrService`**: Mocked for OCR processing with configurable delays and results
- **`IOcrExtractionService`**: Mocked for structured data extraction with success/failure scenarios
- **`WebApplicationFactory<Program>`**: Provides isolated test environment

### Test Data
- Uses `TestImageResources.CreateTextReceiptImage()` for consistent test image data
- Generates synthetic OCR results and extraction responses
- Supports both check and receipt document types

### Assertions
- Validates HTTP status codes and response structure
- Checks processing times and token consumption
- Verifies status transitions and error handling
- Ensures resource cleanup and proper disposal

## Running the Tests

```bash
# Run all async operation tests
dotnet test --filter "FullyQualifiedName~AsyncOperationTests"

# Run specific test categories
dotnet test --filter "AsyncEndpoint_WithValidRequest_ReturnsAsyncResponse"
dotnet test --filter "ConcurrentOperations_WithMultipleRequests_ProcessesIndependently"
dotnet test --filter "StatusTransition_FromPendingToProcessingToComplete_ValidatesCorrectFlow"
```

## Performance Characteristics

- **Basic async operations**: ~100-800ms execution time
- **Slow processing tests**: ~2+ seconds (by design)
- **Timeout tests**: ~1-2 seconds (controlled timeouts)
- **Concurrent operations**: Scales to 10+ simultaneous requests
- **Memory usage**: Efficient with proper resource disposal

These tests ensure the OCR service can handle real-world async scenarios including network delays, concurrent users, partial failures, and resource constraints while maintaining reliability and performance.