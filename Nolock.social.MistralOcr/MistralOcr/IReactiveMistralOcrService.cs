namespace Nolock.social.MistralOcr;

/// <summary>
/// Reactive interface for Mistral OCR operations using Observable streams
/// </summary>
public interface IReactiveMistralOcrService
{
    /// <summary>
    /// Process a stream of image URLs and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageUrls(IObservable<string> imageUrls, string? prompt = null);

    /// <summary>
    /// Process a stream of image data URLs and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageDataUrls(IObservable<string> dataUrls, string? prompt = null);

    /// <summary>
    /// Process a stream of image bytes and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageBytes(IObservable<(byte[] imageBytes, string mimeType)> images, string? prompt = null);

    /// <summary>
    /// Process a stream of image streams and return OCR results
    /// </summary>
    IObservable<MistralOcrResult> ProcessImageStreams(IObservable<(Stream imageStream, string mimeType)> streams, string? prompt = null);

    /// <summary>
    /// Process images in batches for better throughput
    /// </summary>
    IObservable<IList<MistralOcrResult>> ProcessImageUrlsBatch(IObservable<string> imageUrls, int batchSize, string? prompt = null);

    /// <summary>
    /// Process images with custom error handling
    /// </summary>
    IObservable<(string ImageUrl, MistralOcrResult? Result, Exception? Error)> ProcessImageUrlsWithErrors(
        IObservable<string> imageUrls,
        string? prompt = null);
}