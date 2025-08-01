using System.Text;
using System.Text.Json;
using FluentAssertions;
using Nolock.social.MistralOcr.Models;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.IntegrationTests.Helpers;

namespace Nolock.social.MistralOcr.IntegrationTests;

/// <summary>
/// Tests that simulate how the service would be used from an API endpoint
/// </summary>
public class MistralOcrEndpointTests : TestBase
{
    public MistralOcrEndpointTests(MistralOcrTestFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task MistralOcrEndpoint_WithReceiptDataUrl_ShouldReturnOcrResult(int receiptNumber)
    {
        // Arrange - Simulate endpoint request with real receipt
        var request = new MistralOcrEndpointRequest
        {
            ImageDataUrl = TestImageHelper.GetReceiptImageDataUrl(receiptNumber),
            Prompt = "Extract all text from this receipt"
        };

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(
            request.ImageDataUrl,
            request.Prompt ?? "Extract text from image");

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.Text.Length.Should().BeGreaterThan(20);
        result.TotalTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MistralOcrEndpoint_WithNullImageUrl_ShouldThrow()
    {
        // Arrange
        var request = new MistralOcrEndpointRequest
        {
            Prompt = "Extract text"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await Fixture.MistralOcrService.ProcessImageAsync(
                request.ImageUrl!, 
                request.Prompt ?? "Extract text from image")
        );
    }

    [Fact]
    public async Task MistralOcrEndpoint_WithNullDataUrl_ShouldThrow()
    {
        // Arrange
        var request = new MistralOcrEndpointRequest
        {
            Prompt = "Extract text"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await Fixture.MistralOcrService.ProcessImageDataUrlAsync(
                request.ImageDataUrl!, 
                request.Prompt ?? "Extract text from image")
        );
    }

    [Theory]
    [InlineData(1, "test-receipt-1.jpg")]
    [InlineData(2, "test-receipt-2.jpg")]
    [InlineData(3, "test-receipt-3.jpg")]
    public async Task MistralOcrEndpoint_WithReceiptFile_ShouldProcessSuccessfully(
        int receiptNumber, 
        string fileName)
    {
        // Arrange - Simulate file upload scenario with real receipt
        var imageBytes = TestImageHelper.GetReceiptImageBytes(receiptNumber);
        const string contentType = "image/jpeg";

        // Act - Process as endpoint would handle file upload
        using var stream = new MemoryStream(imageBytes);
        var result = await Fixture.MistralOcrService.ProcessImageStreamAsync(
            stream, 
            contentType, 
            $"Process the uploaded receipt file: {fileName}");

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        
        // Receipt processing should generate substantial response
        result.Text.Length.Should().BeGreaterThan(50);
    }

    public static IEnumerable<object[]> MultipleReceiptTestData =>
        new List<object[]>
        {
            new object[] { new[] { 1, 2, 3 }, "Extract store names from these receipts" },
            new object[] { new[] { 2, 4 }, "Compare the totals from these receipts" },
            new object[] { new[] { 1, 5 }, "Identify payment methods used" }
        };

    [Theory]
    [MemberData(nameof(MultipleReceiptTestData))]
    public async Task MistralOcrEndpoint_ProcessMultipleReceipts_ShouldHandleBatch(
        int[] receiptNumbers, 
        string prompt)
    {
        // Arrange - Simulate batch processing scenario
        var tasks = receiptNumbers.Select(async num =>
        {
            var dataUrl = TestImageHelper.GetReceiptImageDataUrl(num);
            return await Fixture.MistralOcrService.ProcessImageDataUrlAsync(dataUrl, prompt);
        }).ToList();

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(receiptNumbers.Length);
        results.Should().AllSatisfy(result =>
        {
            result.Should().NotBeNull();
            result.Text.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task MistralOcrEndpoint_WithDefaultPrompt_ShouldUseDefaultText()
    {
        // Arrange - Test with real receipt but no prompt
        var request = new MistralOcrEndpointRequest
        {
            ImageDataUrl = TestImageHelper.GetReceiptImageDataUrl(1)
            // No prompt provided
        };

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(
            request.ImageDataUrl, 
            request.Prompt); // null prompt will use default

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
    }
}