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
}