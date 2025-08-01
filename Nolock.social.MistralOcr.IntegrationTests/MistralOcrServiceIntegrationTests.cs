using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;

namespace Nolock.social.MistralOcr.IntegrationTests;

public class MistralOcrServiceIntegrationTests : TestBase
{
    public MistralOcrServiceIntegrationTests(MistralOcrTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Integration_WithRealMistralApi_VerifyConnectionWorks()
    {
        // ONE test that actually hits the API to verify integration works
        // Uncomment Skip attribute to run this test manually
        var dataUrl = GetTestImageDataUrl();
        
        var result = await Fixture.MistralOcrService.ProcessImageDataItemAsync(
            (new Uri(dataUrl), "image/jpeg"));

        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrWhiteSpace();
        result.ModelUsed.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData(" ", typeof(ArgumentException))]
    public async Task ProcessImageAsync_WithInvalidUrl_ShouldThrowCorrectException(string? url, Type expectedExceptionType)
    {
        // Test OUR validation, not Mistral's
        var act = async () => await Fixture.MistralOcrService.ProcessImageAsync(url!);
        
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.GetType() == expectedExceptionType);
    }

    [Theory]
    [InlineData("not-a-data-url")]
    [InlineData("data:image/png")]
    [InlineData("data:text/plain;base64,SGVsbG8=")]
    public async Task ProcessImageDataUrlAsync_WithInvalidDataUrl_ShouldThrowException(string invalidDataUrl)
    {
        // Act
        var act = async () => await Fixture.MistralOcrService.ProcessImageDataItemAsync((new Uri(invalidDataUrl), "image/jpeg"));
        
        // Assert - Either ArgumentException from our validation or HttpRequestException from API
        await act.Should().ThrowAsync<Exception>()
            .Where(ex => ex is ArgumentException || ex is HttpRequestException);
    }

    [Fact]
    public async Task ProcessImageBytesAsync_WithEmptyBytes_ShouldThrowArgumentException()
    {
        var act = async () => await Fixture.MistralOcrService.ProcessImageBytesAsync(
            Array.Empty<byte>(), 
            "image/png");
        
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public async Task ProcessImageStreamAsync_WithEmptyStream_ShouldThrowArgumentException()
    {
        using var stream = new MemoryStream();
        
        var act = async () => await Fixture.MistralOcrService.ProcessImageStreamAsync(
            stream, 
            "image/png");
        
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ProcessImageBytesAsync_WithInvalidMimeType_ShouldThrowArgumentException(string? mimeType)
    {
        var act = async () => await Fixture.MistralOcrService.ProcessImageBytesAsync(
            new byte[] { 1, 2, 3 }, 
            mimeType!);
        
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*mimeType*");
    }

    [Fact]
    public void Service_Configuration_ShouldBeValid()
    {
        // Verify service is configured correctly
        var config = Fixture.Configuration;
        
        // Should have API key from environment or config
        var apiKey = config["MistralOcr:ApiKey"] ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
        apiKey.Should().NotBeNullOrWhiteSpace("API key must be configured");
        
        // Should have correct base URL
        var baseUrl = config["MistralOcr:BaseUrl"];
        baseUrl.Should().Be("https://api.mistral.ai");
        
        // Should have a model configured
        var model = config["MistralOcr:Model"];
        model.Should().NotBeNullOrWhiteSpace();
        model.Should().StartWith("mistral-ocr");
    }

    [Fact]
    public void Service_ShouldBeProperlyRegistered()
    {
        // Verify service is registered in DI container
        var service = Fixture.ServiceProvider.GetRequiredService<IMistralOcrService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<MistralOcrApiService>();
    }
}