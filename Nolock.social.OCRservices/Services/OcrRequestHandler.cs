using System.Reactive.Linq;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.Services;
using Nolock.social.MistralOcr;
using Nolock.social.OCRservices.Core.Models;

namespace Nolock.social.OCRservices.Services;

/// <summary>
/// Implementation of OCR request handling with separated concerns
/// </summary>
public sealed partial class OcrRequestHandler : IOcrRequestHandler
{
    private readonly IReactiveMistralOcrService _ocrService;
    private readonly OcrExtractionService _extractionService;
    private readonly IImageToDataUrlConverter _imageConverter;
    private readonly IDocumentTypeValidator _documentValidator;
    private readonly ILogger<OcrRequestHandler> _logger;

    public OcrRequestHandler(
        IReactiveMistralOcrService ocrService,
        OcrExtractionService extractionService,
        IImageToDataUrlConverter imageConverter,
        IDocumentTypeValidator documentValidator,
        ILogger<OcrRequestHandler> logger)
    {
        _ocrService = ocrService;
        _extractionService = extractionService;
        _imageConverter = imageConverter;
        _documentValidator = documentValidator;
        _logger = logger;
    }

    public async Task<IResult> HandleAsync(Stream imageStream, string? documentTypeString)
    {
        try
        {
            var validationResult = _documentValidator.ValidateDocumentType(documentTypeString);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.ErrorMessage);
            }

            var imageData = await ConvertImageToDataUrl(imageStream);
            var ocrResult = await ExtractTextFromImage(imageData);
            
            if (string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                return Results.Problem("Failed to extract text from image");
            }

            var structuredData = await ExtractStructuredData(ocrResult.Text, validationResult.DocumentType);
            
            return BuildSuccessResponse(structuredData, ocrResult, validationResult.DocumentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OCR request for document type {DocumentType}", documentTypeString);
            return Results.Problem($"Error processing OCR request: {ex.Message}");
        }
    }

    private async Task<(string dataUrl, string mimeType)> ConvertImageToDataUrl(Stream imageStream)
    {
        ConvertingImageToDataUrl(_logger);
        return await _imageConverter.ConvertAsync(imageStream).ConfigureAwait(false);
    }

    private async Task<MistralOcrResult> ExtractTextFromImage((string dataUrl, string mimeType) imageData)
    {
        ExtractingTextFromImage(_logger);
        
        var dataItemsObservable = Observable.Return(imageData);
        var ocrResult = await _ocrService
            .ProcessImageDataItems(dataItemsObservable)
            .FirstOrDefaultAsync();

        if (ocrResult == null)
        {
            throw new InvalidOperationException("OCR service returned null result");
        }

        OcrTextExtractionCompleted(_logger, ocrResult.Text?.Length ?? 0);
        return ocrResult;
    }

    private async Task<OcrExtractionResponse<object>> ExtractStructuredData(string ocrText, DocumentType documentType)
    {
        ExtractingStructuredData(_logger, documentType);
        
        var extractionRequest = new OcrExtractionRequest
        {
            DocumentType = documentType,
            Content = ocrText,
            IsImage = false
        };

        var result = await _extractionService.ProcessExtractionRequestAsync(extractionRequest).ConfigureAwait(false);
        
        if (!result.Success)
        {
            StructuredDataExtractionFailed(_logger, result.Error);
        }
        
        return result;
    }

    // High-performance logging using LoggerMessage delegates
    [LoggerMessage(LogLevel.Debug, "Converting image stream to data URL")]
    private static partial void ConvertingImageToDataUrl(ILogger logger);

    [LoggerMessage(LogLevel.Debug, "Extracting text from image using Mistral OCR")]
    private static partial void ExtractingTextFromImage(ILogger logger);

    [LoggerMessage(LogLevel.Debug, "OCR text extraction completed. Text length: {TextLength}")]
    private static partial void OcrTextExtractionCompleted(ILogger logger, int textLength);

    [LoggerMessage(LogLevel.Debug, "Extracting structured data for document type {DocumentType}")]
    private static partial void ExtractingStructuredData(ILogger logger, DocumentType documentType);

    [LoggerMessage(LogLevel.Warning, "Structured data extraction failed: {Error}")]
    private static partial void StructuredDataExtractionFailed(ILogger logger, string? error);

    private static IResult BuildSuccessResponse(
        OcrExtractionResponse<object> extractionResponse, 
        MistralOcrResult ocrResult, 
        DocumentType documentType)
    {
        var response = new OcrAsyncResponse
        {
            DocumentType = documentType.ToString().ToLowerInvariant(),
            OcrText = ocrResult.Text,
            ExtractedData = extractionResponse.Data,
            Confidence = extractionResponse.Confidence,
            ProcessingTimeMs = extractionResponse.ProcessingTimeMs,
            ModelUsed = ocrResult.ModelUsed,
            TotalTokens = ocrResult.TotalTokens
        };

        return Results.Ok(response);
    }
}