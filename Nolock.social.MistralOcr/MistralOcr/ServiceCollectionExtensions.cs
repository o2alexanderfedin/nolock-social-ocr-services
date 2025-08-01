using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Nolock.social.MistralOcr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMistralOcr(
        this IServiceCollection services, 
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Configure options
        services.Configure<MistralOcrConfiguration>(
            configuration.GetSection("MistralOcr"));

        // Add HTTP client
        services.AddHttpClient<IMistralOcrService, MistralOcrApiService>((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<MistralOcrConfiguration>>().Value;
            client.BaseAddress = new Uri(config.BaseUrl);
            client.Timeout = config.Timeout;
        });

        return services;
    }

    public static IServiceCollection AddMistralOcr(
        this IServiceCollection services,
        Action<MistralOcrConfiguration> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Configure options
        services.Configure(configureOptions);

        // Add HTTP client
        services.AddHttpClient<IMistralOcrService, MistralOcrApiService>();

        return services;
    }
}