using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nolock.social.OCRservices.Core.Models;
using Nolock.social.OCRservices.Tests.TestData;
using Xunit;

namespace Nolock.social.OCRservices.Tests.Integration;

/// <summary>
/// Comprehensive edge case tests for the OCR service endpoints
/// Tests various failure scenarios and boundary conditions
/// </summary>
public class EdgeCaseTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly HttpClient _client;

    public EdgeCaseTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    #region Empty Image Handling Tests

    [Fact]
    public async Task ReceiptsEndpoint_WithEmptyContent_ReturnsErrorResponse()
    {
        // Arrange
        using var emptyContent = new ByteArrayContent(Array.Empty<byte>());
        emptyContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", emptyContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(receiptResponse);
        Assert.False(receiptResponse.Success);
        Assert.NotNull(receiptResponse.Error);
        Assert.Contains("Failed to extract text", receiptResponse.Error);
    }

    [Fact]
    public async Task ChecksEndpoint_WithEmptyContent_ReturnsErrorResponse()
    {
        // Arrange
        using var emptyContent = new ByteArrayContent(Array.Empty<byte>());
        emptyContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await _client.PostAsync("/ocr/checks", emptyContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(checkResponse);
        Assert.False(checkResponse.Success);
        Assert.NotNull(checkResponse.Error);
        Assert.Contains("Failed to extract text", checkResponse.Error);
    }

    [Fact]
    public async Task ReceiptsEndpoint_WithNullStream_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsync("/ocr/receipts", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Corrupted Image Data Tests

    [Fact]
    public async Task ReceiptsEndpoint_WithCorruptedImageData_HandlesGracefully()
    {
        // Arrange - Create corrupted image data (invalid JPEG header)
        var corruptedImageData = CreateCorruptedImageData();
        using var corruptedContent = new ByteArrayContent(corruptedImageData);
        corruptedContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", corruptedContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(receiptResponse);
        // The service should handle corrupted data gracefully, either succeeding with minimal text or failing gracefully
        Assert.True(receiptResponse.Success || !string.IsNullOrEmpty(receiptResponse.Error));
    }

    [Fact]
    public async Task ChecksEndpoint_WithCorruptedImageData_HandlesGracefully()
    {
        // Arrange - Create corrupted image data
        var corruptedImageData = CreateCorruptedImageData();
        using var corruptedContent = new ByteArrayContent(corruptedImageData);
        corruptedContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await _client.PostAsync("/ocr/checks", corruptedContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(checkResponse);
        // The service should handle corrupted data gracefully
        Assert.True(checkResponse.Success || !string.IsNullOrEmpty(checkResponse.Error));
    }

    [Fact]
    public async Task ReceiptsEndpoint_WithInvalidJpegHeader_HandlesGracefully()
    {
        // Arrange - Create data with invalid JPEG header
        var invalidJpegData = new byte[] { 0xFF, 0xD8, 0x00, 0x00 }; // Invalid JPEG (truncated)
        using var invalidContent = new ByteArrayContent(invalidJpegData);
        invalidContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", invalidContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(receiptResponse);
        // Should handle invalid image gracefully
        Assert.True(receiptResponse.Success || !string.IsNullOrEmpty(receiptResponse.Error));
    }

    #endregion

    #region Large Image Tests

    [Fact]
    public async Task ReceiptsEndpoint_WithLargeImage_HandlesWithinReasonableTime()
    {
        // Arrange - Create a large image (>10MB)
        var largeImageData = CreateLargeImageData(12 * 1024 * 1024); // 12MB
        using var largeContent = new ByteArrayContent(largeImageData);
        largeContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.PostAsync("/ocr/receipts", largeContent);

        // Assert
        stopwatch.Stop();
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(receiptResponse);
        
        // Should complete within reasonable time (adjust threshold as needed)
        Assert.True(stopwatch.ElapsedMilliseconds < 60000, $"Large image processing took too long: {stopwatch.ElapsedMilliseconds}ms");
        
        // Log processing time for monitoring
        Console.WriteLine($"Large image ({largeImageData.Length} bytes) processed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ChecksEndpoint_WithLargeImage_HandlesWithinReasonableTime()
    {
        // Arrange - Create a large image (>10MB)
        var largeImageData = CreateLargeImageData(11 * 1024 * 1024); // 11MB
        using var largeContent = new ByteArrayContent(largeImageData);
        largeContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.PostAsync("/ocr/checks", largeContent);

        // Assert
        stopwatch.Stop();
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(checkResponse);
        
        // Should complete within reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 60000, $"Large image processing took too long: {stopwatch.ElapsedMilliseconds}ms");
        
        Console.WriteLine($"Large image ({largeImageData.Length} bytes) processed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ReceiptsEndpoint_WithExtremelyLargeImage_ReturnsAppropriateResponse()
    {
        // Arrange - Create an extremely large image (50MB)
        var extremelyLargeImageData = CreateLargeImageData(50 * 1024 * 1024); // 50MB
        using var extremelyLargeContent = new ByteArrayContent(extremelyLargeImageData);
        extremelyLargeContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", extremelyLargeContent);

        // Assert
        // Should either succeed or fail gracefully (not crash the service)
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.BadRequest ||
                   response.StatusCode == HttpStatusCode.RequestEntityTooLarge);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
            Assert.NotNull(receiptResponse);
        }
        
        Console.WriteLine($"Extremely large image ({extremelyLargeImageData.Length} bytes) response: {response.StatusCode}");
    }

    #endregion

    #region Unsupported Image Format Tests

    [Fact]
    public async Task ReceiptsEndpoint_WithUnsupportedFormat_HandlesGracefully()
    {
        // Arrange - Create unsupported format data (e.g., .ico file)
        var unsupportedFormatData = CreateUnsupportedFormatData();
        using var unsupportedContent = new ByteArrayContent(unsupportedFormatData);
        unsupportedContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/x-icon");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", unsupportedContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(receiptResponse);
        // Should handle unsupported format gracefully
        Assert.True(receiptResponse.Success || !string.IsNullOrEmpty(receiptResponse.Error));
        
        Console.WriteLine($"Unsupported format result - Success: {receiptResponse.Success}, Error: {receiptResponse.Error}");
    }

    [Fact]
    public async Task ChecksEndpoint_WithTextFile_HandlesGracefully()
    {
        // Arrange - Send a text file instead of an image
        var textData = Encoding.UTF8.GetBytes("This is not an image file, just plain text content.");
        using var textContent = new ByteArrayContent(textData);
        textContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        // Act
        var response = await _client.PostAsync("/ocr/checks", textContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(checkResponse);
        // Should handle non-image data gracefully
        Assert.True(checkResponse.Success || !string.IsNullOrEmpty(checkResponse.Error));
    }

    [Fact]
    public async Task ReceiptsEndpoint_WithBinaryData_HandlesGracefully()
    {
        // Arrange - Send random binary data
        var randomData = new byte[1024];
        new Random(42).NextBytes(randomData); // Use seed for reproducible tests
        
        using var binaryContent = new ByteArrayContent(randomData);
        binaryContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", binaryContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(receiptResponse);
        // Should handle random binary data gracefully
        Assert.True(receiptResponse.Success || !string.IsNullOrEmpty(receiptResponse.Error));
    }

    #endregion

    #region Concurrent Request Handling Tests

    [Fact]
    public async Task ReceiptsEndpoint_ConcurrentRequests_HandlesMultipleRequestsSimultaneously()
    {
        // Arrange
        const int concurrentRequests = 5;
        var testImage = TestImageHelper.CreateValidJpegImage();

        // Act - Create tasks that handle their own content creation and disposal
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async _ =>
            {
                using var imageContent = new ByteArrayContent(testImage);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                return await _client.PostAsync("/ocr/receipts", imageContent);
            });

        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.All(responses, response => 
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        // Verify all responses are valid
        var responseContents = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        
        foreach (var responseJson in responseContents)
        {
            var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
            Assert.NotNull(receiptResponse);
            // Each response should be valid (either success or graceful failure)
            Assert.True(receiptResponse.Success || !string.IsNullOrEmpty(receiptResponse.Error));
        }

        Console.WriteLine($"Successfully processed {concurrentRequests} concurrent receipt requests");
    }

    [Fact]
    public async Task ChecksEndpoint_ConcurrentRequests_HandlesMultipleRequestsSimultaneously()
    {
        // Arrange
        const int concurrentRequests = 5;
        var testImage = TestImageHelper.CreateValidJpegImage();

        // Act - Create tasks that handle their own content creation and disposal
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async _ =>
            {
                using var imageContent = new ByteArrayContent(testImage);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                return await _client.PostAsync("/ocr/checks", imageContent);
            });

        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.All(responses, response => 
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        // Verify all responses are valid
        var responseContents = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        
        foreach (var responseJson in responseContents)
        {
            var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
            Assert.NotNull(checkResponse);
            // Each response should be valid (either success or graceful failure)
            Assert.True(checkResponse.Success || !string.IsNullOrEmpty(checkResponse.Error));
        }

        Console.WriteLine($"Successfully processed {concurrentRequests} concurrent check requests");
    }

    [Fact]
    public async Task MixedEndpoints_ConcurrentRequests_HandlesSimultaneouslyWithoutInterference()
    {
        // Arrange
        const int requestsPerEndpoint = 3;
        var testImage = TestImageHelper.CreateValidJpegImage();

        // Act - Create mixed tasks that handle their own content creation and disposal
        var receiptTasks = Enumerable.Range(0, requestsPerEndpoint)
            .Select(async _ =>
            {
                using var imageContent = new ByteArrayContent(testImage);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                return await _client.PostAsync("/ocr/receipts", imageContent);
            });

        var checkTasks = Enumerable.Range(0, requestsPerEndpoint)
            .Select(async _ =>
            {
                using var imageContent = new ByteArrayContent(testImage);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                return await _client.PostAsync("/ocr/checks", imageContent);
            });

        var allTasks = receiptTasks.Concat(checkTasks);
        var responses = await Task.WhenAll(allTasks);

        // Assert
        Assert.All(responses, response => 
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        Console.WriteLine($"Successfully processed {requestsPerEndpoint * 2} mixed concurrent requests");
    }

    [Fact]
    public async Task HighVolumeRequests_StressTest_MaintainsStability()
    {
        // Arrange
        const int highVolumeRequests = 10;
        var testImage = TestImageHelper.CreateValidJpegImage();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Create high volume tasks that handle their own content creation and disposal
        var tasks = Enumerable.Range(0, highVolumeRequests)
            .Select(async i =>
            {
                using var imageContent = new ByteArrayContent(testImage);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                
                // Alternate between endpoints
                var endpoint = i % 2 == 0 ? "/ocr/receipts" : "/ocr/checks";
                return await _client.PostAsync(endpoint, imageContent);
            });

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.All(responses, response => 
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        // Check that service remains responsive under load
        Assert.True(stopwatch.ElapsedMilliseconds < 120000, // 2 minutes max
            $"High volume processing took too long: {stopwatch.ElapsedMilliseconds}ms");

        Console.WriteLine($"Processed {highVolumeRequests} high-volume requests in {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates corrupted image data that appears to be an image but has invalid content
    /// </summary>
    private static byte[] CreateCorruptedImageData()
    {
        var corruptedData = new byte[1024];
        // Start with valid JPEG header
        corruptedData[0] = 0xFF;
        corruptedData[1] = 0xD8;
        corruptedData[2] = 0xFF;
        
        // Fill the rest with random data that doesn't follow JPEG format
        var random = new Random(42);
        random.NextBytes(corruptedData.AsSpan(3));
        
        return corruptedData;
    }

    /// <summary>
    /// Creates a large image data for testing memory and performance limits
    /// </summary>
    private static byte[] CreateLargeImageData(int sizeInBytes)
    {
        var largeData = new byte[sizeInBytes];
        
        // Create a valid PNG header
        var pngHeader = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk start
            0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x00, // 4096x4096 image
            0x08, 0x02, 0x00, 0x00, 0x00, 0x91, 0x5D, 0x05, 0x7E // Rest of IHDR
        };
        
        Array.Copy(pngHeader, largeData, Math.Min(pngHeader.Length, largeData.Length));
        
        // Fill the rest with repeating pattern to simulate compressed image data
        for (int i = pngHeader.Length; i < largeData.Length; i++)
        {
            largeData[i] = (byte)(i % 256);
        }
        
        return largeData;
    }

    /// <summary>
    /// Creates data in an unsupported format (ICO file)
    /// </summary>
    private static byte[] CreateUnsupportedFormatData()
    {
        // Create a minimal ICO file header
        return new byte[]
        {
            0x00, 0x00, // Reserved (must be 0)
            0x01, 0x00, // Image type (1 = ICO)
            0x01, 0x00, // Number of images
            0x10, 0x10, // Width and height (16x16)
            0x00, 0x00, // Color count and reserved
            0x01, 0x00, // Color planes
            0x01, 0x00, // Bits per pixel
            0x28, 0x00, 0x00, 0x00, // Size of image data
            0x16, 0x00, 0x00, 0x00  // Offset to image data
        };
    }

    #endregion
}