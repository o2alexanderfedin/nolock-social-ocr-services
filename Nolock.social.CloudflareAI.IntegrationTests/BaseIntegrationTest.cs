using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Interfaces;

namespace Nolock.social.CloudflareAI.IntegrationTests;

public abstract class BaseIntegrationTest : IDisposable
{
    protected readonly IWorkersAI Client;
    protected readonly ILogger Logger;
    private readonly ILoggerFactory _loggerFactory;

    protected BaseIntegrationTest()
    {
        if (!TestConfiguration.AreCredentialsAvailable())
        {
            throw new SkipException("Cloudflare credentials not available. Set CLOUDFLARE_ACCOUNT_ID and CLOUDFLARE_API_TOKEN environment variables.");
        }

        var settings = TestConfiguration.GetSettings();
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        Logger = _loggerFactory.CreateLogger(GetType());
        
        Client = WorkersAIFactory.CreateWorkersAI(settings, logger: Logger as ILogger<Services.WorkersAIClient>);
    }

    public virtual void Dispose()
    {
        Client?.Dispose();
        _loggerFactory?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}