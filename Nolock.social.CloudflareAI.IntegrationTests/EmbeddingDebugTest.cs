namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class EmbeddingDebugTest : BaseIntegrationTest
{
    [Fact]
    public async Task Debug_EmbeddingResponse_Format()
    {
        var request = new EmbeddingRequest
        {
            Text = ["Hello world"]
        };

        try
        {
            using var response = await Client.RunRawAsync(EmbeddingModels.BGE_Small_EN_V1_5, request);
            var content = await response.Content.ReadAsStringAsync();
            
            Logger.LogInformation("Embedding Status: {Status}", response.StatusCode);
            Logger.LogInformation("Embedding Response: {Response}", content);
            
            if (response.IsSuccessStatusCode)
            {
                Logger.LogInformation("✅ Embedding API call succeeded - analyzing response format");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("❌ Embedding failed: {Message}", ex.Message);
        }
    }
}