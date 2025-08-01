using FluentAssertions;
using Microsoft.Extensions.Logging;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.IntegrationTests.Helpers;
using Xunit.Abstractions;

namespace Nolock.social.MistralOcr.IntegrationTests;

public class RealOcrApiTest : TestBase
{
    private readonly ITestOutputHelper _output;

    public RealOcrApiTest(MistralOcrTestFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    [Fact]
    public async Task MistralOcr_WithRealReceipt_ShouldExtractTextSuccessfully()
    {
        // Arrange
        var dataUrl = TestImageHelper.GetReceiptImageDataUrl(1);
        
        // Act
        _output.WriteLine("Calling Mistral OCR API with receipt image...");
        var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync(
            (dataUrl, "image/jpeg"));

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.ModelUsed.Should().StartWith("mistral-ocr");
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        
        // Log the results
        _output.WriteLine($"Model Used: {result.ModelUsed}");
        _output.WriteLine($"Processing Time: {result.ProcessingTime.TotalMilliseconds}ms");
        _output.WriteLine($"Total Tokens: {result.TotalTokens}");
        _output.WriteLine($"Metadata: {string.Join(", ", result.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        _output.WriteLine($"\nExtracted Text:\n{result.Text}");
        
        // Verify content quality
        result.Text.Length.Should().BeGreaterThan(100, "Receipt should contain substantial text");
        // Note: OCR API may return 0 tokens in usage info
    }
}