using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nolock.social.CloudflareAI.Configuration;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.Services;

namespace Nolock.social.CloudflareAI;

/// <summary>
/// Factory for creating WorkersAI client instances
/// </summary>
public static class WorkersAIFactory
{
    /// <summary>
    /// Create a WorkersAI client with the specified settings
    /// </summary>
    /// <param name="settings">Configuration settings</param>
    /// <param name="httpClient">Optional HTTP client (will create new if not provided)</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>WorkersAI client instance</returns>
    public static IWorkersAI CreateWorkersAI(
        WorkersAISettings settings,
        HttpClient? httpClient = null,
        ILogger<WorkersAIClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var client = httpClient ?? new HttpClient();
        var options = Options.Create(settings);
        var loggerInstance = logger ?? CreateNullLogger();

        return new WorkersAIClient(client, options, loggerInstance);
    }

    /// <summary>
    /// Create a WorkersAI client with account ID and API token
    /// </summary>
    /// <param name="accountId">Cloudflare account ID</param>
    /// <param name="apiToken">Cloudflare API token</param>
    /// <param name="httpClient">Optional HTTP client</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>WorkersAI client instance</returns>
    public static IWorkersAI CreateWorkersAI(
        string accountId,
        string apiToken,
        HttpClient? httpClient = null,
        ILogger<WorkersAIClient>? logger = null)
    {
        var settings = new WorkersAISettings
        {
            AccountId = accountId,
            ApiToken = apiToken
        };

        return CreateWorkersAI(settings, httpClient, logger);
    }

    private static ILogger<WorkersAIClient> CreateNullLogger()
    {
        return new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider()
            .GetRequiredService<ILogger<WorkersAIClient>>();
    }
}

/// <summary>
/// Extension methods for dependency injection registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add WorkersAI client to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureSettings">Settings configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddWorkersAI(
        this IServiceCollection services,
        Action<WorkersAISettings> configureSettings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureSettings);

        services.Configure(configureSettings);
        services.AddHttpClient<IWorkersAI, WorkersAIClient>();

        return services;
    }

    /// <summary>
    /// Add WorkersAI client to the service collection with settings
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="settings">WorkersAI settings</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddWorkersAI(
        this IServiceCollection services,
        WorkersAISettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.Configure<WorkersAISettings>(options =>
        {
            if (settings.AccountId != null) options.AccountId = settings.AccountId;
            if (settings.ApiToken != null) options.ApiToken = settings.ApiToken;
            options.BaseUrl = settings.BaseUrl;
            options.Timeout = settings.Timeout;
            options.MaxRetryAttempts = settings.MaxRetryAttempts;
        });
        
        services.AddHttpClient<IWorkersAI, WorkersAIClient>();
        return services;
    }
}