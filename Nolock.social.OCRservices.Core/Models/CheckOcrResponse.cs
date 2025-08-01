using Nolock.social.CloudflareAI.JsonExtraction.Models;

namespace Nolock.social.OCRservices.Core.Models;

/// <summary>
/// Response model for check OCR processing endpoint
/// </summary>
public sealed class CheckOcrResponse
{
    /// <summary>
    /// Raw OCR text extracted from the check image
    /// </summary>
    public string OcrText { get; set; } = string.Empty;
    
    /// <summary>
    /// Structured check data extracted from the OCR text
    /// </summary>
    public Check? CheckData { get; set; }
    
    /// <summary>
    /// Confidence score of the extraction (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Total processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }
    
    /// <summary>
    /// The OCR model used for text extraction
    /// </summary>
    public string? ModelUsed { get; set; }
    
    /// <summary>
    /// Total tokens consumed during processing
    /// </summary>
    public int? TotalTokens { get; set; }
    
    /// <summary>
    /// Indicates if the extraction was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if extraction failed
    /// </summary>
    public string? Error { get; set; }
}