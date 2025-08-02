using System.Reactive.Linq;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.SchemaGeneration;
using Nolock.social.MistralOcr;

namespace Nolock.social.OCRservices.Pipelines;

/// <summary>
/// Simple pipeline for processing images through OCR and extracting structured models
/// </summary>
public class OcrToModelPipeline
{
    private readonly IMistralOcrService _ocrService;
    private readonly IWorkersAI _workersAI;

    public OcrToModelPipeline(
        IMistralOcrService ocrService,
        IWorkersAI workersAI)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _workersAI = workersAI ?? throw new ArgumentNullException(nameof(workersAI));
    }

    /// <summary>
    /// Process receipt image and extract Receipt model
    /// </summary>
    public async Task<Receipt> ProcessReceiptImage(Stream imageStream, string mimeType)
    {
        // Step 1: OCR
        var ocrResult = await _ocrService.ProcessImageStreamAsync(imageStream, mimeType).ConfigureAwait(false);

        // Step 2: Extract Receipt
        using var extractor = _workersAI.CreateJsonExtractor();
        var receipt = await extractor
            .ExtractFromType<Receipt>(ocrResult.Text)
            .FirstAsync();

        return receipt ?? throw new InvalidOperationException("Failed to extract receipt data");
    }

    /// <summary>
    /// Process check image and extract Check model
    /// </summary>
    public async Task<Check> ProcessCheckImage(Stream imageStream, string mimeType)
    {
        // Step 1: OCR
        var ocrResult = await _ocrService.ProcessImageStreamAsync(imageStream, mimeType).ConfigureAwait(false);

        // Step 2: Extract Check
        using var extractor = _workersAI.CreateJsonExtractor();
        var check = await extractor
            .ExtractFromType<Check>(ocrResult.Text)
            .FirstAsync();

        return check ?? throw new InvalidOperationException("Failed to extract check data");
    }


    /// <summary>
    /// Process receipt with reactive pattern
    /// </summary>
    public IObservable<Receipt> ProcessReceiptImageReactive(Stream imageStream, string mimeType)
    {
        return Observable.FromAsync(async () => await ProcessReceiptImage(imageStream, mimeType).ConfigureAwait(false));
    }

    /// <summary>
    /// Process check with reactive pattern
    /// </summary>
    public IObservable<Check> ProcessCheckImageReactive(Stream imageStream, string mimeType)
    {
        return Observable.FromAsync(async () => await ProcessCheckImage(imageStream, mimeType).ConfigureAwait(false));
    }
}