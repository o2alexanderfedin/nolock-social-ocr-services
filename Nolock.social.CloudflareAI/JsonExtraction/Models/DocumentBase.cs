using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Nolock.social.CloudflareAI.JsonExtraction.Models;

/// <summary>
/// Base class for document extraction results with common properties
/// </summary>
public abstract class DocumentExtractionBase
{
    [JsonPropertyName("isValidInput")]
    [Description("Indicates if the input appears to be a valid document image")]
    public bool? IsValidInput { get; set; }
    
    [JsonPropertyName("confidence")]
    [Description("Overall confidence score (0-1)")]
    public double Confidence { get; set; }
}

/// <summary>
/// Base class for document metadata with common properties
/// </summary>
public abstract class DocumentMetadata
{
    [JsonPropertyName("confidenceScore")]
    [Description("Overall confidence of extraction (0-1)")]
    public double ConfidenceScore { get; set; }
    
    [JsonPropertyName("sourceImageId")]
    [Description("Reference to the source image")]
    public string? SourceImageId { get; set; }
    
    [JsonPropertyName("warnings")]
    [Description("List of warning messages")]
    public List<string>? Warnings { get; set; }
}