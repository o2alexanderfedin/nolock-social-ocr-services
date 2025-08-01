namespace Nolock.social.OCRservices.Core.Models;

/// <summary>
/// Response model for async OCR processing endpoint
/// </summary>
#pragma warning disable CA1812 // Used by OpenAPI/Swagger documentation
public sealed class OcrAsyncResponse
#pragma warning restore CA1812
{
    /// <summary>
    /// Type of document processed (check or receipt)
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;
    
    /// <summary>
    /// Raw OCR text extracted from the image
    /// </summary>
    public string OcrText { get; set; } = string.Empty;
    
    /// <summary>
    /// Structured data extracted based on document type
    /// </summary>
    public object? ExtractedData { get; set; }
    
    /// <summary>
    /// Confidence score of the extraction (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Total processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}