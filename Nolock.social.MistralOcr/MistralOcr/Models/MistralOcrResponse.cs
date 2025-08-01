using System.Text.Json.Serialization;

namespace Nolock.social.MistralOcr.Models;

public sealed class MistralOcrResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<MistralChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public MistralUsage? Usage { get; set; }
}

public sealed class MistralChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public MistralResponseMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class MistralResponseMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class MistralUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}

public sealed class MistralOcrError
{
    [JsonPropertyName("error")]
    public MistralErrorDetail? Error { get; set; }
    
    [JsonPropertyName("detail")]
    public List<MistralValidationError>? Detail { get; set; }
}

public sealed class MistralErrorDetail
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public sealed class MistralValidationError
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("loc")]
    public List<object> Location { get; set; } = new();
    
    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;
}