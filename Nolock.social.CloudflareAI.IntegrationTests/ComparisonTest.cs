using System.Net.Http.Headers;
using System.Text;
using Nolock.social.CloudflareAI.Configuration;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class ComparisonTest : BaseIntegrationTest
{
    [Fact]
    public async Task Compare_DirectHttpClient_vs_WorkersAIClient()
    {
        var settings = TestConfiguration.GetSettings();

        // Test 1: Direct HttpClient (like in diagnostic test - this worked)
        Logger.LogInformation("=== TESTING DIRECT HTTP CLIENT ===");
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var testPayload = new
        {
            prompt = "Hello",
            max_tokens = 10
        };

        var jsonPayload = System.Text.Json.JsonSerializer.Serialize(testPayload);
        Logger.LogInformation("Direct HTTP payload: {Payload}", jsonPayload);

        var endpoint = $"{settings.BaseUrl}/accounts/{settings.AccountId}/ai/run/@cf/meta/llama-2-7b-chat-int8";
        Logger.LogInformation("Direct HTTP endpoint: {Endpoint}", endpoint);

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(endpoint, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        Logger.LogInformation("Direct HTTP Status: {Status}", response.StatusCode);
        Logger.LogInformation("Direct HTTP Response: {Response}", responseContent);

        if (response.IsSuccessStatusCode)
        {
            Logger.LogInformation("✅ Direct HTTP CLIENT WORKED!");
        }
        else
        {
            Logger.LogError("❌ Direct HTTP CLIENT FAILED");
        }

        Logger.LogInformation("\n=== TESTING WORKERS AI CLIENT ===");

        // Test 2: WorkersAI Client (this is failing)
        var request = new TextGenerationRequest
        {
            Prompt = "Hello",
            MaxTokens = 10
        };

        var serializedRequest = System.Text.Json.JsonSerializer.Serialize(request);
        Logger.LogInformation("WorkersAI Client payload: {Payload}", serializedRequest);

        try
        {
            var result = await Client.RunAsync<TextGenerationResponse>("@cf/meta/llama-2-7b-chat-int8", request);
            Logger.LogInformation("✅ WORKERS AI CLIENT WORKED! Response: {Response}", result.Response ?? result.GeneratedText);
        }
        catch (Exception ex)
        {
            Logger.LogError("❌ WORKERS AI CLIENT FAILED: {Message}", ex.Message);

            // Try raw response to see what's happening
            try
            {
                using var rawResponse = await Client.RunRawAsync("@cf/meta/llama-2-7b-chat-int8", request);
                var rawContent = await rawResponse.Content.ReadAsStringAsync();
                Logger.LogError("Raw WorkersAI Status: {Status}", rawResponse.StatusCode);
                Logger.LogError("Raw WorkersAI Response: {Response}", rawContent);
                Logger.LogError("Raw WorkersAI Request URL: {Url}", rawResponse.RequestMessage?.RequestUri);
            }
            catch (Exception rawEx)
            {
                Logger.LogError("Raw WorkersAI also failed: {Message}", rawEx.Message);
            }
        }
    }
}