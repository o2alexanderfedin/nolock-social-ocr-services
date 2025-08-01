using Microsoft.Extensions.Configuration;
using Nolock.social.CloudflareAI.Configuration;

namespace Nolock.social.CloudflareAI.IntegrationTests;

public static class TestConfiguration
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();

    public static WorkersAISettings GetSettings()
    {
        var accountId = Configuration["CLOUDFLARE_ACCOUNT_ID"] 
            ?? throw new InvalidOperationException("CLOUDFLARE_ACCOUNT_ID environment variable is required");
        
        var apiToken = Configuration["CLOUDFLARE_API_TOKEN"] 
            ?? throw new InvalidOperationException("CLOUDFLARE_API_TOKEN environment variable is required");

        return new WorkersAISettings
        {
            AccountId = accountId,
            ApiToken = apiToken,
            Timeout = TimeSpan.FromMinutes(5) // Longer timeout for integration tests
        };
    }

    public static bool AreCredentialsAvailable()
    {
        try
        {
            GetSettings();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}