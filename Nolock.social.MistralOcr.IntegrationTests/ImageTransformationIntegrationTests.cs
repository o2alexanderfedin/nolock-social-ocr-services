using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nolock.social.MistralOcr.Extensions;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;

namespace Nolock.social.MistralOcr.IntegrationTests;

/// <summary>
/// Integration tests demonstrating image URL to data URL transformation with OCR processing
/// </summary>
public class ImageTransformationIntegrationTests : TestBase
{
    private readonly IImageUrlToDataUrlTransformer _transformer;
    private readonly IReactiveMistralOcrService _reactiveOcrService;

    public ImageTransformationIntegrationTests(MistralOcrTestFixture fixture) : base(fixture)
    {
        // Register the image transformer
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddImageTransformation(options =>
        {
            options.MaxConcurrency = 2;
            options.RetryCount = 2;
            options.RequestTimeout = TimeSpan.FromSeconds(30);
        });

        var serviceProvider = services.BuildServiceProvider();
        _transformer = serviceProvider.GetRequiredService<IImageUrlToDataUrlTransformer>();
        
        // Get the reactive OCR service from the fixture
        _reactiveOcrService = new ReactiveMistralOcrService(
            Fixture.MistralOcrService,
            Fixture.ServiceProvider.GetRequiredService<ILogger<ReactiveMistralOcrService>>());
    }

    [Fact]
    public async Task TransformAndProcessImage_WithDataUrl_ShouldPassThrough()
    {
        // Arrange - use an existing data URL
        var dataUrl = GetTestImageDataUrl();
        
        // Act - transform should pass through data URLs unchanged
        var transformedUrl = await _transformer.TransformAsync(dataUrl);
        
        // Assert
        transformedUrl.Should().Be(dataUrl);
        
        // Process through OCR
        var ocrResult = await Fixture.MistralOcrService.ProcessImageDataUrlAsync(
            transformedUrl, 
            "Extract any text from this image");
        
        ocrResult.Should().NotBeNull();
        ocrResult.ModelUsed.Should().StartWith("mistral-ocr");
    }

    [Fact]
    public async Task ReactiveTransformAndProcess_WithMultipleDataUrls_ShouldProcessAll()
    {
        // Arrange - use data URLs only to avoid network calls in tests
        var imageUrls = new[]
        {
            GetTestImageDataUrl(), // Already a data URL
            GetTestImageDataUrl(), // Another data URL
            GetTestImageDataUrl()  // Another data URL
        };

        // Act - use reactive pipeline with transformation
        var results = await imageUrls
            .ToObservable()
            .ProcessImagesWithTransform(
                _reactiveOcrService,
                _transformer,
                "Extract any visible text")
            .ToList();

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.ModelUsed.Should().StartWith("mistral-ocr");
            r.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task TransformWithErrors_HandlesDataUrlPassthrough()
    {
        // Arrange - only test data URL passthrough to avoid network issues
        var dataUrls = new[]
        {
            GetTestImageDataUrl(),
            GetTestImageDataUrl()
        };

        // Act
        var results = await _transformer
            .TransformWithErrors(dataUrls.ToObservable())
            .ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.DataUrl.Should().Be(r.OriginalUrl);
            r.ProcessingTime.Should().Be(TimeSpan.Zero); // Data URLs are passed through immediately
        });
    }

    [Fact]
    public async Task ProcessImagesWithTransform_DataUrlsOnly()
    {
        // Arrange - use only data URLs to avoid network dependencies
        var imageUrls = new[]
        {
            GetTestImageDataUrl(),
            GetTestImageDataUrl()
        };

        // Act - process through transform and OCR
        var results = await imageUrls
            .ToObservable()
            .ProcessImagesWithTransform(
                _reactiveOcrService,
                _transformer,
                "Extract text from image")
            .ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.ModelUsed.Should().StartWith("mistral-ocr");
            r.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task ConcurrentTransformation_ProcessesDataUrlsEfficiently()
    {
        // Arrange - use data URLs which are processed instantly
        var urls = Enumerable.Range(1, 10)
            .Select(_ => GetTestImageDataUrl())
            .ToList();

        // Act
        var results = await _transformer
            .TransformWithErrors(urls.ToObservable())
            .ToList();

        // Assert
        results.Should().HaveCount(10);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.DataUrl.Should().Be(r.OriginalUrl);
            r.ProcessingTime.Should().Be(TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task ExampleUsage_DataUrlProcessing()
    {
        // This example shows processing of data URLs which are passed through

        // Input: Data URLs only
        var imageSources = new[]
        {
            GetTestImageDataUrl(),
            GetTestImageDataUrl(),
            GetTestImageDataUrl()
        };

        // Process all images through transformation and OCR
        var processedResults = await imageSources
            .ToObservable()
            .ProcessImagesWithTransform(
                _reactiveOcrService,
                _transformer,
                "Extract text from image")
            .ToList();

        // Assertions
        processedResults.Should().HaveCount(3);
        processedResults.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.ModelUsed.Should().StartWith("mistral-ocr");
        });
    }
}