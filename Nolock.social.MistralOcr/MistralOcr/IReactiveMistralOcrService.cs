namespace Nolock.social.MistralOcr;

/// <summary>
/// Reactive interface for Mistral OCR operations using Observable streams
/// </summary>
public interface IReactiveMistralOcrService
{
    /// <summary>
    /// Process a stream of image data URLs and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageDataItems(IObservable<(string url, string mimeType)> dataItems);

    /// <summary>
    /// Process a stream of image bytes and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageBytes(IObservable<(byte[] imageBytes, string mimeType)> images);

    /// <summary>
    /// Process a stream of image streams and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageStreams(IObservable<(Stream imageStream, string mimeType)> streams);

}