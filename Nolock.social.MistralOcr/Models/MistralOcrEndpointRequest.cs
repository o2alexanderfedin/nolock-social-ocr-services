namespace Nolock.social.MistralOcr.Models;

public sealed class MistralOcrEndpointRequest
{
    public string? ImageUrl { get; set; }
    public string? ImageDataUrl { get; set; }
    public string? Prompt { get; set; }
}