using System.Text.Json;
using static Nolock.social.CloudflareAI.JsonExtraction.JsonExtractionExtensions;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class JsonExtractionDebugTest : BaseIntegrationTest
{
    [Fact]
    public async Task Debug_JsonExtraction_SimplePrompt()
    {
        // First, let's try a simple JSON extraction without using response_format
        var prompt = @"
            Extract the following information from this text and return as JSON:
            'John Doe is 30 years old and works as a software engineer.'
            
            Return JSON with these fields:
            - name (string): the person's name
            - age (number): the person's age
            - occupation (string): the person's job
            
            Return ONLY valid JSON, no other text.
        ";

        var request = new TextGenerationRequest
        {
            Prompt = prompt,
            MaxTokens = 200,
            Temperature = 0.1
        };

        try
        {
            var response = await Client.RunAsync<TextGenerationResponse>(
                TextGenerationModels.Llama3_1_8B_Instruct,
                request);

            var result = response.Response ?? response.GeneratedText ?? "";
            Logger.LogInformation("Simple prompt response: {Response}", result);

            // Try to parse as JSON
            try
            {
                var json = JsonDocument.Parse(result);
                Logger.LogInformation("✅ Valid JSON extracted!");
            }
            catch (JsonException)
            {
                Logger.LogWarning("❌ Response is not valid JSON");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Request failed: {Error}", ex.Message);
        }
    }

    [Fact]
    public async Task Debug_JsonExtraction_WithResponseFormat()
    {
        // Test with response_format to see what format Cloudflare expects
        var request = new
        {
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant that extracts information and returns JSON." },
                new { role = "user", content = "Extract name, age, and job from: 'John Doe is 30 years old and works as a software engineer.'" }
            },
            max_tokens = 200,
            temperature = 0.1,
            response_format = new
            {
                type = "json_object"
            }
        };

        try
        {
            using var response = await Client.RunRawAsync(
                TextGenerationModels.Llama3_1_8B_Instruct,
                request);

            var content = await response.Content.ReadAsStringAsync();
            Logger.LogInformation("Response status: {Status}", response.StatusCode);
            Logger.LogInformation("Response content: {Content}", content);

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TextGenerationResponse>>(content);
                if (apiResponse?.Result != null)
                {
                    var text = apiResponse.Result.Response ?? apiResponse.Result.GeneratedText;
                    Logger.LogInformation("Extracted text: {Text}", text);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("JSON mode request failed: {Error}", ex.Message);
        }
    }
}