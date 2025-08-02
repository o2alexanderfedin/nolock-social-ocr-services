using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nolock.social.CloudflareAI.Models;

/// <summary>
/// Converter that handles both string and object responses from Cloudflare AI
/// </summary>
public class FlexibleResponseConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                // If it's already a string, return it directly
                return reader.GetString();
                
            case JsonTokenType.StartObject:
                // If it's an object, serialize it back to string
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    return doc.RootElement.GetRawText();
                }
                
            case JsonTokenType.Null:
                return null;
                
            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }
}