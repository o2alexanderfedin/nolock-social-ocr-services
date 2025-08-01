using System.Net;
using Nolock.social.CloudflareAI.Models;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class ImageGenerationIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task RunAsync_StableDiffusion_WithSimplePrompt_GeneratesImage()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A beautiful sunset over mountains, digital art"
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0, 
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Generated image size: {Size} bytes", imageBytes.Length);
        
        // Image should be a reasonable size (at least 10KB)
        Assert.True(imageBytes.Length > 10000, $"Image too small: {imageBytes.Length} bytes");
    }

    [Fact]
    public async Task RunAsync_StableDiffusionXL_WithDetailedPrompt_GeneratesHighQualityImage()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A majestic dragon flying over a medieval castle at dawn, highly detailed, fantasy art, 4k resolution",
            NumSteps = 20,
            Guidance = 7.5
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Generated XL image size: {Size} bytes", imageBytes.Length);
        
        // SDXL should generate larger images
        Assert.True(imageBytes.Length > 50000, $"XL image should be larger: {imageBytes.Length} bytes");
    }

    [Fact]
    public async Task RunAsync_DreamShaper_WithArtisticPrompt_GeneratesArtwork()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "An abstract painting of music notes floating in space, vibrant colors, surreal",
            NumSteps = 15,
            Strength = 0.8
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.DreamShaper_8_LCM,
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        // Image bytes already validated above
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Generated DreamShaper image size: {Size} bytes", imageBytes.Length);
    }

    [Fact]
    public async Task RunAsync_WithPortraitPrompt_GeneratesPortrait()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "Portrait of a wise old wizard with a long white beard, detailed face, fantasy character",
            NumSteps = 20,
            Guidance = 7.5
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        // Image bytes already validated above
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Generated portrait size: {Size} bytes", imageBytes.Length);
    }

    [Fact]
    public async Task RunAsync_WithNaturePrompt_GeneratesLandscape()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A serene lake surrounded by pine trees, morning mist, realistic photography style",
            NumSteps = 20
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Generated landscape size: {Size} bytes", imageBytes.Length);
    }

    [Fact]
    public async Task RunRawAsync_WithImageGeneration_ReturnsHttpResponse()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A simple red circle on white background"
        };

        using var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0, 
            request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("Image generation failed with status {Status}: {Error}", response.StatusCode, errorContent);
        }

        Assert.True(response.IsSuccessStatusCode, $"Expected success status code, but got {response.StatusCode}");
        
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Raw response image size: {Size} bytes", imageBytes.Length);
        
        // Should contain binary image data
        Assert.True(imageBytes.Length > 1000, "Generated image should be at least 1KB");
    }

    [Fact]
    public async Task RunAsync_WithBasicPrompt_GeneratesImage()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A cat sitting on a windowsill, digital art"
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("Image generation failed with status {Status}: {Error}", response.StatusCode, errorContent);
        }
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Generated image size: {Size} bytes", imageBytes.Length);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(7.5)]
    [InlineData(10.0)]
    public async Task RunAsync_WithDifferentGuidance_GeneratesImagesWithVaryingAdherence(double guidance)
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A blue butterfly on a yellow flower, macro photography",
            Guidance = guidance,
            NumSteps = 15
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Guidance {Guidance}: Generated image size: {Size} bytes", guidance, imageBytes.Length);
    }

    [Fact]
    public async Task RunAsync_WithArchitecturalPrompt_GeneratesBuilding()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "Modern glass skyscraper with unique twisted design, architectural photography, blue sky",
            NumSteps = 20,
            Guidance = 7.0
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("Image generation failed with status {Status}: {Error}", response.StatusCode, errorContent);
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Generated architectural image size: {Size} bytes", imageBytes.Length);
    }

    [Fact]
    public async Task RunAsync_WithAnimalPrompt_GeneratesAnimal()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A majestic lion resting under an acacia tree in African savanna, wildlife photography",
            NumSteps = 20,
            Guidance = 8.0
        };

        var response = await Client.RunRawAsync(
            ImageGenerationModels.DreamShaper_8_LCM,
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotNull(imageBytes);
        // Image bytes already validated above
        Assert.True(imageBytes.Length > 0);
        
        Logger.LogInformation("Generated animal image size: {Size} bytes", imageBytes.Length);
    }
}