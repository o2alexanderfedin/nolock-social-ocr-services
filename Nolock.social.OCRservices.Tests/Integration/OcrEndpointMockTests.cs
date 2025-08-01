using System.Net;
using System.Net.Http.Json;
using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.MistralOcr;
using Nolock.social.OCRservices.Core.Models;
using Nolock.social.OCRservices.Tests.TestData;
using Xunit;

namespace Nolock.social.OCRservices.Tests.Integration;

public class OcrEndpointMockTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public OcrEndpointMockTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    [Theory]
    [InlineData("receipt")]
    [InlineData("check")]
    public async Task PostAsync_WithValidDocumentType_ReturnsOk(string documentType)
    {
        // Arrange
        var mockOcrService = new Mock<IReactiveMistralOcrService>();
        
        var ocrResult = new MistralOcrResult
        {
            Text = documentType == "check" ? TestImageHelper.CreateTestCheckText() : TestImageHelper.CreateTestReceiptText(),
            ModelUsed = "mistral-ocr-latest",
            TotalTokens = 100
        };

        mockOcrService.Setup(x => x.ProcessImageDataItems(It.IsAny<IObservable<(string url, string mimeType)>>()))
            .Returns(Observable.Return(ocrResult));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing service and add our mock
                var serviceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IReactiveMistralOcrService));
                if (serviceDescriptor != null)
                {
                    services.Remove(serviceDescriptor);
                }
                services.AddSingleton(mockOcrService.Object);
            });
        }).CreateClient();

        using var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var endpoint = documentType == "check" ? "/ocr/checks" : "/ocr/receipts";
        var response = await client.PostAsync(endpoint, imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        if (documentType == "check")
        {
            var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
            Assert.NotNull(checkResponse);
            Assert.NotNull(checkResponse.OcrText);
            Assert.Contains("John Doe", checkResponse.OcrText);
            Assert.Equal("mistral-ocr-latest", checkResponse.ModelUsed);
            Assert.Equal(100, checkResponse.TotalTokens);
        }
        else
        {
            var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
            Assert.NotNull(receiptResponse);
            Assert.NotNull(receiptResponse.OcrText);
            Assert.Contains("WALMART", receiptResponse.OcrText);
            Assert.Equal("mistral-ocr-latest", receiptResponse.ModelUsed);
            Assert.Equal(100, receiptResponse.TotalTokens);
        }
    }

    [Fact]
    public async Task PostAsync_WhenOcrFails_ReturnsErrorResponse()
    {
        // Arrange
        var mockOcrService = new Mock<IReactiveMistralOcrService>();
        
        mockOcrService.Setup(x => x.ProcessImageDataItems(It.IsAny<IObservable<(string url, string mimeType)>>()))
            .Returns(Observable.Throw<MistralOcrResult>(new InvalidOperationException("OCR service unavailable")));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var serviceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IReactiveMistralOcrService));
                if (serviceDescriptor != null)
                {
                    services.Remove(serviceDescriptor);
                }
                services.AddSingleton(mockOcrService.Object);
            });
        }).CreateClient();

        using var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receiptResponse = await response.Content.ReadFromJsonAsync<ReceiptOcrResponse>(_jsonOptions);
        Assert.NotNull(receiptResponse);
        Assert.False(receiptResponse.Success);
        Assert.NotNull(receiptResponse.Error);
    }
}