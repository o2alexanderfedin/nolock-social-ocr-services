using System.Net.Http.Headers;
using System.Text;
using Nolock.social.CloudflareAI.Configuration;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class DiagnosticTests : BaseIntegrationTest
{
    [Fact]
    public async Task Diagnostic_CompleteWorkersAIAccessCheck()
    {
        var settings = TestConfiguration.GetSettings();
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        Logger.LogInformation("=== CLOUDFLARE WORKERS AI DIAGNOSTIC ===");
        Logger.LogInformation("Account ID: {AccountId}", settings.AccountId);
        Logger.LogInformation("API Token starts with: {TokenStart}", settings.ApiToken[..10] + "...");
        Logger.LogInformation("Base URL: {BaseUrl}", settings.BaseUrl);

        // Test 1: Account access
        await TestAccountAccess(httpClient, settings);
        
        // Test 2: Workers service access
        await TestWorkersAccess(httpClient, settings);
        
        // Test 3: Workers AI specific endpoints
        await TestWorkersAIEndpoints(httpClient, settings);
        
        // Test 4: Try direct model invocation with detailed error analysis
        await TestDirectModelInvocation(httpClient, settings);
        
        // Test 5: Check if we need to enable Workers AI first
        await CheckWorkersAIActivation(httpClient, settings);
    }

    private async Task TestAccountAccess(HttpClient httpClient, WorkersAISettings settings)
    {
        Logger.LogInformation("\n1. TESTING ACCOUNT ACCESS");
        try
        {
            var response = await httpClient.GetAsync($"{settings.BaseUrl}/accounts/{settings.AccountId}");
            var content = await response.Content.ReadAsStringAsync();
            
            Logger.LogInformation("✅ Account Status: {Status}", response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                var accountInfo = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                if (accountInfo.TryGetProperty("result", out var result) && 
                    result.TryGetProperty("name", out var name))
                {
                    Logger.LogInformation("✅ Account Name: {Name}", name.GetString());
                }
            }
            else
            {
                Logger.LogError("❌ Account Error: {Content}", content);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("❌ Account Access Failed: {Message}", ex.Message);
        }
    }

    private async Task TestWorkersAccess(HttpClient httpClient, WorkersAISettings settings)
    {
        Logger.LogInformation("\n2. TESTING WORKERS SERVICE ACCESS");
        try
        {
            var response = await httpClient.GetAsync($"{settings.BaseUrl}/accounts/{settings.AccountId}/workers");
            var content = await response.Content.ReadAsStringAsync();
            
            Logger.LogInformation("Workers Status: {Status}", response.StatusCode);
            Logger.LogInformation("Workers Response: {Content}", content);
        }
        catch (Exception ex)
        {
            Logger.LogError("❌ Workers Access Failed: {Message}", ex.Message);
        }
    }

    private async Task TestWorkersAIEndpoints(HttpClient httpClient, WorkersAISettings settings)
    {
        Logger.LogInformation("\n3. TESTING WORKERS AI ENDPOINTS");
        
        var endpointsToTest = new[]
        {
            $"{settings.BaseUrl}/accounts/{settings.AccountId}/ai",
            $"{settings.BaseUrl}/accounts/{settings.AccountId}/workers/ai",
            $"{settings.BaseUrl}/accounts/{settings.AccountId}/ai/models",
            $"{settings.BaseUrl}/accounts/{settings.AccountId}/ai/run"
        };

        foreach (var endpoint in endpointsToTest)
        {
            try
            {
                Logger.LogInformation("Testing endpoint: {Endpoint}", endpoint);
                var response = await httpClient.GetAsync(endpoint);
                var content = await response.Content.ReadAsStringAsync();
                
                Logger.LogInformation("Status: {Status}", response.StatusCode);
                Logger.LogInformation("Response: {Content}", content);
                
                // Check for specific error codes that indicate what's wrong
                if (content.Contains("7000"))
                {
                    Logger.LogWarning("🔍 Error 7000: No route for that URI - Workers AI may not be enabled");
                }
                if (content.Contains("7003"))
                {
                    Logger.LogWarning("🔍 Error 7003: Could not route - Invalid object identifier or service not available");
                }
                if (content.Contains("10000"))
                {
                    Logger.LogWarning("🔍 Error 10000: Method not allowed - May need POST instead of GET");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("❌ Endpoint {Endpoint} failed: {Message}", endpoint, ex.Message);
            }
        }
    }

    private async Task TestDirectModelInvocation(HttpClient httpClient, WorkersAISettings settings)
    {
        Logger.LogInformation("\n4. TESTING DIRECT MODEL INVOCATION");
        
        var testPayload = new
        {
            prompt = "Hello",
            max_tokens = 10
        };
        
        var jsonPayload = System.Text.Json.JsonSerializer.Serialize(testPayload);
        
        var modelsToTest = new[]
        {
            "@cf/meta/llama-2-7b-chat-int8",
            "@hf/meta-llama/llama-2-7b-chat-hf", 
            "@cf/meta/llama-3.1-8b-instruct",
            "llama-2-7b-chat-int8"
        };

        foreach (var model in modelsToTest)
        {
            try
            {
                var endpoint = $"{settings.BaseUrl}/accounts/{settings.AccountId}/ai/run/{model}";
                Logger.LogInformation("Testing model: {Model} at {Endpoint}", model, endpoint);
                
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Logger.LogInformation("Model {Model} Status: {Status}", model, response.StatusCode);
                Logger.LogInformation("Model {Model} Response: {Response}", model, responseContent);
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.LogInformation("🎉 SUCCESS! Model {Model} worked!", model);
                    return; // Found a working model
                }
                else
                {
                    // Analyze the specific error
                    if (responseContent.Contains("model not found") || responseContent.Contains("7007"))
                    {
                        Logger.LogWarning("Model {Model} not found or not available", model);
                    }
                    else if (responseContent.Contains("7000"))
                    {
                        Logger.LogWarning("Model {Model} - No route (Workers AI may not be enabled)", model);
                    }
                    else if (responseContent.Contains("authentication") || responseContent.Contains("10000"))
                    {
                        Logger.LogWarning("Model {Model} - Authentication/permission issue", model);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("❌ Model {Model} failed: {Message}", model, ex.Message);
            }
        }
    }

    private async Task CheckWorkersAIActivation(HttpClient httpClient, WorkersAISettings settings)
    {
        Logger.LogInformation("\n5. CHECKING WORKERS AI ACTIVATION STATUS");
        
        // Try to access Workers AI dashboard or settings
        var dashboardEndpoints = new[]
        {
            $"{settings.BaseUrl}/accounts/{settings.AccountId}/workers/services",
            $"{settings.BaseUrl}/accounts/{settings.AccountId}/workers/subdomain",
            $"{settings.BaseUrl}/accounts/{settings.AccountId}/workers/settings",
        };

        foreach (var endpoint in dashboardEndpoints)
        {
            try
            {
                var response = await httpClient.GetAsync(endpoint);
                var content = await response.Content.ReadAsStringAsync();
                
                Logger.LogInformation("Workers Info Endpoint: {Endpoint}", endpoint);
                Logger.LogInformation("Status: {Status}", response.StatusCode);
                Logger.LogInformation("Response: {Response}", content);
            }
            catch (Exception ex)
            {
                Logger.LogError("❌ Workers info endpoint failed: {Message}", ex.Message);
            }
        }
        
        // Final diagnosis
        Logger.LogInformation("\n🔍 DIAGNOSIS SUMMARY:");
        Logger.LogInformation("1. If you see 'No route for that URI' (7000), Workers AI is likely not enabled");
        Logger.LogInformation("2. If you see 'Could not route' (7003), the service may not be available on your account");
        Logger.LogInformation("3. If you see 'Method not allowed' (10000), authentication might be wrong");
        Logger.LogInformation("\n💡 NEXT STEPS:");
        Logger.LogInformation("1. Go to Cloudflare Dashboard → Workers & Pages → Overview");
        Logger.LogInformation("2. Check if Workers AI is enabled/visible");
        Logger.LogInformation("3. Try creating a Workers AI API token specifically from the AI section");
        Logger.LogInformation("4. Ensure your account has Workers AI access (should be free tier available)");
    }
}