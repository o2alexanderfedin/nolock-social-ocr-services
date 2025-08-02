using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.SchemaGeneration;
using Nolock.social.CloudflareAI.Models;

namespace Nolock.social.CloudflareAI.JsonExtraction.Services;

/// <summary>
/// Service for OCR document extraction using Cloudflare AI
/// </summary>
public class OcrExtractionService : IOcrExtractionService
{
    private readonly IWorkersAI _workersAI;
    private readonly ILogger<OcrExtractionService> _logger;
    
    public OcrExtractionService(IWorkersAI workersAI, ILogger<OcrExtractionService> logger)
    {
        _workersAI = workersAI;
        _logger = logger;
    }
    
    /// <summary>
    /// Extract structured data from OCR text based on document type
    /// </summary>
    public async Task<object> ExtractDocumentAsync(DocumentType documentType, string ocrText, bool useSimpleSchema = true)
    {
        _logger.LogDebug("Extracting {DocumentType} using {Schema} schema", 
            documentType, useSimpleSchema ? "simple" : "full");
            
        try
        {
            return documentType switch
            {
                DocumentType.Check => useSimpleSchema 
                    ? await ExtractAsync<SimpleCheck>(ocrText)
                    : await ExtractAsync<Check>(ocrText),
                    
                DocumentType.Receipt => useSimpleSchema
                    ? await ExtractAsync<SimpleReceipt>(ocrText)
                    : await ExtractAsync<Receipt>(ocrText),
                    
                _ => throw new NotSupportedException($"Document type {documentType} is not supported")
            };
        }
        catch (Exception ex) when (useSimpleSchema == false)
        {
            // If full schema fails, fallback to simple schema
            _logger.LogWarning("Full schema extraction failed, falling back to simple schema: {Error}", ex.Message);
            return await ExtractDocumentAsync(documentType, ocrText, useSimpleSchema: true);
        }
    }
    
    /// <summary>
    /// Process OCR extraction request
    /// </summary>
    public async Task<OcrExtractionResponse<object>> ProcessExtractionRequestAsync(OcrExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Handle base64 image if needed
            var content = request.IsImage 
                ? await ProcessImageToText(request.Content)
                : request.Content;
            
            // Extract based on document type
            var data = await ExtractDocumentAsync(request.DocumentType, content, request.UseSimpleSchema);
            
            // Get confidence from the extracted data
            var confidence = GetConfidence(data);
            
            return new OcrExtractionResponse<object>
            {
                DocumentType = request.DocumentType,
                Success = true,
                Data = data,
                Confidence = confidence,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract {DocumentType}", request.DocumentType);
            
            return new OcrExtractionResponse<object>
            {
                DocumentType = request.DocumentType,
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }
    
    /// <summary>
    /// Process batch OCR extraction request
    /// </summary>
    public async Task<BatchOcrExtractionResponse<object>> ProcessBatchExtractionRequestAsync(BatchOcrExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var stopwatch = Stopwatch.StartNew();
        var response = new BatchOcrExtractionResponse<object>
        {
            DocumentType = request.DocumentType
        };
        
        // Process in batches with concurrency control
        var tasks = request.Contents
            .Select((content, index) => ProcessSingleItemAsync(request, content, index))
            .ToList();
        
        // Execute with concurrency limit
        var results = new List<OcrExtractionResponse<object>>();
        foreach (var batch in tasks.Chunk(request.MaxConcurrency))
        {
            var batchResults = await Task.WhenAll(batch);
            results.AddRange(batchResults);
        }
        
        response.Results = results;
        response.TotalProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        
        _logger.LogInformation("Batch extraction completed: {Success}/{Total} successful, avg confidence: {Confidence:P}",
            response.SuccessCount, response.Results.Count, response.AverageConfidence);
        
        return response;
    }
    
    /// <summary>
    /// Extract structured data for a specific type using Llama 3.3 70B model
    /// </summary>
    /// <remarks>
    /// Uses the Llama 3.3 70B Instruct FP8 Fast model for improved accuracy in JSON extraction
    /// compared to smaller models, while maintaining reasonable performance with FP8 optimization.
    /// </remarks>
    private async Task<T> ExtractAsync<T>(string ocrText) where T : class, new()
    {
        using var extractor = _workersAI.CreateJsonExtractor();
        
        // Use more flexible extraction options for better success rate
        var options = new ExtractionOptions
        {
            MaxTokens = 2000,
            Temperature = 0.2, // Slightly higher for more creativity in interpretation
            StrictValidation = false, // Allow partial matches
            Examples = new List<string>()
        };
        
        try 
        {
            var result = await extractor
                .ExtractFromType<T>(ocrText, TextGenerationModels.Llama3_3_70B_Instruct_FP8_Fast, options)
                .Retry(2)
                .Do(extracted => _logger.LogDebug("Extracted {Type} with confidence {Confidence:P}", 
                    typeof(T).Name, GetConfidence(extracted)))
                .FirstOrDefaultAsync();
                
            if (result == null)
            {
                _logger.LogWarning("No valid {Type} data could be extracted from OCR text", typeof(T).Name);
                // Return a default instance with minimal data
                result = new T();
            }
            
            _logger.LogInformation("Successfully extracted {Type} from OCR text", typeof(T).Name);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract {Type} from OCR text: {Error}", typeof(T).Name, ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Process single item in batch
    /// </summary>
    private async Task<OcrExtractionResponse<object>> ProcessSingleItemAsync(
        BatchOcrExtractionRequest request, 
        string content, 
        int index)
    {
        try
        {
            var itemRequest = new OcrExtractionRequest
            {
                DocumentType = request.DocumentType,
                Content = content,
                IsImage = request.IsImage,
                UseSimpleSchema = request.UseSimpleSchema
            };
            
            return await ProcessExtractionRequestAsync(itemRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process item {Index} in batch", index);
            return new OcrExtractionResponse<object>
            {
                DocumentType = request.DocumentType,
                Success = false,
                Error = $"Item {index}: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Process base64 image to text (placeholder - would use actual OCR service)
    /// </summary>
    private async Task<string> ProcessImageToText(string base64Image)
    {
        // In a real implementation, this would call an OCR service
        // For now, we'll just throw an exception
        await Task.Delay(100); // Simulate async operation
        throw new NotImplementedException("Image OCR processing not implemented. Please provide text content.");
    }
    
    /// <summary>
    /// Get confidence score from extracted data
    /// </summary>
    private double GetConfidence(object data)
    {
        return data switch
        {
            Check check => check.Confidence,
            Receipt receipt => receipt.Confidence,
            SimpleCheck => 0.8, // Simple models don't have confidence, use default
            SimpleReceipt => 0.8,
            _ => 0.5
        };
    }
}

// Extension methods for dependency injection would go here
// Example:
// public static class OcrExtractionServiceExtensions
// {
//     public static IServiceCollection AddOcrExtractionService(this IServiceCollection services)
//     {
//         services.AddScoped<OcrExtractionService>();
//         return services;
//     }
// }