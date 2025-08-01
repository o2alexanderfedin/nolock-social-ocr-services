using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nolock.social.MistralOcr.Extensions;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;

namespace Nolock.social.MistralOcr.IntegrationTests;

/// <summary>
/// Integration tests using real external URLs from Mistral cookbook
/// </summary>
public class ImageTransformationWithExternalUrlsTests : TestBase
{
    private readonly IImageUrlToDataUrlTransformer _transformer;
    private readonly IReactiveMistralOcrService _reactiveOcrService;
    
    // Real external URLs from Mistral cookbook
    private const string MistralPdfUrl = "https://raw.githubusercontent.com/mistralai/cookbook/refs/heads/main/mistral/ocr/mistral7b.pdf";
    private const string MistralReceiptUrl = "https://raw.githubusercontent.com/mistralai/cookbook/refs/heads/main/mistral/ocr/receipt.png";

    public ImageTransformationWithExternalUrlsTests(MistralOcrTestFixture fixture) : base(fixture)
    {
        // Register the image transformer with proper configuration
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddImageTransformation(options =>
        {
            options.MaxConcurrency = 2;
            options.RetryCount = 3;
            options.RequestTimeout = TimeSpan.FromSeconds(60);
            options.HttpTimeout = TimeSpan.FromMinutes(2);
        });

        var serviceProvider = services.BuildServiceProvider();
        _transformer = serviceProvider.GetRequiredService<IImageUrlToDataUrlTransformer>();
        
        // Get the reactive OCR service from the fixture
        _reactiveOcrService = new ReactiveMistralOcrService(
            Fixture.MistralOcrService,
            Fixture.ServiceProvider.GetRequiredService<ILogger<ReactiveMistralOcrService>>());
    }

    [Fact]
    public async Task TransformReceiptImage_FromGitHub_ShouldSucceed()
    {
        // Act - download and transform receipt image to data URL
        var dataUrl = await _transformer.TransformAsync(MistralReceiptUrl);
        
        // Assert
        dataUrl.Should().NotBeNullOrEmpty();
        dataUrl.Should().StartWith("data:image/png;base64,");
        
        // Verify it's a valid base64 string
        var base64Part = dataUrl.Substring("data:image/png;base64,".Length);
        var imageBytes = Convert.FromBase64String(base64Part);
        imageBytes.Length.Should().BeGreaterThan(1000); // Receipt image should be at least 1KB
    }

    [Fact]
    public async Task TransformPdfDocument_FromGitHub_ShouldSucceed()
    {
        // Act - download and transform PDF to data URL
        var dataUrl = await _transformer.TransformAsync(MistralPdfUrl);
        
        // Assert
        dataUrl.Should().NotBeNullOrEmpty();
        dataUrl.Should().StartWith("data:application/pdf;base64,");
        
        // Verify it's a valid base64 string
        var base64Part = dataUrl.Substring("data:application/pdf;base64,".Length);
        var pdfBytes = Convert.FromBase64String(base64Part);
        pdfBytes.Length.Should().BeGreaterThan(10000); // PDF should be at least 10KB
    }

    [Fact]
    public async Task ProcessReceiptImage_WithOCR_ShouldExtractText()
    {
        // Arrange
        var imageUrl = MistralReceiptUrl;
        
        // Act - transform URL to data URL and process with OCR
        var dataUrl = await _transformer.TransformAsync(imageUrl);
        var ocrResult = await Fixture.MistralOcrService.ProcessImageDataItemAsync(
            (new Uri(dataUrl), "image/png"), 
            "Extract all text from this receipt, including items, prices, and total");
        
        // Assert
        ocrResult.Should().NotBeNull();
        ocrResult.Text.Should().NotBeNullOrWhiteSpace();
        ocrResult.ModelUsed.Should().StartWith("mistral-ocr");
        ocrResult.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        
        // The receipt should contain some expected content
        var text = ocrResult.Text.ToLower();
        // Receipt typically contains numbers and text
        text.Should().MatchRegex(@"\d+"); // Should contain numbers
    }

    [Fact]
    public async Task ReactiveProcessing_MultipleExternalUrls_ShouldSucceed()
    {
        // Arrange - mix of external URLs and data URLs
        var imageUrls = new[]
        {
            MistralReceiptUrl,              // External PNG
            GetTestImageDataUrl(),          // Local data URL
            MistralReceiptUrl               // Same external PNG again (should use cached HttpClient)
        };

        // Act - use reactive pipeline with transformation
        var results = await imageUrls
            .ToObservable()
            .ProcessImagesWithTransform(
                _reactiveOcrService,
                _transformer,
                "Extract text from this image")
            .ToList();

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.ModelUsed.Should().StartWith("mistral-ocr");
            r.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
            r.Text.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task TransformWithErrors_MixedUrls_HandlesSuccessAndFailure()
    {
        // Arrange
        var mixedUrls = new[]
        {
            MistralReceiptUrl,                                    // Valid external URL
            "https://this-domain-does-not-exist-12345.com/img",  // Invalid domain
            GetTestImageDataUrl(),                                // Valid data URL
            "not-a-valid-url"                                    // Invalid URL format
        };

        // Act
        var results = await _transformer
            .TransformWithErrors(mixedUrls.ToObservable())
            .ToList();

        // Assert
        results.Should().HaveCount(4);
        
        // Find results by content rather than URL
        var successfulResults = results.Where(r => r.Success).ToList();
        var failedResults = results.Where(r => !r.Success).ToList();
        
        // Should have 2 successful (receipt and data URL) and 2 failed
        successfulResults.Should().HaveCount(2);
        failedResults.Should().HaveCount(2);
        
        // Check successful results
        var receiptResult = successfulResults.FirstOrDefault(r => r.OriginalUrl == MistralReceiptUrl);
        var dataUrlResult = successfulResults.FirstOrDefault(r => r.OriginalUrl.StartsWith("data:"));
        
        receiptResult.Should().NotBeNull("Receipt URL should have succeeded");
        receiptResult!.DataUrl.Should().StartWith("data:image/png;base64,");
        receiptResult.DetectedMimeType.Should().Be("image/png");
        receiptResult.ContentLength.Should().BeGreaterThan(0);
        
        dataUrlResult.Should().NotBeNull("Data URL should have succeeded");
        dataUrlResult!.DataUrl.Should().Be(dataUrlResult.OriginalUrl);
        
        // Check failed results
        var invalidDomainResult = failedResults.FirstOrDefault(r => r.OriginalUrl.Contains("this-domain-does-not-exist"));
        var invalidUrlResult = failedResults.FirstOrDefault(r => r.OriginalUrl == "not-a-valid-url");
        
        invalidDomainResult.Should().NotBeNull("Invalid domain should have failed");
        invalidDomainResult!.Error.Should().NotBeNull();
        
        invalidUrlResult.Should().NotBeNull("Invalid URL should have failed");
        invalidUrlResult!.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentDownloads_RespectsConcurrencyLimit()
    {
        // Arrange - multiple requests to the same external URL
        var urls = Enumerable.Repeat(MistralReceiptUrl, 5).ToList();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("ImageUrlToDataUrlTransformer");
        
        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        
        var transformer = new ImageUrlToDataUrlTransformer(
            httpClientFactory,
            Fixture.ServiceProvider.GetRequiredService<ILogger<ImageUrlToDataUrlTransformer>>(),
            maxConcurrency: 2); // Limit to 2 concurrent downloads

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await transformer
            .TransformWithErrors(urls.ToObservable())
            .ToList();
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.DataUrl.Should().StartWith("data:image/png;base64,");
        });
        
        // With concurrency limit of 2, downloading 5 files should take some time
        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task ProcessImagesWithTransformAndErrors_CompleteWorkflow()
    {
        // Arrange
        var imageUrls = new[]
        {
            MistralReceiptUrl,                    // Valid external URL
            GetTestImageDataUrl(),                // Valid data URL
            "https://invalid-url-12345.com/img"  // Invalid URL
        };

        // Act - complete workflow with error handling
        var results = await imageUrls
            .ToObservable()
            .ProcessImagesWithTransformAndErrors(
                _reactiveOcrService,
                _transformer,
                "Extract all visible text")
            .ToList();

        // Assert
        results.Should().HaveCount(3);
        
        // Find specific results by URL
        var receiptResult = results.First(r => r.OriginalUrl == MistralReceiptUrl);
        var dataUrlResult = results.First(r => r.OriginalUrl.StartsWith("data:"));
        var invalidResult = results.First(r => r.OriginalUrl.Contains("invalid-url"));
        
        // External URL should succeed completely
        receiptResult.Success.Should().BeTrue();
        receiptResult.TransformResult.Should().NotBeNull();
        receiptResult.TransformResult!.Success.Should().BeTrue();
        receiptResult.OcrResult.Should().NotBeNull();
        receiptResult.ExtractedText.Should().NotBeNullOrEmpty();
        
        // Data URL should succeed
        dataUrlResult.Success.Should().BeTrue();
        dataUrlResult.OcrResult.Should().NotBeNull();
        
        // Invalid URL should fail at transformation
        invalidResult.Success.Should().BeFalse();
        invalidResult.TransformResult!.Success.Should().BeFalse();
        invalidResult.OcrResult.Should().BeNull();
    }

    [Fact]
    public async Task MimeTypeDetection_ForVariousUrls_ShouldBeCorrect()
    {
        // Arrange
        var testUrls = new[]
        {
            (Url: MistralReceiptUrl, ExpectedMime: "image/png"),
            (Url: MistralPdfUrl, ExpectedMime: "application/pdf")
        };

        // Act & Assert
        foreach (var (url, expectedMime) in testUrls)
        {
            var results = await _transformer
                .TransformWithErrors(Observable.Return(url))
                .ToList();
            
            results.Should().HaveCount(1);
            var result = results[0];
            
            result.Success.Should().BeTrue($"Failed to transform {url}");
            result.DetectedMimeType.Should().Be(expectedMime);
            result.ContentLength.Should().BeGreaterThan(0);
            result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }
}