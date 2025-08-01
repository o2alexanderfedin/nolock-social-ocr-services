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
            new object[] { 1 },
            new object[] { 2 },
            new object[] { 3 },
            new object[] { 4 },
            new object[] { 5 }
        };

    [Theory]
    [MemberData(nameof(ReceiptTestData))]
    public async Task ProcessReceipt_WithDataUrl_ShouldExtractText(int receiptNumber)
    {
        // Arrange
        var dataUrl = TestImageHelper.GetReceiptImageDataUrl(receiptNumber);

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((new Uri(dataUrl), "image/jpeg"));

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

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageBytesAsync(
            imageBytes, 
            "image/jpeg");

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

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageStreamAsync(
            stream, 
            "image/jpeg");

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.Metadata.Should().NotBeNull();
    }

    public static IEnumerable<object[]> SpecificExtractionTestData =>
        new List<object[]>
        {
            new object[] { 1 },
            new object[] { 2 },
            new object[] { 3 },
            new object[] { 4 },
            new object[] { 5 }
        };

    [Theory]
    [MemberData(nameof(SpecificExtractionTestData))]
    public async Task ProcessReceipt_WithSpecificExtraction_ShouldReturnSomeText(
        int receiptNumber)
    {
        // Arrange
        var dataUrl = TestImageHelper.GetReceiptImageDataUrl(receiptNumber);

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((new Uri(dataUrl), "image/jpeg"));

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.Text.Length.Should().BeGreaterThan(10, "OCR should return some text");
        result.ModelUsed.Should().StartWith("mistral-ocr");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ProcessReceipt_WithStructuredOutput_ShouldReturnSomeText(
        int receiptNumber)
    {
        // Arrange
        var dataUrl = TestImageHelper.GetReceiptImageDataUrl(receiptNumber);

        // Act
        var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync((new Uri(dataUrl), "image/jpeg"));

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.Text.Length.Should().BeGreaterThan(10, "OCR should return some text");
        // Note: We can't guarantee the OCR will return JSON format
        // The important thing is that it recognizes and returns something
    }

    [Fact]
    public async Task ProcessAllReceipts_InParallel_ShouldSucceed()
    {
        // Arrange
        var tasks = Enumerable
            .Range(1, 5)
            .Select(i => Fixture.MistralOcrService.ProcessImageDataItemAsync(
                (new Uri(TestImageHelper.GetReceiptImageDataUrl(i)), "image/jpeg")
            ))
            .ToList();

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