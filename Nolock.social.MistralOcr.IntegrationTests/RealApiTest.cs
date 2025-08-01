using FluentAssertions;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.IntegrationTests.Helpers;

namespace Nolock.social.MistralOcr.IntegrationTests;

/// <summary>
/// This test actually calls the Mistral API - run it to verify the integration works
/// </summary>
public class RealApiTest : TestBase
{
    public RealApiTest(MistralOcrTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task MistralOcr_WithRealReceipt_ShouldExtractTextSuccessfully()
    {
        // Arrange - Use a real receipt image
        var receiptDataUrl = TestImageHelper.GetReceiptImageDataUrl(1);
        const string prompt = "Extract all text from this receipt including store name, items, prices, and total amount. Format the response clearly.";

        // Act - Actually call Mistral API
        var result = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(receiptDataUrl, prompt);

        // Assert - Verify we got a real response
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        
        // A real receipt OCR should return substantial text
        result.Text.Length.Should().BeGreaterThan(100, "Receipt OCR should return detailed text");
        
        // Verify metadata
        result.ModelUsed.Should().StartWith("pixtral");
        result.TotalTokens.Should().BeGreaterThan(200, "Receipt processing uses many tokens");
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(100));
        
        // The response should contain common receipt elements (case-insensitive)
        var lowerText = result.Text.ToLower();
        (lowerText.Contains("total") || 
         lowerText.Contains("amount") || 
         lowerText.Contains("price") ||
         lowerText.Contains("item") ||
         lowerText.Contains("$") ||
         lowerText.Contains("receipt"))
        .Should().BeTrue("Receipt OCR should identify receipt-related content");
        
        // Log the actual response for manual verification
        Console.WriteLine("=== Mistral OCR Response ===");
        Console.WriteLine($"Model: {result.ModelUsed}");
        Console.WriteLine($"Tokens: {result.TotalTokens}");
        Console.WriteLine($"Processing Time: {result.ProcessingTime.TotalMilliseconds}ms");
        Console.WriteLine($"Text Length: {result.Text.Length} characters");
        Console.WriteLine("=== Extracted Text ===");
        Console.WriteLine(result.Text);
        Console.WriteLine("=== End of Response ===");
    }

    [Fact]
    public async Task MistralOcr_WithSpecificReceiptExtraction_ShouldReturnStructuredData()
    {
        // Arrange - Use a real receipt with structured prompt
        var receiptDataUrl = TestImageHelper.GetReceiptImageDataUrl(2);
        const string structuredPrompt = @"Analyze this receipt and extract:
1. Store/Business name
2. Date of purchase
3. List of items with prices
4. Total amount
5. Payment method if visible

Format your response as a clear list.";

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(receiptDataUrl, structuredPrompt);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.Text.Length.Should().BeGreaterThan(50);
        
        // Log structured response
        Console.WriteLine("=== Structured Receipt Data ===");
        Console.WriteLine(result.Text);
        Console.WriteLine($"Tokens used: {result.TotalTokens}");
    }

    [Fact]
    public async Task MistralOcr_TestAllReceipts_ShouldSuccessfullyProcessEach()
    {
        // Test each receipt to ensure they all work
        for (int i = 1; i <= 5; i++)
        {
            // Arrange
            var receiptDataUrl = TestImageHelper.GetReceiptImageDataUrl(i);
            var prompt = $"Extract text from receipt #{i}. Include store name and total if visible.";

            // Act
            var result = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(receiptDataUrl, prompt);

            // Assert
            result.Should().NotBeNull();
            result.Text.Should().NotBeNullOrWhiteSpace();
            result.Text.Length.Should().BeGreaterThan(20, $"Receipt {i} should have meaningful content");
            
            Console.WriteLine($"\n=== Receipt {i} Summary ===");
            Console.WriteLine($"Text preview: {result.Text.Substring(0, Math.Min(100, result.Text.Length))}...");
            Console.WriteLine($"Total length: {result.Text.Length} chars, Tokens: {result.TotalTokens}");
        }
    }
}