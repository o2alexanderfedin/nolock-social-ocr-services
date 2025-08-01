using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nolock.social.MistralOcr.IntegrationTests.Fixtures;

public class MistralOcrTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public IMistralOcrService MistralOcrService { get; }
    public IConfiguration Configuration { get; }

    public MistralOcrTestFixture()
    {
        // Build configuration from environment variables and test settings
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddJsonFile("appsettings.test.local.json", optional: true) // Local overrides
            .AddEnvironmentVariables();
        
        // Add MISTRAL_API_KEY support
        var mistralApiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
        if (!string.IsNullOrWhiteSpace(mistralApiKey))
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MistralOcr:ApiKey"] = mistralApiKey
            });
        }
        
        Configuration = configBuilder.Build();

        // Validate that API key is configured
        var apiKey = Configuration["MistralOcr:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Try alternate environment variable name
            apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Mistral API key not configured. Set the MISTRAL_API_KEY or MistralOcr__ApiKey environment variable or configure it in appsettings.test.json");
            }
        }

        // Build service provider with real Mistral API configuration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug));
        
        services.AddMistralOcr(Configuration);
        
        ServiceProvider = services.BuildServiceProvider();
        MistralOcrService = ServiceProvider.GetRequiredService<IMistralOcrService>();
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}