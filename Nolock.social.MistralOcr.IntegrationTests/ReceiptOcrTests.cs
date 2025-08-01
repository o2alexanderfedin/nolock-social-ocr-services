using FluentAssertions;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.IntegrationTests.Helpers;

namespace Nolock.social.MistralOcr.IntegrationTests;

public class ReceiptOcrTests : TestBase
{
    public ReceiptOcrTests(MistralOcrTestFixture fixture) : base(fixture)
    {
    }

    public static IEnumerable<object[]> ReceiptTestData =>
        new List<object[]>
        {
            new object[] { 1, "Extract all text from this receipt including store name, items, prices, and total" },
            new object[] { 2, "Extract all text from this receipt including store name, items, prices, and total" },
            new object[] { 3, "Extract all text from this receipt including store name, items, prices, and total" },
            new object[] { 4, "Extract all text from this receipt including store name, items, prices, and total" },
            new object[] { 5, "Extract all text from this receipt including store name, items, prices, and total" }
        };

    [Theory]
    [MemberData(nameof(ReceiptTestData))]
    public async Task ProcessReceipt_WithDataUrl_ShouldExtractText(int receiptNumber, string prompt)
    {
        // Arrange
        var dataUrl = TestImageHelper.GetReceiptImageDataUrl(receiptNumber);

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(dataUrl, prompt);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.Text.Length.Should().BeGreaterThan(50, "Receipt should contain substantial text");
        result.ModelUsed.Should().StartWith("mistral-ocr");
        // Note: OCR API may return 0 tokens in usage info
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ProcessReceipt_WithByteArray_ShouldExtractText(int receiptNumber)
    {
        // Arrange
        var imageBytes = TestImageHelper.GetReceiptImageBytes(receiptNumber);
        const string prompt = "Extract all visible text from this receipt";

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageBytesAsync(
            imageBytes, 
            "image/jpeg", 
            prompt);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ProcessReceipt_WithStream_ShouldExtractText(int receiptNumber)
    {
        // Arrange
        using var stream = TestImageHelper.GetReceiptImageStream(receiptNumber);
        const string prompt = "Read this receipt and extract all text";

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageStreamAsync(
            stream, 
            "image/jpeg", 
            prompt);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.Metadata.Should().NotBeNull();
    }

    public static IEnumerable<object[]> SpecificExtractionTestData =>
        new List<object[]>
        {
            new object[] { 1, "Extract only the total amount from this receipt", "total", "amount" },
            new object[] { 2, "Extract only the date from this receipt", "date", null! },
            new object[] { 3, "Extract only the store name from this receipt", "store", "name" },
            new object[] { 4, "List all items and their prices from this receipt", "item", "price" },
            new object[] { 5, "Extract the payment method from this receipt", "payment", "card" }
        };

    [Theory]
    [MemberData(nameof(SpecificExtractionTestData))]
    public async Task ProcessReceipt_WithSpecificExtraction_ShouldReturnTargetedInfo(
        int receiptNumber, 
        string prompt, 
        string expectedKeyword1,
        string? expectedKeyword2)
    {
        // Arrange
        var dataUrl = TestImageHelper.GetReceiptImageDataUrl(receiptNumber);

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(dataUrl, prompt);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        
        // The response should contain the expected keywords (case-insensitive)
        result.Text.ToLower().Should().Contain(expectedKeyword1.ToLower());
        if (expectedKeyword2 != null)
        {
            result.Text.ToLower().Should().Contain(expectedKeyword2.ToLower());
        }
    }

    [Theory]
    [InlineData(1, "Provide the extracted text in JSON format with fields: store, date, items, total")]
    [InlineData(2, "Extract receipt data and format as JSON with store name, items array, and total")]
    [InlineData(3, "Parse this receipt and return structured JSON data")]
    public async Task ProcessReceipt_WithStructuredOutput_ShouldReturnFormattedData(
        int receiptNumber, 
        string prompt)
    {
        // Arrange
        var dataUrl = TestImageHelper.GetReceiptImageDataUrl(receiptNumber);

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(dataUrl, prompt);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        
        // Check if response looks like JSON (contains braces)
        result.Text.Should().Contain("{");
        result.Text.Should().Contain("}");
    }

    [Fact]
    public async Task ProcessAllReceipts_InParallel_ShouldSucceed()
    {
        // Arrange
        var tasks = new List<Task<MistralOcrResult>>();
        
        for (int i = 1; i <= 5; i++)
        {
            var receiptNum = i;
            var dataUrl = TestImageHelper.GetReceiptImageDataUrl(receiptNum);
            var task = Fixture.MistralOcrService.ProcessImageDataUrlAsync(
                dataUrl, 
                $"Extract text from receipt {receiptNum}");
            tasks.Add(task);
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(result =>
        {
            result.Should().NotBeNull();
            result.Text.Should().NotBeNullOrWhiteSpace();
            result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        });
    }
}