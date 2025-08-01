namespace Nolock.social.MistralOcr;

/// <summary>
/// Reactive interface for Mistral OCR operations using Observable streams
/// </summary>
public interface IReactiveMistralOcrService
{
    /// <summary>
    /// Process a stream of image data URLs (as Uri objects) and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageDataItems(IObservable<(Uri url, string mimeType)> dataItems, string? prompt = null);

    /// <summary>
    /// Process a stream of image bytes and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageBytes(IObservable<(byte[] imageBytes, string mimeType)> images, string? prompt = null);

    /// <summary>
    /// Process a stream of image streams and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageStreams(IObservable<(Stream imageStream, string mimeType)> streams, string? prompt = null);

}