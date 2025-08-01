using System.Text.Json.Serialization;

namespace Nolock.social.MistralOcr.Models;

public class OcrRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "mistral-ocr-latest";

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("document")]
    public OcrDocument Document { get; set; } = null!;

    [JsonPropertyName("pages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? Pages { get; set; }

    [JsonPropertyName("include_image_base64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeImageBase64 { get; set; }

    [JsonPropertyName("image_limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ImageLimit { get; set; }

    [JsonPropertyName("image_min_size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ImageMinSize { get; set; }

    [JsonPropertyName("bbox_annotation_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormat? BboxAnnotationFormat { get; set; }

    [JsonPropertyName("document_annotation_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormat? DocumentAnnotationFormat { get; set; }
}

public abstract class OcrDocument
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public class FileChunk : OcrDocument
{
    public override string Type => "file";

    [JsonPropertyName("data")]
    public string Data { get; set; } = null!;

    [JsonPropertyName("file_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileName { get; set; }
}

public class ImageUrlChunk : OcrDocument
{
    public override string Type => "image_url";

    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; } = null!;

    [JsonPropertyName("image_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImageName { get; set; }
}

public class DocumentUrlChunk : OcrDocument
{
    public override string Type => "document_url";

    [JsonPropertyName("document_url")]
    public string DocumentUrl { get; set; } = null!;

    [JsonPropertyName("document_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocumentName { get; set; }
}

public class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_schema";

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? JsonSchema { get; set; }
}

public class OcrResponse
{
    [JsonPropertyName("pages")]
    public List<OcrPageObject> Pages { get; set; } = new();

    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("document_annotation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocumentAnnotation { get; set; }

    [JsonPropertyName("usage_info")]
    public OcrUsageInfo UsageInfo { get; set; } = null!;
}

public class OcrPageObject
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("markdown")]
    public string Markdown { get; set; } = null!;

    [JsonPropertyName("images")]
    public List<OcrImageObject> Images { get; set; } = new();

    [JsonPropertyName("dimensions")]
    public OcrPageDimensions? Dimensions { get; set; }
}

public class OcrImageObject
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("base64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Base64 { get; set; }

    [JsonPropertyName("bounding_box")]
    public OcrBoundingBox BoundingBox { get; set; } = null!;
}

public class OcrBoundingBox
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}

public class OcrPageDimensions
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public class OcrUsageInfo
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}