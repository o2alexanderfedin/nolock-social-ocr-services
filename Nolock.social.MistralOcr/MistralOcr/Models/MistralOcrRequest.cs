using System.Text.Json.Serialization;

namespace Nolock.social.MistralOcr.Models;

public sealed class MistralOcrRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "mistral-ocr-latest";

    [JsonPropertyName("messages")]
    public List<MistralMessage> Messages { get; set; } = new();

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.0;

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public sealed class MistralMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public List<MistralContent> Content { get; set; } = new();
}

public abstract class MistralContent
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public sealed class MistralTextContent : MistralContent
{
    public override string Type => "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class MistralImageContent : MistralContent
{
    public override string Type => "image_url";

    [JsonPropertyName("image_url")]
    public MistralImageUrl ImageUrl { get; set; } = new();
}

public sealed class MistralImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = "auto";
}