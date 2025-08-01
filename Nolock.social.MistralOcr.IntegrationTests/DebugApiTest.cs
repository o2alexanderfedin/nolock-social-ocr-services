using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.Models;

namespace Nolock.social.MistralOcr.IntegrationTests;

public class DebugApiTest : TestBase
{
    public DebugApiTest(MistralOcrTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task TestDirectApiCall()
    {
        // Arrange
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://api.mistral.ai");
        
        var apiKey = Fixture.Configuration["MistralOcr:ApiKey"];
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var request = new
        {
            model = "pixtral-12b-2409",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "What is this image?" },
                        new { type = "image_url", image_url = new { url = GetTestImageDataUrl() } }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.PostAsync("/v1/chat/completions", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, 
            $"API returned {response.StatusCode}: {responseContent}");
    }
}