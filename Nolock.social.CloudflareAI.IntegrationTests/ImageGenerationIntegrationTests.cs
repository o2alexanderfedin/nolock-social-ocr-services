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

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.StableDiffusion_1_5, 
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Generated image size: {Size} bytes", result.Image.Length);
        
        // Image should be a reasonable size (at least 10KB)
        Assert.True(result.Image.Length > 10000, $"Image too small: {result.Image.Length} bytes");
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

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Generated XL image size: {Size} bytes", result.Image.Length);
        
        // SDXL should generate larger images
        Assert.True(result.Image.Length > 50000, $"XL image should be larger: {result.Image.Length} bytes");
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

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.DreamShaper_8_LCM,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Generated DreamShaper image size: {Size} bytes", result.Image.Length);
    }

    [Fact]
    public async Task RunAsync_WithPortraitPrompt_GeneratesPortrait()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "Portrait of a wise old wizard with a long white beard, detailed face, fantasy character",
            NumSteps = 25,
            Guidance = 8.0
        };

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.StableDiffusion_1_5,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Generated portrait size: {Size} bytes", result.Image.Length);
    }

    [Fact]
    public async Task RunAsync_WithNaturePrompt_GeneratesLandscape()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A serene lake surrounded by pine trees, morning mist, realistic photography style",
            NumSteps = 20
        };

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Generated landscape size: {Size} bytes", result.Image.Length);
    }

    [Fact]
    public async Task RunRawAsync_WithImageGeneration_ReturnsHttpResponse()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A simple red circle on white background"
        };

        using var response = await Client.RunRawAsync(
            ImageGenerationModels.StableDiffusion_1_5, 
            request);

        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));
        
        Logger.LogInformation("Raw image generation response length: {Length}", content.Length);
        
        // Should contain result with image data
        Assert.Contains("result", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task RunAsync_WithDifferentSteps_GeneratesImagesWithVaryingQuality(int numSteps)
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "A cat sitting on a windowsill, digital art",
            NumSteps = numSteps
        };

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.StableDiffusion_1_5,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Steps {Steps}: Generated image size: {Size} bytes", numSteps, result.Image.Length);
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

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Guidance {Guidance}: Generated image size: {Size} bytes", guidance, result.Image.Length);
    }

    [Fact]
    public async Task RunAsync_WithArchitecturalPrompt_GeneratesBuilding()
    {
        var request = new ImageGenerationRequest
        {
            Prompt = "Modern glass skyscraper with unique twisted design, architectural photography, blue sky",
            NumSteps = 25,
            Guidance = 7.0
        };

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.StableDiffusion_XL_Base_1_0,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Generated architectural image size: {Size} bytes", result.Image.Length);
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

        var result = await Client.RunAsync<ImageGenerationResponse>(
            ImageGenerationModels.DreamShaper_8_LCM,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Image);
        Assert.True(result.Image.Length > 0);
        
        Logger.LogInformation("Generated animal image size: {Size} bytes", result.Image.Length);
    }
}