using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.SchemaGeneration;

namespace Nolock.social.CloudflareAI.JsonExtraction.Examples;

/// <summary>
/// Examples of using reflection-based schema generation for OCR extraction
/// </summary>
public static class OcrExtractionExamples
{
    /// <summary>
    /// Example: Extract check data from OCR text
    /// </summary>
    public static async Task ExtractCheckFromOcr(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();
        
        // Example OCR text from a check image
        var ocrText = @"
            ABC BANK
            123 Main Street, Anytown, USA 12345
            
            Pay to the order of: John Smith                    $1,250.00
            One thousand two hundred fifty dollars and 00/100
            
            Date: 01/15/2024
            Check #: 1234
            
            Memo: Rent payment
            
            Signature: [Signed]
            
            Routing: 123456789  Account: 9876543210  Check: 1234
        ";
        
        // Extract check data using the Check model
        var check = await extractor
            .ExtractFromType<Check>(ocrText)
            .FirstAsync();
        
        logger.LogInformation("Extracted Check Data:");
        logger.LogInformation("  Check #: {CheckNumber}", check.CheckNumber);
        logger.LogInformation("  Date: {Date}", check.Date);
        logger.LogInformation("  Payee: {Payee}", check.Payee);
        logger.LogInformation("  Amount: ${Amount}", check.Amount);
        logger.LogInformation("  Bank: {Bank}", check.BankName);
        logger.LogInformation("  Confidence: {Confidence:P}", check.Confidence);
    }
    
    /// <summary>
    /// Example: Extract receipt data from OCR text
    /// </summary>
    public static async Task ExtractReceiptFromOcr(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();
        
        // Example OCR text from a receipt image
        var ocrText = @"
            SUPER MART
            456 Shopping Plaza
            Big City, ST 98765
            (555) 123-4567
            
            Receipt #: 2024-0001
            Date: 01/20/2024 3:45 PM
            
            GROCERY
            Milk 2% Gallon         2 @ $3.99      $7.98
            Bread Whole Wheat      1 @ $2.49      $2.49
            Eggs Large Dozen       1 @ $4.99      $4.99
            
            PRODUCE  
            Bananas                3.2 lb @ $0.59/lb  $1.89
            Apples Gala            2.5 lb @ $1.29/lb  $3.23
            
            Subtotal:                              $20.58
            Sales Tax (8%):                         $1.65
            
            TOTAL:                                 $22.23
            
            PAYMENT:
            VISA ****1234                          $22.23
            
            Thank you for shopping at Super Mart!
        ";
        
        // Extract receipt data using the Receipt model
        var receipt = await extractor
            .ExtractFromType<Receipt>(ocrText)
            .FirstAsync();
        
        logger.LogInformation("Extracted Receipt Data:");
        logger.LogInformation("  Merchant: {MerchantName}", receipt.Merchant.Name);
        logger.LogInformation("  Date: {Timestamp}", receipt.Timestamp);
        logger.LogInformation("  Total: ${Total}", receipt.Totals.Total);
        logger.LogInformation("  Tax: ${Tax}", receipt.Totals.Tax);
        logger.LogInformation("  Items: {ItemCount}", receipt.Items?.Count ?? 0);
        
        if (receipt.Items != null)
        {
            foreach (var item in receipt.Items)
            {
                logger.LogInformation("    - {Description}: ${Price}", 
                    item.Description, item.TotalPrice);
            }
        }
    }
    
    /// <summary>
    /// Example: Extract simplified check data for quick processing
    /// </summary>
    public static async Task ExtractSimpleCheck(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();
        
        var checkText = "Check #5678 dated 02/01/2024 for $500.00 to Jane Doe from Chase Bank";
        
        // Use the simplified model for basic extraction
        var simpleCheck = await extractor
            .ExtractFromType<SimpleCheck>(checkText)
            .FirstAsync();
        
        logger.LogInformation("Quick Check Extract:");
        logger.LogInformation("  Number: {Number}", simpleCheck.CheckNumber);
        logger.LogInformation("  Amount: ${Amount}", simpleCheck.Amount);
        logger.LogInformation("  Payee: {Payee}", simpleCheck.Payee);
    }
    
    /// <summary>
    /// Example: Batch process multiple receipts
    /// </summary>
    public static async Task BatchProcessReceipts(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();
        
        var receiptTexts = new[]
        {
            "Starbucks receipt 01/15/2024 Total: $5.45 for Latte",
            "McDonald's 01/16/2024 Order #123 Total $8.99 Big Mac Meal",
            "Gas Station 01/17/2024 Fuel 10 gallons $35.90 Total"
        };
        
        // Process multiple receipts using simplified model
        var receipts = await extractor
            .ExtractFromTypeBatch<SimpleReceipt>(receiptTexts)
            .ToList();
        
        logger.LogInformation("Processed {Count} receipts:", receipts.Count);
        foreach (var receipt in receipts)
        {
            logger.LogInformation("  {Merchant} - ${Total} on {Date}", 
                receipt.MerchantName, receipt.TotalAmount, receipt.Date);
        }
    }
    
    /// <summary>
    /// Example: Extract with custom schema modifications
    /// </summary>
    public static async Task ExtractWithCustomSchema(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();
        
        // Generate schema and customize it
        var schema = ReflectionSchemaExtensions.SchemaFromType<Check>()
            .WithOptional("routingNumber", "string", "Bank routing number - not required")
            .WithOptional("accountNumber", "string", "Account number - not required")
            .Build();
        
        var checkText = @"
            Pay $750 to Electric Company
            Date: February 1, 2024
            Check number: 999
            For: Monthly bill
        ";
        
        var result = await extractor
            .ExtractJson(checkText, schema)
            .FirstAsync();
        
        if (result.Success)
        {
            logger.LogInformation("Extracted with custom schema: {Json}", result.ExtractedJson);
        }
    }
    
    /// <summary>
    /// Example: Validate extraction results
    /// </summary>
    public static async Task ValidateExtractionResults(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();
        
        var suspiciousText = @"
            This is not a check or receipt.
            Just some random text about the weather.
            It's sunny today with a high of 75 degrees.
        ";
        
        // Extract and check if valid
        var check = await extractor
            .ExtractFromType<Check>(suspiciousText)
            .FirstAsync();
        
        if (check.IsValidInput == false || check.Confidence < 0.5)
        {
            logger.LogWarning("Input does not appear to be a valid check. Confidence: {Confidence:P}", 
                check.Confidence);
        }
        else
        {
            logger.LogInformation("Valid check detected with confidence: {Confidence:P}", 
                check.Confidence);
        }
    }
    
    /// <summary>
    /// Example: Handle extraction errors gracefully
    /// </summary>
    public static async Task HandleExtractionErrors(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();
        
        var poorQualityText = "Blurry text... $... date... illegible...";
        
        // Set up extraction with error handling
        await extractor
            .ExtractFromType<Receipt>(poorQualityText)
            .Catch<Receipt, Exception>(ex =>
            {
                logger.LogError(ex, "Failed to extract receipt data");
                // Return a default receipt with low confidence
                return Observable.Return(new Receipt
                {
                    IsValidInput = false,
                    Confidence = 0.1,
                    Merchant = new MerchantInfo { Name = "Unknown" },
                    Totals = new ReceiptTotals { Total = 0.00m }
                });
            })
            .Do(receipt =>
            {
                if (receipt.Confidence < 0.3)
                {
                    logger.LogWarning("Low confidence extraction. Manual review recommended.");
                }
            })
            .FirstAsync();
    }
}