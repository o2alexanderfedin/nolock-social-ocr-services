using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.SchemaGeneration;

namespace Nolock.social.CloudflareAI.JsonExtraction.Examples;

/// <summary>
/// Demonstration of OCR processing pipeline using Cloudflare AI
/// </summary>
public class OcrProcessingDemo
{
    private readonly IWorkersAI _workersAI;
    private readonly ILogger<OcrProcessingDemo> _logger;
    
    public OcrProcessingDemo(IWorkersAI workersAI, ILogger<OcrProcessingDemo> logger)
    {
        _workersAI = workersAI;
        _logger = logger;
    }
    
    /// <summary>
    /// Process a check image through OCR and structured extraction
    /// </summary>
    public async Task<Check?> ProcessCheckImage(string imageText)
    {
        using var extractor = _workersAI.CreateJsonExtractor();
        
        try
        {
            // Step 1: Extract structured data from OCR text
            var check = await extractor
                .ExtractFromType<Check>(imageText)
                .Do(c => _logger.LogDebug("Extraction confidence: {Confidence:P}", c.Confidence))
                .FirstAsync();
            
            // Step 2: Validate the extraction
            if (!ValidateCheck(check))
            {
                _logger.LogWarning("Check validation failed");
                return null;
            }
            
            // Step 3: Log successful extraction
            _logger.LogInformation("Successfully extracted check #{Number} for ${Amount} to {Payee}", 
                check.CheckNumber, check.Amount, check.Payee);
            
            return check;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process check image");
            return null;
        }
    }
    
    /// <summary>
    /// Process a receipt image through OCR and structured extraction
    /// </summary>
    public async Task<Receipt?> ProcessReceiptImage(string imageText)
    {
        using var extractor = _workersAI.CreateJsonExtractor();
        
        try
        {
            // Extract with retry logic for better reliability
            var receipt = await extractor
                .ExtractFromType<Receipt>(imageText)
                .Retry(3)
                .Do(r => _logger.LogDebug("Extracted receipt from {Merchant} with {Items} items", 
                    r.Merchant.Name, r.Items?.Count ?? 0))
                .FirstAsync();
            
            // Validate and enrich
            if (ValidateReceipt(receipt))
            {
                EnrichReceiptData(receipt);
                return receipt;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process receipt image");
            return null;
        }
    }
    
    /// <summary>
    /// Batch process multiple documents
    /// </summary>
    public async Task BatchProcessDocuments(string[] ocrTexts)
    {
        using var extractor = _workersAI.CreateJsonExtractor();
        
        // Process in parallel with concurrency limit
        var results = await Observable.Merge(
            ocrTexts.Select(text => 
                ProcessDocumentAsync(extractor, text)
                    .ToObservable()
                    .Catch<DocumentResult, Exception>(ex => 
                    {
                        _logger.LogError(ex, "Error processing document");
                        return Observable.Return(new DocumentResult { Success = false });
                    })
            ), 
            maxConcurrent: 3)
            .ToList();
        
        // Summary
        var successCount = results.Count(r => r.Success);
        _logger.LogInformation("Processed {Total} documents. Success: {Success}, Failed: {Failed}",
            results.Count, successCount, results.Count - successCount);
    }
    
    private async Task<DocumentResult> ProcessDocumentAsync(JsonExtractionService extractor, string text)
    {
        // Try to determine document type
        var documentType = await DetermineDocumentType(extractor, text);
        
        switch (documentType)
        {
            case "check":
                var check = await extractor.ExtractFromType<Check>(text).FirstAsync();
                return new DocumentResult 
                { 
                    Success = check.Confidence > 0.7,
                    Type = "check",
                    Data = check
                };
                
            case "receipt":
                var receipt = await extractor.ExtractFromType<Receipt>(text).FirstAsync();
                return new DocumentResult 
                { 
                    Success = receipt.Confidence > 0.7,
                    Type = "receipt",
                    Data = receipt
                };
                
            default:
                return new DocumentResult { Success = false, Type = "unknown" };
        }
    }
    
    private async Task<string> DetermineDocumentType(JsonExtractionService extractor, string text)
    {
        // Simple schema for document classification
        var schema = JsonExtractionExtensions.Schema("DocumentType")
            .WithRequired("type", "string", "Document type: 'check', 'receipt', or 'unknown'")
            .WithRequired("confidence", "number", "Confidence score 0-1")
            .Build();
        
        var prompt = $@"Analyze this text and determine if it's a check, receipt, or unknown document type.
        
Text: {text}";
        
        var result = await extractor.ExtractJson(prompt, schema).FirstAsync();
        
        if (result.Success && result.ExtractedJson != null)
        {
            var doc = System.Text.Json.JsonDocument.Parse(result.ExtractedJson);
            return doc.RootElement.GetProperty("type").GetString() ?? "unknown";
        }
        
        return "unknown";
    }
    
    private bool ValidateCheck(Check check)
    {
        // Basic validation rules
        if (check.Confidence < 0.5)
            return false;
            
        if (check.IsValidInput == false)
            return false;
            
        if (string.IsNullOrEmpty(check.Amount))
            return false;
            
        // Validate amount format
        if (!System.Text.RegularExpressions.Regex.IsMatch(check.Amount, @"^\d+(\.\d{1,2})?$"))
        {
            _logger.LogWarning("Invalid check amount format: {Amount}", check.Amount);
            return false;
        }
        
        return true;
    }
    
    private bool ValidateReceipt(Receipt receipt)
    {
        if (receipt.Confidence < 0.5)
            return false;
            
        if (string.IsNullOrEmpty(receipt.Merchant?.Name))
            return false;
            
        if (string.IsNullOrEmpty(receipt.Totals?.Total))
            return false;
            
        return true;
    }
    
    private void EnrichReceiptData(Receipt receipt)
    {
        // Add calculated fields
        if (receipt.Items?.Count > 0 && receipt.Metadata != null)
        {
            receipt.Metadata.SourceImageId = Guid.NewGuid().ToString();
        }
        
        // Set default currency if not detected
        receipt.Currency ??= "USD";
        
        // Add processing timestamp
        receipt.Metadata ??= new ReceiptMetadata();
        receipt.Metadata.TimeZone = TimeZoneInfo.Local.StandardName;
    }
    
    private class DocumentResult
    {
        public bool Success { get; set; }
        public string Type { get; set; } = "";
        public object? Data { get; set; }
    }
}

/// <summary>
/// Extension method to set up the demo
/// </summary>
public static class OcrProcessingDemoExtensions
{
    public static IServiceCollection AddOcrProcessing(this IServiceCollection services)
    {
        services.AddScoped<OcrProcessingDemo>();
        return services;
    }
    
    /// <summary>
    /// Example usage in a console application
    /// </summary>
    public static async Task RunOcrDemo(IServiceProvider serviceProvider)
    {
        var demo = serviceProvider.GetRequiredService<OcrProcessingDemo>();
        var logger = serviceProvider.GetRequiredService<ILogger<OcrProcessingDemo>>();
        
        // Example check processing
        var checkOcrText = @"
            First National Bank
            Check #5432
            Date: 03/15/2024
            
            Pay to: Electric Company         $245.67
            Two hundred forty-five and 67/100 dollars
            
            Memo: March bill
            [Signature]
        ";
        
        var check = await demo.ProcessCheckImage(checkOcrText);
        if (check != null)
        {
            logger.LogInformation("Check processed successfully: #{Number}", check.CheckNumber);
        }
        
        // Example receipt processing
        var receiptOcrText = @"
            COFFEE SHOP
            123 Main St
            
            Receipt #0012
            03/15/2024 08:30 AM
            
            Cappuccino Large    $4.50
            Croissant          $3.25
            
            Subtotal:          $7.75
            Tax:               $0.62
            Total:             $8.37
            
            Paid: VISA ****1234
        ";
        
        var receipt = await demo.ProcessReceiptImage(receiptOcrText);
        if (receipt != null)
        {
            logger.LogInformation("Receipt processed: {Merchant} - ${Total}", 
                receipt.Merchant.Name, receipt.Totals.Total);
        }
    }
}