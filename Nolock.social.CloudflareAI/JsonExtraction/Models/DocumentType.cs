using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nolock.social.CloudflareAI.JsonExtraction.Models;

/// <summary>
/// Type of document for OCR processing
/// </summary>
[JsonConverter(typeof(DocumentTypeJsonConverter))]
[TypeConverter(typeof(DocumentTypeConverter))]
public enum DocumentType
{
    /// <summary>
    /// Bank check or money order
    /// </summary>
    Check,
    
    /// <summary>
    /// Receipt from a purchase or transaction
    /// </summary>
    Receipt
}

/// <summary>
/// Custom JSON converter for DocumentType enum to ensure lowercase serialization
/// </summary>
public class DocumentTypeJsonConverter : JsonConverter<DocumentType>
{
    public override DocumentType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "check" => DocumentType.Check,
            "receipt" => DocumentType.Receipt,
            _ => throw new JsonException($"Unknown document type: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, DocumentType value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        
        var stringValue = value switch
        {
            DocumentType.Check => "check",
            DocumentType.Receipt => "receipt",
            _ => throw new JsonException($"Unknown document type: {value}")
        };
        writer.WriteStringValue(stringValue);
    }
}

/// <summary>
/// Base class for OCR extraction requests
/// </summary>
public class OcrExtractionRequest
{
    /// <summary>
    /// Type of document to extract
    /// </summary>
    [JsonPropertyName("document_type")]
    public DocumentType DocumentType { get; set; }
    
    /// <summary>
    /// OCR text or base64 encoded image
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
    
    /// <summary>
    /// Whether content is base64 encoded image
    /// </summary>
    [JsonPropertyName("is_image")]
    public bool IsImage { get; set; }
    
    /// <summary>
    /// Optional model to use for extraction
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    
    /// <summary>
    /// Use simplified schema for faster extraction
    /// </summary>
    [JsonPropertyName("use_simple_schema")]
    public bool UseSimpleSchema { get; set; }
}

/// <summary>
/// Base response for OCR extraction
/// </summary>
public class OcrExtractionResponse<T>
{
    /// <summary>
    /// Type of document extracted
    /// </summary>
    [JsonPropertyName("document_type")]
    public DocumentType DocumentType { get; set; }
    
    /// <summary>
    /// Whether extraction was successful
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    /// <summary>
    /// Extracted data
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }
    
    /// <summary>
    /// Error message if extraction failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    /// <summary>
    /// Extraction confidence score
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
    
    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    [JsonPropertyName("processing_time_ms")]
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// Batch OCR extraction request
/// </summary>
public class BatchOcrExtractionRequest
{
    /// <summary>
    /// Type of documents to extract
    /// </summary>
    [JsonPropertyName("document_type")]
    public DocumentType DocumentType { get; set; }
    
    /// <summary>
    /// Array of OCR texts or base64 encoded images
    /// </summary>
    [JsonPropertyName("contents")]
    public List<string> Contents { get; set; } = new();
    
    /// <summary>
    /// Whether contents are base64 encoded images
    /// </summary>
    [JsonPropertyName("is_image")]
    public bool IsImage { get; set; }
    
    /// <summary>
    /// Use simplified schema for faster extraction
    /// </summary>
    [JsonPropertyName("use_simple_schema")]
    public bool UseSimpleSchema { get; set; }
    
    /// <summary>
    /// Maximum concurrent extractions
    /// </summary>
    [JsonPropertyName("max_concurrency")]
    public int MaxConcurrency { get; set; } = 3;
}

/// <summary>
/// Batch OCR extraction response
/// </summary>
public class BatchOcrExtractionResponse<T>
{
    /// <summary>
    /// Type of documents extracted
    /// </summary>
    [JsonPropertyName("document_type")]
    public DocumentType DocumentType { get; set; }
    
    /// <summary>
    /// Individual extraction results
    /// </summary>
    [JsonPropertyName("results")]
    public List<OcrExtractionResponse<T>> Results { get; set; } = new();
    
    /// <summary>
    /// Total processing time in milliseconds
    /// </summary>
    [JsonPropertyName("total_processing_time_ms")]
    public long TotalProcessingTimeMs { get; set; }
    
    /// <summary>
    /// Number of successful extractions
    /// </summary>
    [JsonPropertyName("success_count")]
    public int SuccessCount => Results.Count(r => r.Success);
    
    /// <summary>
    /// Number of failed extractions
    /// </summary>
    [JsonPropertyName("failure_count")]
    public int FailureCount => Results.Count(r => !r.Success);
    
    /// <summary>
    /// Average confidence score for successful extractions
    /// </summary>
    [JsonPropertyName("average_confidence")]
    public double AverageConfidence => Results.Where(r => r.Success).Average(r => r.Confidence);
}