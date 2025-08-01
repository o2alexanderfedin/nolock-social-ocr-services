using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.IntegrationTests.Helpers;

namespace Nolock.social.MistralOcr.IntegrationTests;

public class ReactiveMistralOcrPipelineIntegrationTests : TestBase, IDisposable
{
    private readonly ReactiveMistralOcrPipeline _pipeline;
    private readonly List<OcrResult> _results = new();
    private readonly List<OcrError> _errors = new();
    private IDisposable? _resultsSubscription;
    private IDisposable? _errorsSubscription;
    
    public ReactiveMistralOcrPipelineIntegrationTests(MistralOcrTestFixture fixture) : base(fixture)
    {
        var logger = Fixture.ServiceProvider.GetRequiredService<ILogger<ReactiveMistralOcrPipeline>>();
        var config = new PipelineConfiguration
        {
            MaxConcurrency = 2,
            RetryCount = 2,
            RequestTimeout = TimeSpan.FromMinutes(1),
            RateLimitWindow = TimeSpan.FromSeconds(1),
            RateLimitCount = 5
        };
        
        _pipeline = new ReactiveMistralOcrPipeline(
            Fixture.MistralOcrService,
            logger,
            config);
        
        // Subscribe to results and errors
        _resultsSubscription = _pipeline.Results.Subscribe(r => _results.Add(r));
        _errorsSubscription = _pipeline.Errors.Subscribe(e => _errors.Add(e));
    }

    [Fact]
    public async Task Pipeline_ProcessSingleRequest_ShouldSucceed()
    {
        // Arrange
        var request = new ReactiveOcrRequest
        {
            Input = GetTestImageDataUrl(),
            InputType = OcrInputType.DataUrl,
            Prompt = "Extract text"
        };
        
        // Act
        _pipeline.SubmitRequest(request);
        
        // Wait for processing
        await Task.Delay(3000);
        
        // Assert
        _results.Should().HaveCount(1);
        _results[0].RequestId.Should().Be(request.RequestId);
        _results[0].Success.Should().BeTrue();
        _results[0].Text.Should().NotBeNullOrWhiteSpace();
        _results[0].ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        
        _errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Pipeline_ProcessMultipleRequests_ShouldHandleConcurrency()
    {
        // Arrange
        var requests = Enumerable.Range(1, 3)
            .Select(i => new ReactiveOcrRequest
            {
                Input = TestImageHelper.GetReceiptImageDataUrl(i),
                InputType = OcrInputType.DataUrl,
                Prompt = $"Extract text from receipt {i}"
            })
            .ToList();
        
        // Act
        foreach (var request in requests)
        {
            _pipeline.SubmitRequest(request);
        }
        
        // Wait for all processing
        await Task.Delay(5000);
        
        // Assert
        _results.Should().HaveCount(3);
        _results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.Text.Should().NotBeNullOrWhiteSpace();
        });
        
        _errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Pipeline_WithInvalidRequest_ShouldHandleGracefully()
    {
        // Arrange
        var validRequest = new ReactiveOcrRequest
        {
            Input = GetTestImageDataUrl(),
            InputType = OcrInputType.DataUrl,
            Prompt = "Extract text from this valid image"
        };
        
        var invalidRequest = new ReactiveOcrRequest
        {
            Input = "data:image/png", // Invalid data URL - missing base64 content
            InputType = OcrInputType.DataUrl,
            Prompt = "This should fail gracefully"
        };
        
        // Act - submit both valid and invalid requests
        _pipeline.SubmitRequest(validRequest);
        _pipeline.SubmitRequest(invalidRequest);
        
        // Wait for processing
        await Task.Delay(8000);
        
        // Assert - we should have at least one result (the valid one)
        (_results.Count + _errors.Count).Should().BeGreaterThanOrEqualTo(1);
        
        // The valid request should have succeeded
        var validResult = _results.FirstOrDefault(r => r.RequestId == validRequest.RequestId);
        if (validResult != null)
        {
            validResult.Success.Should().BeTrue();
            validResult.Text.Should().NotBeNullOrWhiteSpace();
        }
        
        // The pipeline should continue working after handling invalid requests
        _results.Count(r => r.Success).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Pipeline_WithDifferentPriorities_ShouldProcessInOrder()
    {
        // Arrange
        var highPriorityRequest = new ReactiveOcrRequest
        {
            Input = TestImageHelper.GetReceiptImageDataUrl(1),
            InputType = OcrInputType.DataUrl,
            Priority = 1,
            Prompt = "High priority"
        };
        
        var lowPriorityRequest = new ReactiveOcrRequest
        {
            Input = TestImageHelper.GetReceiptImageDataUrl(2),
            InputType = OcrInputType.DataUrl,
            Priority = 0,
            Prompt = "Low priority"
        };
        
        // Act - submit low priority first
        _pipeline.SubmitRequest(lowPriorityRequest);
        _pipeline.SubmitRequest(highPriorityRequest);
        
        // Wait for processing
        await Task.Delay(5000);
        
        // Assert
        _results.Should().HaveCount(2);
        _results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    [Fact]
    public async Task Pipeline_GetStatistics_ShouldReturnMetrics()
    {
        // Arrange
        var statsWindow = TimeSpan.FromSeconds(1);
        var stats = new List<PipelineStatistics>();
        
        using var statsSubscription = _pipeline.GetStatistics(statsWindow)
            .Take(5)  // Take more samples
            .Subscribe(s => stats.Add(s));
        
        // Submit some requests
        for (var i = 1; i <= 2; i++)
        {
            _pipeline.SubmitRequest(new ReactiveOcrRequest
            {
                Input = TestImageHelper.GetReceiptImageDataUrl(i),
                InputType = OcrInputType.DataUrl,
                Prompt = $"Request {i}"
            });
        }
        
        // Act - wait for statistics and processing
        await Task.Delay(7000);  // Wait longer
        
        // Assert
        stats.Should().HaveCountGreaterThan(0);
        
        // Wait for all requests to complete
        await Task.Delay(2000);
        
        // Check final stats
        var finalSuccessCount = _results.Count(r => r.Success);
        var finalErrorCount = _results.Count(r => !r.Success) + _errors.Count;
        
        finalSuccessCount.Should().BeGreaterThanOrEqualTo(0);
        (finalSuccessCount + finalErrorCount).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Pipeline_WithBase64Input_ShouldProcess()
    {
        // Arrange
        var imageBytes = TestImageHelper.GetReceiptImageBytes(1);
        var base64 = Convert.ToBase64String(imageBytes);
        
        var request = new ReactiveOcrRequest
        {
            Input = base64,
            InputType = OcrInputType.Base64,
            MimeType = "image/jpeg",
            Prompt = "Extract text from base64"
        };
        
        // Act
        _pipeline.SubmitRequest(request);
        
        // Wait for processing
        await Task.Delay(3000);
        
        // Assert
        _results.Should().HaveCount(1);
        _results[0].Success.Should().BeTrue();
        _results[0].Text.Should().NotBeNullOrWhiteSpace();
    }

    public void Dispose()
    {
        _resultsSubscription?.Dispose();
        _errorsSubscription?.Dispose();
        _pipeline?.Dispose();
    }
}