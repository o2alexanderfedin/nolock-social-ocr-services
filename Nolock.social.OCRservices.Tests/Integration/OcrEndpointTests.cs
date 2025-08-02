using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nolock.social.OCRservices.Core.Models;
using Nolock.social.OCRservices.Tests.TestData;
using Xunit;

namespace Nolock.social.OCRservices.Tests.Integration;

public class OcrEndpointTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public OcrEndpointTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public async Task ReceiptsEndpoint_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(TestImageHelper.GetReceiptImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(receiptResponse);
        Assert.NotNull(receiptResponse.OcrText);
        // The OCR should return some text (could be minimal for real-world images)
        Assert.True(receiptResponse.OcrText.Length > 0, $"OCR text is empty: '{receiptResponse.OcrText}'");
        // Log the actual OCR result for debugging
        Console.WriteLine($"OCR extracted text: '{receiptResponse.OcrText}' ({receiptResponse.OcrText.Length} chars)");
    }

    [Fact]
    public async Task ChecksEndpoint_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(TestImageHelper.GetCheckImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/checks", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(checkResponse);
        Assert.NotNull(checkResponse.OcrText);
        // The OCR should return some text (could be minimal for real-world images)
        Assert.True(checkResponse.OcrText.Length > 0, $"OCR text is empty: '{checkResponse.OcrText}'");
        // Log the actual OCR result for debugging
        Console.WriteLine($"OCR extracted text: '{checkResponse.OcrText}' ({checkResponse.OcrText.Length} chars)");
    }

    [Fact]
    public async Task InvalidEndpoint_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/invoices", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}