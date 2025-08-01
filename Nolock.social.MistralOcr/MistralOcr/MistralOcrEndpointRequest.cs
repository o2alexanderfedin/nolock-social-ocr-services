namespace Nolock.social.MistralOcr;

public sealed class MistralOcrEndpointRequest
{
    public string? ImageUrl { get; set; }
    public string? ImageDataUrl { get; set; }
    public string? Prompt { get; set; }
}