using System.Reactive.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.MistralOcr;
using Nolock.social.MistralOcr.Extensions;
using Nolock.social.OCRservices.Pipelines;
using Xunit;

namespace Nolock.social.OCRservices.Tests.Pipelines;

public class OcrToModelPipelineIntegrationTests : IClassFixture<TestServicesFixture>
{
    private readonly OcrToModelPipeline _pipeline;
    private readonly IMistralOcrService _ocrService;
    private readonly IWorkersAI _workersAI;

    public OcrToModelPipelineIntegrationTests(TestServicesFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _ocrService = fixture.ServiceProvider.GetRequiredService<IMistralOcrService>();
        _workersAI = fixture.ServiceProvider.GetRequiredService<IWorkersAI>();
        _pipeline = new OcrToModelPipeline(_ocrService, _workersAI);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessReceiptImage_WithRealServices_ReturnsReceipt()
    {
        // Arrange
        using var imageStream = TestData.TestImageHelper.GenerateTestImage();

        // Act
        var result = await _pipeline.ProcessReceiptImage(imageStream, "image/png");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Merchant);
        Assert.NotNull(result.Totals);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessCheckImage_WithRealServices_ReturnsCheck()
    {
        // Arrange
        using var imageStream = TestData.TestImageHelper.GenerateTestImage();

        // Act
        var result = await _pipeline.ProcessCheckImage(imageStream, "image/png");

        // Assert
        Assert.NotNull(result);
    }
}

public class TestServicesFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public TestServicesFixture()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // Add MistralOcr
        services.AddMistralOcr(options =>
        {
            options.ApiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY") 
                ?? throw new InvalidOperationException("MISTRAL_API_KEY environment variable is required for integration tests");
        });

        // Add CloudflareAI
        services.AddWorkersAI(options =>
        {
            options.AccountId = Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID") 
                ?? throw new InvalidOperationException("CLOUDFLARE_ACCOUNT_ID environment variable is required for integration tests");
            options.ApiToken = Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN") 
                ?? throw new InvalidOperationException("CLOUDFLARE_API_TOKEN environment variable is required for integration tests");
        });

        ServiceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}