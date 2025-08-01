using System.Net;
using System.Net.Http.Json;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nolock.social.CloudflareAI;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.Services;
using Nolock.social.MistralOcr;
using Nolock.social.OCRservices.Core.Models;
using Xunit;

namespace Nolock.social.OCRservices.Tests.Integration;

public class OcrAsyncEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public OcrAsyncEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    [Theory]
    [InlineData("check")]
    [InlineData("receipt")]
    public async Task PostAsync_WithValidDocumentType_ReturnsOk(string documentType)
    {
        // Arrange
        var mockOcrService = new Mock<IReactiveMistralOcrService>();
        var mockExtractionService = new Mock<OcrExtractionService>(
            Mock.Of<IWorkersAI>(), 
            Mock.Of<ILogger<OcrExtractionService>>());

        var ocrResult = new MistralOcrResult
        {
            Text = "Sample OCR text"
        };

        mockOcrService.Setup(x => x.ProcessImageDataItems(It.IsAny<IObservable<(string url, string mimeType)>>()))
            .Returns(Observable.Return(ocrResult));

        var extractionResponse = documentType == "check" 
            ? new OcrExtractionResponse<object>
            {
                Success = true,
                DocumentType = DocumentType.Check,
                Data = new Check { Amount = 100.50m, CheckNumber = "1234" },
                Confidence = 0.90,
                ProcessingTimeMs = 100
            }
            : new OcrExtractionResponse<object>
            {
                Success = true,
                DocumentType = DocumentType.Receipt,
                Data = new Receipt 
                { 
                    Totals = new ReceiptTotals { Total = 59.99m },
                    Merchant = new MerchantInfo { Name = "Test Store" }
                },
                Confidence = 0.92,
                ProcessingTimeMs = 120
            };

        mockExtractionService
            .Setup(x => x.ProcessExtractionRequestAsync(It.IsAny<OcrExtractionRequest>()))
            .ReturnsAsync(extractionResponse);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(mockOcrService.Object);
                services.AddScoped(_ => mockExtractionService.Object);
            });
        }).CreateClient();

        using var imageContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }); // JPEG header
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync($"/ocr/async?documentType={documentType}", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseData = await response.Content.ReadFromJsonAsync<OcrAsyncResponse>(_jsonOptions);
        Assert.NotNull(responseData);
        Assert.Equal(documentType, responseData.DocumentType);
        Assert.NotNull(responseData.OcrText);
        Assert.NotNull(responseData.ExtractedData);
        Assert.True(responseData.Confidence > 0);
        Assert.True(responseData.ProcessingTimeMs > 0);
    }

    [Fact]
    public async Task PostAsync_WithoutDocumentType_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF });
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/async", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Document type is required", content);
    }

    [Fact]
    public async Task PostAsync_WithInvalidDocumentType_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF });
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/async?documentType=invoice", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid document type", content);
    }

    [Fact]
    public async Task PostAsync_HandlesReceiptWithDecimalAmounts()
    {
        // Arrange
        var mockOcrService = new Mock<IReactiveMistralOcrService>();
        var mockExtractionService = new Mock<OcrExtractionService>(
            Mock.Of<IWorkersAI>(), 
            Mock.Of<ILogger<OcrExtractionService>>());

        var ocrResult = new MistralOcrResult
        {
            Text = "Store Receipt\nItem 1 $10.99\nItem 2 $5.50\nSubtotal: $16.49\nTax: $1.32\nTotal: $17.81"
        };

        mockOcrService.Setup(x => x.ProcessImageDataItems(It.IsAny<IObservable<(string url, string mimeType)>>()))
            .Returns(Observable.Return(ocrResult));

        var receipt = new Receipt
        {
            Merchant = new MerchantInfo { Name = "Store" },
            Items = new List<ReceiptLineItem>
            {
                new() { Description = "Item 1", TotalPrice = 10.99m },
                new() { Description = "Item 2", TotalPrice = 5.50m }
            },
            Totals = new ReceiptTotals
            {
                Subtotal = 16.49m,
                Tax = 1.32m,
                Total = 17.81m
            }
        };

        var extractionResponse = new OcrExtractionResponse<object>
        {
            Success = true,
            DocumentType = DocumentType.Receipt,
            Data = receipt,
            Confidence = 0.95,
            ProcessingTimeMs = 150
        };

        mockExtractionService
            .Setup(x => x.ProcessExtractionRequestAsync(It.IsAny<OcrExtractionRequest>()))
            .ReturnsAsync(extractionResponse);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(mockOcrService.Object);
                services.AddScoped(_ => mockExtractionService.Object);
            });
        }).CreateClient();

        using var imageContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF });
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/async?documentType=receipt", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<JsonDocument>(responseJson, _jsonOptions);
        
        Assert.NotNull(responseData);
        var extractedData = responseData.RootElement.GetProperty("extractedData");
        var totals = extractedData.GetProperty("totals");
        
        // Verify decimal values are properly serialized
        Assert.Equal(16.49m, totals.GetProperty("subtotal").GetDecimal());
        Assert.Equal(1.32m, totals.GetProperty("tax").GetDecimal());
        Assert.Equal(17.81m, totals.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task PostAsync_HandlesCheckWithDecimalAmount()
    {
        // Arrange
        var mockOcrService = new Mock<IReactiveMistralOcrService>();
        var mockExtractionService = new Mock<OcrExtractionService>(
            Mock.Of<IWorkersAI>(), 
            Mock.Of<ILogger<OcrExtractionService>>());

        var ocrResult = new MistralOcrResult
        {
            Text = "PAY TO: John Doe\nAMOUNT: $1,234.56\nCHECK #: 5678"
        };

        mockOcrService.Setup(x => x.ProcessImageDataItems(It.IsAny<IObservable<(string url, string mimeType)>>()))
            .Returns(Observable.Return(ocrResult));

        var check = new Check
        {
            CheckNumber = "5678",
            Payee = "John Doe",
            Amount = 1234.56m,
            IsValidInput = true
        };

        var extractionResponse = new OcrExtractionResponse<object>
        {
            Success = true,
            DocumentType = DocumentType.Check,
            Data = check,
            Confidence = 0.93,
            ProcessingTimeMs = 110
        };

        mockExtractionService
            .Setup(x => x.ProcessExtractionRequestAsync(It.IsAny<OcrExtractionRequest>()))
            .ReturnsAsync(extractionResponse);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(mockOcrService.Object);
                services.AddScoped(_ => mockExtractionService.Object);
            });
        }).CreateClient();

        using var imageContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF });
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/async?documentType=check", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<JsonDocument>(responseJson, _jsonOptions);
        
        Assert.NotNull(responseData);
        var extractedData = responseData.RootElement.GetProperty("extractedData");
        
        // Verify decimal amount is properly serialized
        Assert.Equal(1234.56m, extractedData.GetProperty("amount").GetDecimal());
        Assert.Equal("5678", extractedData.GetProperty("checkNumber").GetString());
        Assert.Equal("John Doe", extractedData.GetProperty("payee").GetString());
    }

    [Fact]
    public async Task PostAsync_WhenExtractionFails_ReturnsInternalServerError()
    {
        // Arrange
        var mockOcrService = new Mock<IReactiveMistralOcrService>();
        var mockExtractionService = new Mock<OcrExtractionService>(
            Mock.Of<IWorkersAI>(), 
            Mock.Of<ILogger<OcrExtractionService>>());

        var ocrResult = new MistralOcrResult
        {
            Text = "Some text"
        };

        mockOcrService.Setup(x => x.ProcessImageDataItems(It.IsAny<IObservable<(string url, string mimeType)>>()))
            .Returns(Observable.Return(ocrResult));

        var extractionResponse = new OcrExtractionResponse<object>
        {
            Success = false,
            Error = "Failed to extract structured data",
            DocumentType = DocumentType.Receipt
        };

        mockExtractionService
            .Setup(x => x.ProcessExtractionRequestAsync(It.IsAny<OcrExtractionRequest>()))
            .ReturnsAsync(extractionResponse);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(mockOcrService.Object);
                services.AddScoped(_ => mockExtractionService.Object);
            });
        }).CreateClient();

        using var imageContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF });
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/async?documentType=receipt", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Failed to extract receipt data", content);
    }
}