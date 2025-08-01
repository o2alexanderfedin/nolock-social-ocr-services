using System.Text.Json;
using System.Text.Json.Serialization;
using Nolock.social.MistralOcr.Models;

namespace Nolock.social.MistralOcr.Converters;

public class OcrDocumentJsonConverter : JsonConverter<OcrDocument>
{
    public override OcrDocument? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException("Deserialization is not implemented");
    }

    public override void Write(Utf8JsonWriter writer, OcrDocument value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        switch (value)
        {
            case ImageUrlChunk imageUrl:
                writer.WriteString("type", "image_url");
                writer.WriteString("image_url", imageUrl.ImageUrl);
                if (imageUrl.ImageName != null)
                    writer.WriteString("image_name", imageUrl.ImageName);
                break;
                
            case DocumentUrlChunk docUrl:
                writer.WriteString("type", "document_url");
                writer.WriteString("document_url", docUrl.DocumentUrl);
                if (docUrl.DocumentName != null)
                    writer.WriteString("document_name", docUrl.DocumentName);
                break;
                
            case FileChunk file:
                writer.WriteString("type", "file");
                writer.WriteString("data", file.Data);
                if (file.FileName != null)
                    writer.WriteString("file_name", file.FileName);
                break;
        }
        
        writer.WriteEndObject();
    }
}