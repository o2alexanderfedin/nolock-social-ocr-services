using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Configuration;
using Nolock.social.CloudflareAI.Interfaces;
using Xunit;

namespace Nolock.social.CloudflareAI.Tests;

public sealed class WorkersAIFactoryTests
{
    [Fact]
    public void CreateWorkersAI_WithSettings_ReturnsValidClient()
    {
        var settings = new WorkersAISettings
        {
            AccountId = "test-account",
            ApiToken = "test-token"
        };

        using var client = WorkersAIFactory.CreateWorkersAI(settings);

        Assert.NotNull(client);
        Assert.IsAssignableFrom<IWorkersAI>(client);
    }

    [Fact]
    public void CreateWorkersAI_WithAccountIdAndToken_ReturnsValidClient()
    {
        using var client = WorkersAIFactory.CreateWorkersAI("test-account", "test-token");

        Assert.NotNull(client);
        Assert.IsAssignableFrom<IWorkersAI>(client);
    }

    [Fact]
    public void CreateWorkersAI_WithNullSettings_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => WorkersAIFactory.CreateWorkersAI(null!));
    }

    [Fact]
    public void CreateWorkersAI_WithCustomHttpClient_UsesProvidedClient()
    {
        using var httpClient = new HttpClient();
        var settings = new WorkersAISettings
        {
            AccountId = "test-account",
            ApiToken = "test-token"
        };

        using var client = WorkersAIFactory.CreateWorkersAI(settings, httpClient);

        Assert.NotNull(client);
        Assert.IsAssignableFrom<IWorkersAI>(client);
    }

    [Fact]
    public void AddWorkersAI_WithConfigureAction_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddWorkersAI(options =>
        {
            options.AccountId = "test-account";
            options.ApiToken = "test-token";
        });

        using var serviceProvider = services.BuildServiceProvider();
        var workersAI = serviceProvider.GetService<IWorkersAI>();

        Assert.NotNull(workersAI);
    }

    [Fact]
    public void AddWorkersAI_WithSettings_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var settings = new WorkersAISettings
        {
            AccountId = "test-account",
            ApiToken = "test-token"
        };

        services.AddWorkersAI(settings);

        using var serviceProvider = services.BuildServiceProvider();
        var workersAI = serviceProvider.GetService<IWorkersAI>();

        Assert.NotNull(workersAI);
    }

    [Fact]
    public void AddWorkersAI_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => 
            services.AddWorkersAI(options => { }));
    }

    [Fact]
    public void AddWorkersAI_WithNullConfigureAction_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => 
            services.AddWorkersAI((Action<WorkersAISettings>)null!));
    }
}