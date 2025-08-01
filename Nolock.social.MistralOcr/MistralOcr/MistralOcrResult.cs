namespace Nolock.social.MistralOcr;

public sealed class MistralOcrResult
{
    public string Text { get; init; } = string.Empty;
    public string ModelUsed { get; init; } = string.Empty;
    public int TotalTokens { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    
    public static MistralOcrResult Empty => new();
}