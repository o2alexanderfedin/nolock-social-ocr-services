namespace Nolock.social.CloudflareAI.Configuration;

/// <summary>
/// Configuration settings for Cloudflare Workers AI client
/// </summary>
public sealed class WorkersAISettings
{
    /// <summary>
    /// Cloudflare account ID
    /// </summary>
    public required string AccountId { get; set; }

    /// <summary>
    /// Cloudflare API token
    /// </summary>
    public required string ApiToken { get; set; }

    /// <summary>
    /// Base URL for the Cloudflare Workers AI API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.cloudflare.com/client/v4";

    /// <summary>
    /// HTTP timeout for requests
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}