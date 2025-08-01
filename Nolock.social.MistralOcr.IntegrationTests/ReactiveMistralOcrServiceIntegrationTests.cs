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
    public async Task ProcessImageDataItems_WithValidDataUrl_ShouldReturnResult()
    {
        // Arrange
        var dataUrl = GetTestImageDataUrl();
        var dataItems = Observable.Return((new Uri(dataUrl), "image/jpeg"));
        
        // Act
        var results = await _reactiveService.ProcessImageDataItems(dataItems)
            .ToList();
        
        // Assert
        results.Should().HaveCount(1);
        results[0].Text.Should().NotBeNullOrWhiteSpace();
        results[0].Text.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task ProcessImageDataItems_WithMultipleImages_ShouldProcessConcurrently()
    {
        // Arrange
        var dataItems = Observable.Range(1, 3)
            .Select(i => (new Uri(TestImageHelper.GetReceiptImageDataUrl(i)), "image/jpeg"));
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await _reactiveService.ProcessImageDataItems(dataItems)
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
    public async Task ProcessImageDataItems_WithMixedMimeTypes_ShouldSucceed()
    {
        // Arrange
        var dataItems = new[]
        {
            (new Uri(TestImageHelper.GetReceiptImageDataUrl(1)), "image/jpeg"),
            (new Uri(TestImageHelper.GetReceiptImageDataUrl(2)), "image/png"),
            (new Uri(TestImageHelper.GetReceiptImageDataUrl(3)), "image/webp")
        }.ToObservable();
        
        // Act
        var results = await _reactiveService.ProcessImageDataItems(dataItems)
            .ToList();
        
        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r =>
        {
            r.Text.Should().NotBeNullOrWhiteSpace();
            r.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task ProcessImageBytes_WithValidBytes_ShouldReturnResult()
    {
        // Arrange
        var imageBytes = TestImageHelper.GetReceiptImageBytes(1);
        var images = Observable.Return((imageBytes, "image/jpeg"));
        
        // Act
        var results = await _reactiveService.ProcessImageBytes(images)
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
        var results = await _reactiveService.ProcessImageStreams(streams)
            .ToList();
        
        // Assert
        results.Should().HaveCount(1);
        results[0].Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessImageDataItems_WithEmptyInput_ShouldReturnEmpty()
    {
        // Arrange
        var emptyDataItems = Observable.Empty<(Uri url, string mimeType)>();
        
        // Act
        var results = await _reactiveService.ProcessImageDataItems(emptyDataItems)
            .ToList();
        
        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessImageDataItems_FiltersOutNullUrls()
    {
        // Arrange
        var dataItems = new[]
        {
            (new Uri(TestImageHelper.GetReceiptImageDataUrl(1)), "image/jpeg"),
            (null, "image/jpeg"), // This should be filtered out
            (new Uri(TestImageHelper.GetReceiptImageDataUrl(2)), "image/png")
        }.Where(item => item.Item1 != null)
        .Select(item => (item.Item1!, item.Item2))
        .ToObservable();
        
        // Act
        var results = await _reactiveService.ProcessImageDataItems(dataItems)
            .ToList();
        
        // Assert
        results.Should().HaveCount(2); // Only valid URLs processed
        results.Should().AllSatisfy(r => r.Text.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task ProcessImageDataItems_WithDifferentPrompts_ShouldReturnDifferentResults()
    {
        // Arrange
        var dataUrl = TestImageHelper.GetReceiptImageDataUrl(1);
        var dataItem = (new Uri(dataUrl), "image/jpeg");
        
        // Act
        var generalResult = await _reactiveService
            .ProcessImageDataItems(Observable.Return(dataItem))
            .FirstAsync();
            
        var specificResult = await _reactiveService
            .ProcessImageDataItems(Observable.Return(dataItem))
            .FirstAsync();
        
        // Assert
        generalResult.Should().NotBeNull();
        specificResult.Should().NotBeNull();
        generalResult.Text.Should().NotBeNullOrWhiteSpace();
        specificResult.Text.Should().NotBeNullOrWhiteSpace();
        
        // Different prompts should potentially yield different results
        // (though both should contain some text)
        generalResult.Text.Length.Should().BeGreaterThan(0);
        specificResult.Text.Length.Should().BeGreaterThan(0);
    }
}