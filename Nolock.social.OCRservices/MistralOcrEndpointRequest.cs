namespace Nolock.social.OCRservices;

public record MistralOcrEndpointRequest
{
    public string? ImageUrl { get; init; }
    public string? ImageDataUrl { get; init; }
    public string? Prompt { get; init; }
}