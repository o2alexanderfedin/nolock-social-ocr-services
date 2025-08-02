using System.Text.Json.Serialization;

namespace Nolock.social.CloudflareAI.Models;

/// <summary>
/// Base response from Cloudflare Workers AI API
/// </summary>
/// <typeparam name="T">Type of the result data</typeparam>
public sealed record ApiResponse<T>
{
    [JsonPropertyName("result")]
    public T? Result { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("errors")]
    public ApiError[]? Errors { get; init; }

    [JsonPropertyName("messages")]
    public string[]? Messages { get; init; }
}

/// <summary>
/// API error information
/// </summary>
public sealed record ApiError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Text generation request
/// </summary>
public sealed record TextGenerationRequest
{
    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Message[]? Messages { get; init; }

    [JsonPropertyName("prompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prompt { get; init; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; init; }

    [JsonPropertyName("stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stream { get; init; }
}

/// <summary>
/// Chat message
/// </summary>
public sealed record Message
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Text generation response
/// </summary>
public sealed record TextGenerationResponse
{
    [JsonPropertyName("response")]
    [JsonConverter(typeof(FlexibleResponseConverter))]
    public string Response { get; init; } = string.Empty;

    [JsonPropertyName("generated_text")]
    public string GeneratedText { get; init; } = string.Empty;
}

/// <summary>
/// Image generation request
/// </summary>
public sealed record ImageGenerationRequest
{
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("num_steps")]
    public int? NumSteps { get; init; }

    [JsonPropertyName("strength")]
    public double? Strength { get; init; }

    [JsonPropertyName("guidance")]
    public double? Guidance { get; init; }
}

/// <summary>
/// Image generation response
/// </summary>
public sealed record ImageGenerationResponse
{
    [JsonPropertyName("image")]
    public byte[]? Image { get; init; }
}

/// <summary>
/// Text embedding request
/// </summary>
public sealed record EmbeddingRequest
{
    [JsonPropertyName("text")]
    public string[]? Text { get; init; }
}

/// <summary>
/// Text embedding response
/// </summary>
public sealed record EmbeddingResponse
{
    [JsonPropertyName("data")]
    public double[][]? Data { get; init; }
    
    [JsonPropertyName("shape")]
    public int[]? Shape { get; init; }
    
    [JsonPropertyName("pooling")]
    public string? Pooling { get; init; }
}

/// <summary>
/// Embedding data (legacy - kept for backward compatibility)
/// </summary>
public sealed record EmbeddingData
{
    [JsonPropertyName("embedding")]
    public double[]? Embedding { get; init; }
}