using Nolock.social.CloudflareAI.JsonExtraction.Models;

namespace Nolock.social.CloudflareAI.JsonExtraction.Interfaces;

/// <summary>
/// Interface for OCR document extraction using Cloudflare AI
/// </summary>
public interface IOcrExtractionService
{
    /// <summary>
    /// Extract structured data from OCR text based on document type
    /// </summary>
    Task<object> ExtractDocumentAsync(DocumentType documentType, string ocrText, bool useSimpleSchema = true);
    
    /// <summary>
    /// Process OCR extraction request
    /// </summary>
    Task<OcrExtractionResponse<object>> ProcessExtractionRequestAsync(OcrExtractionRequest request);
    
    /// <summary>
    /// Process batch OCR extraction request
    /// </summary>
    Task<BatchOcrExtractionResponse<object>> ProcessBatchExtractionRequestAsync(BatchOcrExtractionRequest request);
}