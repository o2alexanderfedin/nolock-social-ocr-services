using System.Reactive.Linq;
using Microsoft.IO;
using Nolock.social.CloudflareAI.JsonExtraction.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.MistralOcr;
using Nolock.social.OCRservices.Core.Models;
using Nolock.social.OCRservices.Core.Utils;

namespace Nolock.social.OCRservices.Services;

/// <summary>
/// Simplified OCR request handler with minimal abstractions
/// </summary>
public sealed partial class OcrRequestHandler : IOcrRequestHandler
{
    private readonly IReactiveMistralOcrService _ocrService;
    private readonly IOcrExtractionService _extractionService;
    private readonly ILogger<OcrRequestHandler> _logger;
    private static readonly RecyclableMemoryStreamManager StreamManager = new();
    private static readonly MimeTypeTrie MimeTrie = BuildMimeTrie();

    public OcrRequestHandler(
        IReactiveMistralOcrService ocrService,
        IOcrExtractionService extractionService,
        ILogger<OcrRequestHandler> logger)
    {
        _ocrService = ocrService;
        _extractionService = extractionService;
        _logger = logger;
    }

    public async Task<IResult> HandleAsync(Stream imageStream, string? documentTypeString)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        
        try
        {
            // Simple inline validation - no need for separate validator class
            if (string.IsNullOrEmpty(documentTypeString))
            {
                return Results.BadRequest("Document type is required. Valid values: check, receipt");
            }

            if (!Enum.TryParse<DocumentType>(documentTypeString, ignoreCase: true, out var documentType))
            {
                return Results.BadRequest($"Invalid document type: {documentTypeString}. Valid values: check, receipt");
            }

            // Convert image inline - no need for separate converter class
            var imageData = await ConvertImageToDataUrl(imageStream).ConfigureAwait(false);
            var ocrResult = await ExtractTextFromImage(imageData).ConfigureAwait(false);
            
            if (string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                return Results.Problem("Failed to extract text from image");
            }

            var structuredData = await ExtractStructuredData(ocrResult.Text, documentType).ConfigureAwait(false);
            
            return BuildSuccessResponse(structuredData, ocrResult, documentType);
        }
        catch (Exception ex)
        {
            LogOcrRequestError(_logger, ex, documentTypeString);
            return Results.Problem($"Error processing OCR request: {ex.Message}");
        }
    }

    private static async Task<(string dataUrl, string mimeType)> ConvertImageToDataUrl(Stream imageStream)
    {
#pragma warning disable CA2007 // ConfigureAwait not available for await using
        await using var memoryStream = StreamManager.GetStream();
#pragma warning restore CA2007
        await imageStream.CopyToAsync(memoryStream).ConfigureAwait(false);
        var imageBytes = memoryStream.ToArray();

        var mimeType = DetectMimeType(imageBytes);
        var base64String = Convert.ToBase64String(imageBytes);
        var dataUrl = $"data:{mimeType};base64,{base64String}";

        return (dataUrl, mimeType);
    }

    private async Task<MistralOcrResult> ExtractTextFromImage((string dataUrl, string mimeType) imageData)
    {        
        var dataItemsObservable = Observable.Return(imageData);
#pragma warning disable CA2007 // ConfigureAwait not available for IObservable
        var ocrResult = await _ocrService
            .ProcessImageDataItems(dataItemsObservable)
            .FirstOrDefaultAsync();
#pragma warning restore CA2007

        return ocrResult ?? throw new InvalidOperationException("OCR service returned null result");
    }

    private async Task<OcrExtractionResponse<object>> ExtractStructuredData(string ocrText, DocumentType documentType)
    {
        var extractionRequest = new OcrExtractionRequest
        {
            DocumentType = documentType,
            Content = ocrText,
            IsImage = false
        };

        var result = await _extractionService.ProcessExtractionRequestAsync(extractionRequest).ConfigureAwait(false);
        
        if (!result.Success)
        {
            LogStructuredDataExtractionFailed(_logger, result.Error);
        }
        
        return result;
    }

    private static string DetectMimeType(byte[] bytes)
    {
        return MimeTrie.Search(bytes) ?? "application/octet-stream";
    }

    private static MimeTypeTrie BuildMimeTrie()
    {
        var trie = new MimeTypeTrie();
        
        // Image formats
        trie.Add([0xFF, 0xD8, 0xFF], "image/jpeg");
        trie.Add([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png");
        trie.Add([0x47, 0x49, 0x46, 0x38, 0x37, 0x61], "image/gif");
        trie.Add([0x47, 0x49, 0x46, 0x38, 0x39, 0x61], "image/gif");
        trie.Add([0x52, 0x49, 0x46, 0x46], "image/webp");
        trie.Add([0x42, 0x4D], "image/bmp");
        trie.Add([0x00, 0x00, 0x01, 0x00], "image/x-icon");
        trie.Add([0x49, 0x49, 0x2A, 0x00], "image/tiff");
        trie.Add([0x4D, 0x4D, 0x00, 0x2A], "image/tiff");
        
        // PDF
        trie.Add([0x25, 0x50, 0x44, 0x46], "application/pdf");
        
        return trie;
    }

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

    public async Task<IResult> HandleReceiptAsync(Stream imageStream)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        
        try
        {
            var imageData = await ConvertImageToDataUrl(imageStream).ConfigureAwait(false);
            var ocrResult = await ExtractTextFromImage(imageData).ConfigureAwait(false);
            
            if (string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                return Results.Ok(new ReceiptOcrResponse
                {
                    Success = false,
                    Error = "Failed to extract text from image",
                    OcrText = ocrResult.Text ?? "",
                    ModelUsed = ocrResult.ModelUsed,
                    TotalTokens = ocrResult.TotalTokens
                });
            }

            var extractionRequest = new OcrExtractionRequest
            {
                DocumentType = DocumentType.Receipt,
                Content = ocrResult.Text,
                IsImage = false,
                UseSimpleSchema = true // Use simple schema for better compatibility
            };

            var extractionResult = await _extractionService.ProcessExtractionRequestAsync(extractionRequest).ConfigureAwait(false);
            
            var response = new ReceiptOcrResponse
            {
                Success = extractionResult.Success,
                OcrText = ocrResult.Text,
                ReceiptData = extractionResult.Success ? extractionResult.Data as Receipt : null,
                Confidence = extractionResult.Confidence,
                ProcessingTimeMs = extractionResult.ProcessingTimeMs,
                ModelUsed = ocrResult.ModelUsed,
                TotalTokens = ocrResult.TotalTokens,
                Error = extractionResult.Success ? null : extractionResult.Error
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            LogOcrRequestError(_logger, ex, "receipt");
            return Results.Ok(new ReceiptOcrResponse
            {
                Success = false,
                Error = $"Error processing receipt: {ex.Message}",
                OcrText = ""
            });
        }
    }

    public async Task<IResult> HandleCheckAsync(Stream imageStream)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        
        try
        {
            var imageData = await ConvertImageToDataUrl(imageStream).ConfigureAwait(false);
            var ocrResult = await ExtractTextFromImage(imageData).ConfigureAwait(false);
            
            if (string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                return Results.Ok(new CheckOcrResponse
                {
                    Success = false,
                    Error = "Failed to extract text from image",
                    OcrText = ocrResult.Text ?? "",
                    ModelUsed = ocrResult.ModelUsed,
                    TotalTokens = ocrResult.TotalTokens
                });
            }

            var extractionRequest = new OcrExtractionRequest
            {
                DocumentType = DocumentType.Check,
                Content = ocrResult.Text,
                IsImage = false,
                UseSimpleSchema = true // Use simple schema for better compatibility
            };

            var extractionResult = await _extractionService.ProcessExtractionRequestAsync(extractionRequest).ConfigureAwait(false);
            
            var response = new CheckOcrResponse
            {
                Success = extractionResult.Success,
                OcrText = ocrResult.Text,
                CheckData = extractionResult.Success ? extractionResult.Data as Check : null,
                Confidence = extractionResult.Confidence,
                ProcessingTimeMs = extractionResult.ProcessingTimeMs,
                ModelUsed = ocrResult.ModelUsed,
                TotalTokens = ocrResult.TotalTokens,
                Error = extractionResult.Success ? null : extractionResult.Error
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            LogOcrRequestError(_logger, ex, "check");
            return Results.Ok(new CheckOcrResponse
            {
                Success = false,
                Error = $"Error processing check: {ex.Message}",
                OcrText = ""
            });
        }
    }

    // High-performance logging using LoggerMessage delegates
    [LoggerMessage(LogLevel.Warning, "Structured data extraction failed: {Error}")]
    private static partial void LogStructuredDataExtractionFailed(ILogger logger, string? error);

    [LoggerMessage(LogLevel.Error, "Error processing OCR request for document type {DocumentType}")]
    private static partial void LogOcrRequestError(ILogger logger, Exception ex, string? documentType);
}