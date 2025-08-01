namespace Nolock.social.OCRservices.Services;

/// <summary>
/// Handles OCR processing requests end-to-end
/// </summary>
public interface IOcrRequestHandler
{
    /// <summary>
    /// Processes an OCR request from image stream to structured data
    /// </summary>
    /// <param name="imageStream">The image stream to process</param>
    /// <param name="documentTypeString">The document type as a string</param>
    /// <returns>The processing result</returns>
    Task<IResult> HandleAsync(Stream imageStream, string? documentTypeString);
    
    /// <summary>
    /// Processes a receipt image and returns structured receipt data
    /// </summary>
    /// <param name="imageStream">The receipt image stream to process</param>
    /// <returns>The receipt processing result</returns>
    Task<IResult> HandleReceiptAsync(Stream imageStream);
    
    /// <summary>
    /// Processes a check image and returns structured check data
    /// </summary>
    /// <param name="imageStream">The check image stream to process</param>
    /// <returns>The check processing result</returns>
    Task<IResult> HandleCheckAsync(Stream imageStream);
}