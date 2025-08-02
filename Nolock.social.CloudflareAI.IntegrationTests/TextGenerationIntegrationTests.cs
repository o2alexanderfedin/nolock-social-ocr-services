using Nolock.social.CloudflareAI.Models;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class TextGenerationIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task RunAsync_Llama2_WithSimplePrompt_GeneratesText()
    {
        var request = new TextGenerationRequest
        {
            Prompt = "What is the capital of France?",
            MaxTokens = 50
        };

        var result = await Client.RunAsync<TextGenerationResponse>(
            TextGenerationModels.Llama2_7B_Chat_Int8, 
            request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response) && string.IsNullOrWhiteSpace(result.GeneratedText));
        
        var generatedText = result.Response ?? result.GeneratedText;
        Logger.LogInformation("Generated text: {Text}", generatedText);
        
        // Should contain something about Paris
        Assert.Contains("Paris", generatedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Llama3_WithChatMessages_GeneratesResponse()
    {
        var request = new TextGenerationRequest
        {
            Messages = [
                new Message { Role = "system", Content = "You are a helpful assistant." },
                new Message { Role = "user", Content = "Explain quantum computing in one sentence." }
            ],
            MaxTokens = 100,
            Temperature = 0.7
        };

        var result = await Client.RunAsync<TextGenerationResponse>(
            TextGenerationModels.Llama3_8B_Instruct, 
            request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response) && string.IsNullOrWhiteSpace(result.GeneratedText));
        
        var generatedText = result.Response ?? result.GeneratedText;
        Logger.LogInformation("Chat response: {Text}", generatedText);
        
        // Should contain quantum-related terms
        Assert.True(
            generatedText.Contains("quantum", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("computing", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("qubit", StringComparison.OrdinalIgnoreCase),
            $"Response should contain quantum-related terms. Got: {generatedText}");
    }

    [Fact] 
    public async Task RunAsync_Mistral_WithCodeGeneration_GeneratesCode()
    {
        var request = new TextGenerationRequest
        {
            Prompt = "Write a simple Python function that calculates the factorial of a number:",
            MaxTokens = 200,
            Temperature = 0.1
        };

        var result = await Client.RunAsync<TextGenerationResponse>(
            TextGenerationModels.Mistral_7B_Instruct_V0_1,
            request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response) && string.IsNullOrWhiteSpace(result.GeneratedText));
        
        var generatedText = result.Response ?? result.GeneratedText;
        Logger.LogInformation("Generated code: {Code}", generatedText);
        
        // Should contain Python function syntax
        Assert.Contains("def", generatedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("factorial", generatedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "CodeLlama model appears to be deprecated or unavailable")]
    public async Task RunAsync_CodeLlama_WithProgrammingTask_GeneratesCode()
    {
        var request = new TextGenerationRequest
        {
            Prompt = "Create a JavaScript function that reverses a string:",
            MaxTokens = 150
        };

        var result = await Client.RunAsync<TextGenerationResponse>(
            TextGenerationModels.CodeLlama_7B_Instruct,
            request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response) && string.IsNullOrWhiteSpace(result.GeneratedText));
        
        var generatedText = result.FinalResponse;
        Logger.LogInformation("Generated JavaScript: {Code}", generatedText);
        
        // Should contain JavaScript function syntax
        Assert.True(
            generatedText.Contains("function", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("=>", StringComparison.OrdinalIgnoreCase),
            $"Response should contain JavaScript function syntax. Got: {generatedText}");
    }

    [Fact]
    public async Task RunAsync_Llama3_WithJavaScriptTask_GeneratesCode()
    {
        // Alternative test using Llama3 for JavaScript code generation
        var request = new TextGenerationRequest
        {
            Prompt = "Create a JavaScript function that reverses a string:",
            MaxTokens = 150,
            Temperature = 0.1
        };

        var result = await Client.RunAsync<TextGenerationResponse>(
            TextGenerationModels.Llama3_8B_Instruct,
            request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response) && string.IsNullOrWhiteSpace(result.GeneratedText));
        
        var generatedText = result.Response ?? result.GeneratedText;
        Logger.LogInformation("Generated JavaScript: {Code}", generatedText);
        
        // Should contain JavaScript function syntax
        Assert.True(
            generatedText.Contains("function", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("=>", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("const", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("let", StringComparison.OrdinalIgnoreCase),
            $"Response should contain JavaScript function syntax. Got: {generatedText}");
    }

    [Fact(Skip = "Gemma model appears to be deprecated or unavailable")]
    public async Task RunAsync_Gemma_WithCreativeWriting_GeneratesStory()
    {
        var request = new TextGenerationRequest
        {
            Prompt = "Write a short story about a robot learning to paint:",
            MaxTokens = 300,
            Temperature = 0.8
        };

        var result = await Client.RunAsync<TextGenerationResponse>(
            TextGenerationModels.Gemma_7B_It,
            request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response) && string.IsNullOrWhiteSpace(result.GeneratedText));
        
        var generatedText = result.Response ?? result.GeneratedText;
        Logger.LogInformation("Generated story: {Story}", generatedText);
        
        // Should contain story elements
        Assert.True(
            generatedText.Contains("robot", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("paint", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("art", StringComparison.OrdinalIgnoreCase),
            $"Response should contain story elements. Got: {generatedText}");
    }

    [Fact]
    public async Task RunAsync_Llama3_WithCreativeWriting_GeneratesStory()
    {
        // Alternative test using Llama3 for creative writing
        var request = new TextGenerationRequest
        {
            Prompt = "Write a short story about a robot learning to paint:",
            MaxTokens = 300,
            Temperature = 0.8
        };

        var result = await Client.RunAsync<TextGenerationResponse>(
            TextGenerationModels.Llama3_8B_Instruct,
            request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.FinalResponse));
        
        var generatedText = result.FinalResponse;
        Logger.LogInformation("Generated story: {Story}", generatedText);
        
        // Should contain story elements
        Assert.True(
            generatedText.Contains("robot", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("paint", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("art", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("color", StringComparison.OrdinalIgnoreCase) ||
            generatedText.Contains("brush", StringComparison.OrdinalIgnoreCase),
            $"Response should contain story elements. Got: {generatedText}");
    }

    [Fact]
    public async Task RunRawAsync_WithPrompt_ReturnsHttpResponse()
    {
        var request = new TextGenerationRequest
        {
            Prompt = "What is 2+2?",
            MaxTokens = 10
        };

        using var response = await Client.RunRawAsync(
            TextGenerationModels.Llama2_7B_Chat_Int8, 
            request);

        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));
        
        Logger.LogInformation("Raw response: {Content}", content);
        
        // Should be valid JSON containing result
        Assert.Contains("result", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WithInvalidModel_ThrowsException()
    {
        var request = new TextGenerationRequest
        {
            Prompt = "Test prompt"
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Client.RunAsync<TextGenerationResponse>("@cf/invalid/model", request));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public async Task RunAsync_WithDifferentTemperatures_GeneratesVariedResponses(double temperature)
    {
        var request = new TextGenerationRequest
        {
            Prompt = "Complete this sentence: The weather today is",
            MaxTokens = 20,
            Temperature = temperature
        };

        var result = await Client.RunAsync<TextGenerationResponse>(
            TextGenerationModels.Llama3_8B_Instruct,
            request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response) && string.IsNullOrWhiteSpace(result.GeneratedText));
        
        var generatedText = result.Response ?? result.GeneratedText;
        Logger.LogInformation("Temperature {Temperature}: {Text}", temperature, generatedText);
    }
}