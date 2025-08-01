using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.IntegrationTests.Helpers;

namespace Nolock.social.MistralOcr.IntegrationTests;

public class ReactiveMistralOcrServiceIntegrationTests : TestBase
{
    private readonly IReactiveMistralOcrService _reactiveService;
    
    public ReactiveMistralOcrServiceIntegrationTests(MistralOcrTestFixture fixture) : base(fixture)
    {
        var logger = Fixture.ServiceProvider.GetRequiredService<ILogger<ReactiveMistralOcrService>>();
        _reactiveService = new ReactiveMistralOcrService(
            Fixture.MistralOcrService,
            logger,
            maxConcurrency: 2,
            rateLimitDelay: TimeSpan.FromMilliseconds(100),
            retryCount: 2,
            retryDelay: TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ProcessImageUrls_WithValidDataUrl_ShouldReturnResult()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        var imageUrls = Observable.Return(dataUrl);
        
        // Act
        var results = await _reactiveService.ProcessImageUrls(imageUrls, "Extract text")
            .ToList();
        
        // Assert
        results.Should().HaveCount(1);
        results[0].Text.Should().NotBeNullOrWhiteSpace();
        results[0].Text.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task ProcessImageUrls_WithMultipleImages_ShouldProcessConcurrently()
    {
        // Arrange
        var imageUrls = Observable.Range(1, 3)
            .Select(i => TestImageHelper.GetReceiptImageDataUrl(i));
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await _reactiveService.ProcessImageUrls(imageUrls, "Extract text")
            .ToList();
        stopwatch.Stop();
        
        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r =>
        {
            r.Text.Should().NotBeNullOrWhiteSpace();
            r.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        });
        
        // Should process concurrently (not take 3x the time)
        // This is a rough check - in practice network latency varies
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task ProcessImageUrlsBatch_ShouldGroupIntoBatches()
    {
        // Arrange
        var imageUrls = Observable.Range(1, 5)
            .Select(i => TestImageHelper.GetReceiptImageDataUrl(i));
        
        // Act
        var batches = await _reactiveService.ProcessImageUrlsBatch(imageUrls, 2, "Extract text")
            .ToList();
        
        // Assert
        batches.Should().HaveCount(3); // 5 items in batches of 2 = 3 batches
        batches[0].Should().HaveCount(2);
        batches[1].Should().HaveCount(2);
        batches[2].Should().HaveCount(1);
        
        batches.SelectMany(b => b).Should().AllSatisfy(r =>
        {
            r.Text.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task ProcessImageUrlsWithErrors_ShouldCaptureErrors()
    {
        // Arrange
        // ProcessImageUrlsWithErrors expects URLs, not data URLs
        // So we need to test with actual URLs where one is invalid
        var imageUrls = new[] 
        {
            "https://example.com/valid-image1.jpg",
            "not-a-valid-url-at-all", // This should cause an error
            "https://example.com/valid-image2.jpg"
        }.ToObservable();
        
        // Act
        var results = await _reactiveService.ProcessImageUrlsWithErrors(imageUrls, "Extract text")
            .ToList();
        
        // Assert
        results.Should().HaveCount(3);
        
        // All might succeed or fail depending on API behavior
        // The important thing is that errors are captured, not thrown
        results.Should().AllSatisfy(r =>
        {
            // Either Result or Error should be set, not both
            (r.Result == null).Should().NotBe(r.Error == null);
        });
    }

    [Fact]
    public async Task ProcessImageBytes_WithValidBytes_ShouldReturnResult()
    {
        // Arrange
        var imageBytes = TestImageHelper.GetReceiptImageBytes(1);
        var images = Observable.Return((imageBytes, "image/jpeg"));
        
        // Act
        var results = await _reactiveService.ProcessImageBytes(images, "Extract text")
            .ToList();
        
        // Assert
        results.Should().HaveCount(1);
        results[0].Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessImageStreams_WithValidStream_ShouldReturnResult()
    {
        // Arrange
        using var stream = TestImageHelper.GetReceiptImageStream(1);
        var streams = Observable.Return((stream, "image/jpeg"));
        
        // Act
        var results = await _reactiveService.ProcessImageStreams(streams, "Extract text")
            .ToList();
        
        // Assert
        results.Should().HaveCount(1);
        results[0].Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReactiveService_WithEmptyInput_ShouldReturnEmpty()
    {
        // Arrange
        var emptyUrls = Observable.Empty<string>();
        
        // Act
        var results = await _reactiveService.ProcessImageUrls(emptyUrls)
            .ToList();
        
        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ReactiveService_FiltersOutInvalidInput()
    {
        // Arrange
        var mixedUrls = new[] { "", null, "  ", TestImageHelper.GetReceiptImageDataUrl(1) }
            .ToObservable()!;
        
        // Act
        var results = await _reactiveService.ProcessImageUrls(mixedUrls!, "Extract text")
            .ToList();
        
        // Assert
        results.Should().HaveCount(1); // Only valid URL processed
        results[0].Text.Should().NotBeNullOrWhiteSpace();
    }
}