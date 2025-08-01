using System.Text.Json;
using System.Text.Json.Serialization;
using Nolock.social.MistralOcr.Models;

namespace Nolock.social.MistralOcr.Converters;

public sealed class MistralContentJsonConverter : JsonConverter<MistralContent>
{
    public override MistralContent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("type", out var typeElement))
        {
            throw new JsonException("Missing 'type' property in MistralContent");
        }

        var type = typeElement.GetString();
        
        return type switch
        {
            "text" => JsonSerializer.Deserialize<MistralTextContent>(root.GetRawText(), options),
            "image_url" => JsonSerializer.Deserialize<MistralImageContent>(root.GetRawText(), options),
            _ => throw new JsonException($"Unknown MistralContent type: {type}")
        };
    }

    public override void Write(Utf8JsonWriter writer, MistralContent value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        
        switch (value)
        {
            case MistralTextContent textContent:
                JsonSerializer.Serialize(writer, textContent, options);
                break;
            case MistralImageContent imageContent:
                JsonSerializer.Serialize(writer, imageContent, options);
                break;
            default:
                throw new JsonException($"Unknown MistralContent type: {value.GetType()}");
        }
    }
}