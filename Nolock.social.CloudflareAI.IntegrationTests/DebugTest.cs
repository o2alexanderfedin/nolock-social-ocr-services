using System.Text;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class DebugTest : BaseIntegrationTest
{
    [Fact]
    public async Task Debug_SimpleTextGeneration_ShowsRequestDetails()
    {
        var request = new TextGenerationRequest
        {
            Prompt = "Hello",
            MaxTokens = 10
        };

        var modelsToTry = new[]
        {
            "@cf/meta/llama-2-7b-chat-int8",  // Original format
            "llama-2-7b-chat-int8",           // Without prefix
            "@cf/meta/llama-3.1-8b-instruct", // Different model
            "llama-3.1-8b-instruct"           // Different model without prefix
        };

        foreach (var model in modelsToTry)
        {
            try
            {
                Logger.LogInformation("Testing model: {Model}", model);
                Logger.LogInformation("Request: {Request}", System.Text.Json.JsonSerializer.Serialize(request));
                
                var result = await Client.RunAsync<TextGenerationResponse>(model, request);
                    
                Logger.LogInformation("SUCCESS with model {Model}! Result: {Result}", model, result.Response ?? result.GeneratedText);
                return; // Exit on first success
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError("Model {Model} failed: {Message}", model, ex.Message);
                
                // Try raw request to see exact error
                try
                {
                    using var rawResponse = await Client.RunRawAsync(model, request);
                    var content = await rawResponse.Content.ReadAsStringAsync();
                    Logger.LogError("Raw response status: {Status}", rawResponse.StatusCode);
                    Logger.LogError("Raw response content: {Content}", content);
                }
                catch (Exception rawEx)
                {
                    Logger.LogError("Raw request also failed: {Message}", rawEx.Message);
                }
            }
        }
        
        throw new Exception("All model formats failed");
    }
    
    [Fact]
    public async Task Debug_CheckAccountAndCredentials()
    {
        var settings = TestConfiguration.GetSettings();
        Logger.LogInformation("Account ID: {AccountId}", settings.AccountId);
        Logger.LogInformation("API Token starts with: {TokenStart}", settings.ApiToken[..10] + "...");
        Logger.LogInformation("Base URL: {BaseUrl}", settings.BaseUrl);
        
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new("Bearer", settings.ApiToken);
        
        // Test 1: Account exists
        try
        {
            var accountUrl = $"{settings.BaseUrl}/accounts/{settings.AccountId}";
            Logger.LogInformation("Testing account URL: {Url}", accountUrl);
            
            var response = await httpClient.GetAsync(accountUrl);
            var content = await response.Content.ReadAsStringAsync();
            
            Logger.LogInformation("Account check status: {Status}", response.StatusCode);
            Logger.LogInformation("Account check response: {Content}", content);
        }
        catch (Exception ex)
        {
            Logger.LogError("Account check failed: {Message}", ex.Message);
        }
        
        // Test 2: Check if Workers AI path exists at all
        try
        {
            var aiUrl = $"{settings.BaseUrl}/accounts/{settings.AccountId}/ai";
            Logger.LogInformation("Testing Workers AI base URL: {Url}", aiUrl);
            
            var response = await httpClient.GetAsync(aiUrl);
            var content = await response.Content.ReadAsStringAsync();
            
            Logger.LogInformation("Workers AI base check status: {Status}", response.StatusCode);
            Logger.LogInformation("Workers AI base response: {Content}", content);
        }
        catch (Exception ex)
        {
            Logger.LogError("Workers AI base check failed: {Message}", ex.Message);
        }
        
        // Test 3: Try to list models or get any AI endpoint
        try
        {
            var modelsUrl = $"{settings.BaseUrl}/accounts/{settings.AccountId}/ai/models";
            Logger.LogInformation("Testing models list URL: {Url}", modelsUrl);
            
            var response = await httpClient.GetAsync(modelsUrl);
            var content = await response.Content.ReadAsStringAsync();
            
            Logger.LogInformation("Models list status: {Status}", response.StatusCode);
            Logger.LogInformation("Models list response: {Content}", content);
        }
        catch (Exception ex)
        {
            Logger.LogError("Models list check failed: {Message}", ex.Message);
        }
    }
}