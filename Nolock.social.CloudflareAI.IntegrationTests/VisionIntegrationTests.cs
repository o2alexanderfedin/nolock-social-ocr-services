using System.Text;
using Nolock.social.CloudflareAI.Models;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class VisionIntegrationTests : BaseIntegrationTest
{
    [Fact(Skip = "Vision models have issues: LLaVA deprecated, Llama 3.2 requires license, UForm has API format issues")]
    public async Task RunAsync_Llava_WithBase64Image_DescribesImage()
    {
        await Task.CompletedTask;
    }

    private static string CreateSimpleRedPixelPng()
    {
        // Create a 2x2 red pixel PNG image (more standard than 1x1)
        return "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFklEQVQIHWP8//8/AzYwirkRmwQYGQAAVQAhAKoMOwAAAABJRU5ErkJggg==";
    }

    [Fact(Skip = "UForm API format is not properly documented and returns schema validation errors")]
    public async Task RunAsync_UForm_WithSimpleImage_AnalyzesContent()
    {
        // Create a simple 2x2 checkerboard pattern in base64
        var checkerboardPng = CreateSimpleCheckerboardBase64();
        var imageBytes = Convert.FromBase64String(checkerboardPng);
        var imageIntegers = imageBytes.Select(b => (int)b).ToArray();
        
        var request = new
        {
            image = imageIntegers,
            prompt = "Generate a caption for this image",
            max_tokens = 80
        };

        var result = await Client.RunAsync<dynamic>(
            VisionModels.UForm_Gen2_QWen_500M,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.description);
        
        string analysis = result.description.ToString();
        Logger.LogInformation("Pattern analysis: {Analysis}", analysis);
        Assert.False(string.IsNullOrWhiteSpace(analysis));
    }

    [Fact(Skip = "LLaVA model is deprecated and returns 'Unsupported image data' errors")]
    public async Task RunAsync_Llava_WithOCRTask_ReadsText()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "UForm API format is not properly documented and returns schema validation errors")]
    public async Task RunAsync_WithColorDetection_IdentifiesColors()
    {
        // Create a simple colored image
        var blueSquarePng = CreateSimpleColoredImageBase64();
        var imageBytes = Convert.FromBase64String(blueSquarePng);
        var imageIntegers = imageBytes.Select(b => (int)b).ToArray();
        
        var request = new
        {
            image = imageIntegers,
            prompt = "Describe the colors in this image",
            max_tokens = 30
        };

        var result = await Client.RunAsync<dynamic>(
            VisionModels.UForm_Gen2_QWen_500M,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.description);
        
        string colorDescription = result.description.ToString();
        Logger.LogInformation("Color detection: {Colors}", colorDescription);
        Assert.False(string.IsNullOrWhiteSpace(colorDescription));
    }

    [Fact(Skip = "LLaVA model is deprecated and Llama 3.2 requires license agreement")]
    public async Task RunAsync_WithObjectCounting_CountsObjects()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "UForm API format is not properly documented and returns schema validation errors")]
    public async Task RunRawAsync_WithVisionModel_ReturnsHttpResponse()
    {
        var redPixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
        var imageBytes = Convert.FromBase64String(redPixelPng);
        var imageIntegers = imageBytes.Select(b => (int)b).ToArray();
        
        var request = new
        {
            image = imageIntegers,
            prompt = "Generate a caption for this image"
        };

        using var response = await Client.RunRawAsync(
            VisionModels.UForm_Gen2_QWen_500M,
            request);

        var content = await response.Content.ReadAsStringAsync();
        Logger.LogInformation("Raw vision response status: {Status}, content: {Content}", response.StatusCode, content);
        
        Assert.True(response.IsSuccessStatusCode, $"Expected success status, got {response.StatusCode}: {content}");
        Assert.False(string.IsNullOrWhiteSpace(content));
        
        // Should contain result with vision analysis
        Assert.Contains("result", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "LLaVA model is deprecated and Llama 3.2 requires license agreement")]
    public async Task RunAsync_WithComplexScene_AnalyzesDetails()
    {
        await Task.CompletedTask;
    }

    [Theory(Skip = "UForm API format is not properly documented and returns schema validation errors")]
    [InlineData("Generate a caption for this image")]
    [InlineData("Describe this image")]
    [InlineData("What do you see in this picture?")]
    public async Task RunAsync_WithDifferentPrompts_GeneratesVariedResponses(string prompt)
    {
        var testImagePng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
        var imageBytes = Convert.FromBase64String(testImagePng);
        var imageIntegers = imageBytes.Select(b => (int)b).ToArray();
        
        var request = new
        {
            image = imageIntegers,
            prompt = prompt,
            max_tokens = 60
        };

        var result = await Client.RunAsync<dynamic>(
            VisionModels.UForm_Gen2_QWen_500M,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.description);
        
        string response = result.description.ToString();
        Logger.LogInformation("Prompt: '{Prompt}' -> Response: '{Response}'", prompt, response);
        Assert.False(string.IsNullOrWhiteSpace(response));
    }

    // Helper methods to create simple test images in base64 format
    private static string CreateSimpleCheckerboardBase64()
    {
        // This is a placeholder - in a real scenario, you'd generate actual image data
        // For now, return a simple 1x1 image
        return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChAI9jU77zgAAAABJRU5ErkJggg==";
    }

    private static string CreateSimpleTextImageBase64()
    {
        // Placeholder for an image containing text
        return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
    }

    private static string CreateSimpleColoredImageBase64()
    {
        // Placeholder for a colored image (blue)
        return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYGBgAAAABQABijOjAAAAAABJRU5ErkJggg==";
    }

    private static string CreateSimpleMultiObjectImageBase64()
    {
        // Placeholder for an image with multiple objects
        return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
    }

    private static string CreateComplexSceneBase64()
    {
        // Placeholder for a complex scene
        return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYGBgAAAABQABijOjAAAAAABJRU5ErkJggg==";
    }
}