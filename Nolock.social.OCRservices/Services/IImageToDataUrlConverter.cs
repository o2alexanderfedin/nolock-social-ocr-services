namespace Nolock.social.OCRservices.Services;

/// <summary>
/// Converts image streams to data URLs for OCR processing
/// </summary>
public interface IImageToDataUrlConverter
{
    /// <summary>
    /// Converts an image stream to a data URL with MIME type detection
    /// </summary>
    /// <param name="imageStream">The image stream to convert</param>
    /// <returns>A tuple containing the data URL and detected MIME type</returns>
    Task<(string dataUrl, string mimeType)> ConvertAsync(Stream imageStream);
}