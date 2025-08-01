namespace Nolock.social.MistralOcr;

public interface IMistralOcrService
{
    /// <summary>
    /// Performs OCR on an image from a URL
    /// </summary>
    Task<MistralOcrResult> ProcessImageAsync(string imageUrl, string? prompt = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs OCR on an image from a base64 data URL
    /// </summary>
    Task<MistralOcrResult> ProcessImageDataItemAsync((Uri url, string mimeType) dataItem, string? prompt = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs OCR on an image from a byte array
    /// </summary>
    Task<MistralOcrResult> ProcessImageBytesAsync(byte[] imageBytes, string mimeType, string? prompt = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs OCR on an image from a stream
    /// </summary>
    Task<MistralOcrResult> ProcessImageStreamAsync(Stream imageStream, string mimeType, string? prompt = null, CancellationToken cancellationToken = default);
}