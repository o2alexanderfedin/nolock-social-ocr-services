namespace Nolock.social.MistralOcr;

public sealed class MistralOcrConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.mistral.ai";
    public string Model { get; set; } = "mistral-ocr-latest";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}