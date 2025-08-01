using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;

namespace Nolock.social.MistralOcr.IntegrationTests;

public class ConfigurationTests : TestBase
{
    public ConfigurationTests(MistralOcrTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Configuration_ShouldHaveApiKey()
    {
        // Assert
        var apiKey = Fixture.Configuration["MistralOcr:ApiKey"];
        apiKey.Should().NotBeNullOrWhiteSpace();
        apiKey.Should().StartWith("6VFw"); // Partial match for security
    }

    [Fact]
    public void Configuration_ShouldHaveCorrectBaseUrl()
    {
        // Assert
        var baseUrl = Fixture.Configuration["MistralOcr:BaseUrl"];
        baseUrl.Should().Be("https://api.mistral.ai");
    }

    [Fact]
    public void MistralOcrService_ShouldBeConfiguredCorrectly()
    {
        // Arrange
        var service = Fixture.ServiceProvider.GetRequiredService<IMistralOcrService>();
        
        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<MistralOcrService>();
    }
}