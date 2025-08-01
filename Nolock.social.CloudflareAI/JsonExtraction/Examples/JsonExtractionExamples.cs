using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Interfaces;
using static Nolock.social.CloudflareAI.JsonExtraction.JsonExtractionExtensions;

namespace Nolock.social.CloudflareAI.JsonExtraction.Examples;

/// <summary>
/// Examples of using the JSON extraction pipeline
/// </summary>
public static class JsonExtractionExamples
{
    /// <summary>
    /// Example: Extract product information from text
    /// </summary>
    public static async Task ExtractProductInfo(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var productText = @"
            The new iPhone 15 Pro Max is Apple's flagship smartphone, featuring a 6.7-inch display 
            and starting at $1,199. It comes in Natural Titanium, Blue Titanium, White Titanium, 
            and Black Titanium colors. The device features the new A17 Pro chip and has a 
            battery life of up to 29 hours of video playback.
        ";

        var schema = Schema("Product", "Product information")
            .WithString("name", "Product name")
            .WithString("brand", "Brand or manufacturer")
            .WithNumber("price", "Price in USD")
            .WithString("display_size", "Display size", required: false)
            .WithArray("colors", "Available colors")
            .WithString("processor", "Processor model", required: false)
            .WithString("battery_life", "Battery life description", required: false)
            .Build();

        await extractor.ExtractJson(productText, schema)
            .Do(result =>
            {
                if (result.Success)
                {
                    logger.LogInformation("Extracted product info: {Json}", result.ExtractedJson);
                }
                else
                {
                    logger.LogError("Extraction failed: {Error}", result.Error);
                }
            })
            .FirstAsync();
    }

    /// <summary>
    /// Example: Extract contact information from emails
    /// </summary>
    public static async Task ExtractContactsFromEmails(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var emails = new[]
        {
            @"Hi, this is John Smith from Acme Corp. You can reach me at john.smith@acme.com 
              or call me at (555) 123-4567. Our office is located at 123 Main St, Suite 100, 
              San Francisco, CA 94105.",
            
            @"Dear Customer, I'm Sarah Johnson, Senior Account Manager at TechCo. 
              Feel free to contact me at sarah.j@techco.io or 555-987-6543. 
              I'm based in our New York office at 456 Broadway, NY 10013."
        };

        var schema = Schema("Contact", "Contact information")
            .WithString("full_name", "Person's full name")
            .WithString("email", "Email address")
            .WithString("phone", "Phone number")
            .WithString("company", "Company name", required: false)
            .WithString("title", "Job title", required: false)
            .WithObject("address", "Address information", required: false)
            .Build();

        await extractor.ExtractJsonBatch(emails, schema)
            .Select((result, index) => new { result, index })
            .Do(item =>
            {
                logger.LogInformation("Email {Index}: {Json}", 
                    item.index + 1, 
                    item.result.ExtractedJson);
            })
            .ToList();
    }

    /// <summary>
    /// Example: Extract and parse strongly typed data
    /// </summary>
    public static async Task ExtractStronglyTypedData(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var invoiceText = @"
            Invoice #INV-2024-001
            Date: January 15, 2024
            Due Date: February 15, 2024
            
            Bill To:
            ABC Company
            123 Business Ave
            New York, NY 10001
            
            Items:
            - Professional Services: $5,000.00
            - Software License: $1,200.00
            - Support Package: $800.00
            
            Subtotal: $7,000.00
            Tax (8.875%): $621.25
            Total Due: $7,621.25
        ";

        var schema = Schema("Invoice", "Invoice data")
            .WithString("invoice_number")
            .WithString("date")
            .WithString("due_date")
            .WithObject("bill_to", "Billing information")
            .WithArray("items", "Line items")
            .WithNumber("subtotal")
            .WithNumber("tax_amount")
            .WithNumber("total")
            .Build();

        // Extract and parse as strongly typed object
        var invoice = await extractor
            .ExtractAs<Invoice>(invoiceText, schema)
            .FirstAsync();

        logger.LogInformation("Invoice {Number} - Total: ${Total:F2}", 
            invoice.InvoiceNumber, 
            invoice.Total);
    }

    /// <summary>
    /// Example: Batch processing with error handling and retry
    /// </summary>
    public static async Task BatchProcessingWithRetry(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var reviews = new[]
        {
            "This product is amazing! 5 stars. Would definitely recommend to friends.",
            "Terrible experience. The item broke after 2 days. 1 star.",
            "Pretty good value for money. Some minor issues but overall satisfied. 4 stars.",
            "Not sure what to think. Has pros and cons. Maybe 3 stars?"
        };

        var schema = Schema("Review", "Product review analysis")
            .WithInteger("rating", "Rating from 1-5")
            .WithString("sentiment", "Overall sentiment (positive/negative/neutral)")
            .WithArray("keywords", "Key phrases from the review")
            .WithBoolean("would_recommend", "Would the reviewer recommend this product")
            .Build();

        var results = await extractor
            .ExtractJsonBatch(reviews, schema)
            .HandleErrors(error => logger.LogWarning("Extraction error: {Error}", error))
            .RetryWithHigherTemperature(extractor)
            .Select((result, index) => new { result, index })
            .Do(item =>
            {
                if (item.result.Success)
                {
                    var data = JsonSerializer.Deserialize<ReviewData>(item.result.ExtractedJson!);
                    logger.LogInformation("Review {Index}: Rating={Rating}, Sentiment={Sentiment}", 
                        item.index + 1, 
                        data?.Rating, 
                        data?.Sentiment);
                }
            })
            .ToList();

        var successRate = results.Count(r => r.result.Success) / (double)results.Count;
        logger.LogInformation("Extraction success rate: {Rate:P}", successRate);
    }

    /// <summary>
    /// Example: Real-time streaming extraction
    /// </summary>
    public static async Task StreamingExtraction(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var schema = Schema("Event", "System event")
            .WithString("timestamp")
            .WithString("level", "Log level")
            .WithString("message")
            .WithObject("metadata", required: false)
            .Build();

        // Simulate streaming log data
        var logStream = Observable
            .Interval(TimeSpan.FromSeconds(1))
            .Take(10)
            .Select(i => $"[2024-01-15 10:{i:D2}:00] INFO: User login successful for user{i}@example.com from IP 192.168.1.{i}");

        await logStream
            .SelectMany(log => extractor.ExtractJson(log, schema))
            .Where(r => r.Success)
            .Do(result =>
            {
                logger.LogInformation("Extracted event: {Json}", result.ExtractedJson);
            })
            .LastAsync();
    }
}

// Example strongly typed models
public record Invoice
{
    public string InvoiceNumber { get; init; } = "";
    public string Date { get; init; } = "";
    public string DueDate { get; init; } = "";
    public BillingInfo BillTo { get; init; } = new();
    public List<LineItem> Items { get; init; } = new();
    public decimal Subtotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal Total { get; init; }
}

public record BillingInfo
{
    public string Company { get; init; } = "";
    public string Address { get; init; } = "";
}

public record LineItem
{
    public string Description { get; init; } = "";
    public decimal Amount { get; init; }
}

public record ReviewData
{
    public int Rating { get; init; }
    public string Sentiment { get; init; } = "";
    public List<string> Keywords { get; init; } = new();
    public bool WouldRecommend { get; init; }
}