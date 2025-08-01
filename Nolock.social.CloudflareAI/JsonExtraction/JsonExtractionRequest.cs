using System.Text.Json.Serialization;
using Nolock.social.CloudflareAI.Models;

namespace Nolock.social.CloudflareAI.JsonExtraction;

/// <summary>
/// Request for JSON extraction from text using Cloudflare Workers AI
/// </summary>
public sealed record JsonExtractionRequest
{
    /// <summary>
    /// The text to extract JSON from
    /// </summary>
    [JsonPropertyName("prompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prompt { get; init; }

    /// <summary>
    /// Messages for chat-based extraction
    /// </summary>
    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Message[]? Messages { get; init; }

    /// <summary>
    /// Maximum tokens to generate
    /// </summary>
    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; } = 1000;

    /// <summary>
    /// Temperature for generation (0-1)
    /// </summary>
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; } = 0.1; // Low temperature for consistent JSON

    /// <summary>
    /// Response format specification
    /// </summary>
    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormat? ResponseFormat { get; init; }
}

/// <summary>
/// Response format specification for JSON mode
/// </summary>
public sealed record ResponseFormat
{
    /// <summary>
    /// The type of response format (should be "json_schema" for Cloudflare)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "json_schema";

    /// <summary>
    /// JSON schema for structured output (required for json_schema type)
    /// </summary>
    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? JsonSchema { get; init; }
}