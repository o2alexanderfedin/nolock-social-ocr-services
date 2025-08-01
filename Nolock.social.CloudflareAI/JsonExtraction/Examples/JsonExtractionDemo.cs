using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Configuration;
using static Nolock.social.CloudflareAI.JsonExtraction.JsonExtractionExtensions;

namespace Nolock.social.CloudflareAI.JsonExtraction.Examples;

/// <summary>
/// Demo program for JSON extraction pipeline
/// </summary>
public static class JsonExtractionDemo
{
    public static async Task RunDemo(string accountId, string apiToken)
    {
        // Set up logging
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger("JsonExtractionDemo");

        // Create Workers AI client
        var settings = new WorkersAISettings
        {
            AccountId = accountId,
            ApiToken = apiToken
        };
        
        using var workersAI = WorkersAIFactory.CreateWorkersAI(settings);
        using var extractor = workersAI.CreateJsonExtractor(loggerFactory.CreateLogger<JsonExtractionService>());

        logger.LogInformation("=== Cloudflare Workers AI JSON Extraction Demo ===\n");

        // Demo 1: Extract structured data from unstructured text
        await Demo1_BasicExtraction(extractor, logger);
        
        // Demo 2: Batch processing with reactive pipeline
        await Demo2_BatchProcessing(extractor, logger);
        
        // Demo 3: Real-time streaming extraction
        await Demo3_StreamingExtraction(extractor, logger);
        
        // Demo 4: Complex nested schema extraction
        await Demo4_ComplexSchema(extractor, logger);
    }

    private static async Task Demo1_BasicExtraction(JsonExtractionService extractor, ILogger logger)
    {
        logger.LogInformation("\n📋 Demo 1: Basic JSON Extraction");
        
        var emailText = @"
            Hi team,
            
            Just wanted to update you on the Q4 sales figures. We closed 156 deals 
            worth a total of $2.3 million, exceeding our target by 15%. 
            
            The top performing regions were:
            - North America: $980,000 (78 deals)
            - Europe: $720,000 (52 deals)  
            - Asia Pacific: $600,000 (26 deals)
            
            Great job everyone!
            Sarah Johnson
            VP of Sales
        ";

        var schema = Schema("SalesReport", "Quarterly sales report data")
            .WithString("quarter", "Quarter (e.g., Q4)")
            .WithInteger("total_deals", "Number of deals closed")
            .WithNumber("total_revenue", "Total revenue in millions")
            .WithNumber("target_exceeded_percent", "Percentage over target")
            .WithArray("regions", "Regional performance data")
            .WithString("sender_name", "Who sent the report")
            .WithString("sender_title", "Sender's job title")
            .Build();

        var result = await extractor.ExtractJson(emailText, schema).FirstAsync();
        
        if (result.Success)
        {
            logger.LogInformation("✅ Successfully extracted sales data:");
            logger.LogInformation("{Json}", JsonFormatter.Format(result.ExtractedJson!));
        }
        else
        {
            logger.LogError("❌ Extraction failed: {Error}", result.Error);
        }
    }

    private static async Task Demo2_BatchProcessing(JsonExtractionService extractor, ILogger logger)
    {
        logger.LogInformation("\n📊 Demo 2: Batch Processing Customer Feedback");
        
        var feedbacks = new[]
        {
            "The product quality is excellent but shipping took too long (8 days). Overall satisfied. 4/5 stars.",
            "Amazing customer service! They resolved my issue in minutes. Will definitely buy again. 5 stars!",
            "Product didn't match the description. Very disappointed. Requested a refund. 1 star.",
            "Good value for money. Works as expected. Minor packaging damage but product was fine. 3.5 stars."
        };

        var schema = Schema("CustomerFeedback")
            .WithNumber("rating", "Star rating out of 5")
            .WithString("sentiment", "Overall sentiment: positive, negative, or neutral")
            .WithArray("positive_aspects", "What the customer liked", required: false)
            .WithArray("negative_aspects", "What the customer disliked", required: false)
            .WithBoolean("will_repurchase", "Whether customer will buy again", required: false)
            .WithBoolean("requested_refund", "Whether customer requested refund")
            .Build();

        var results = await extractor
            .ExtractJsonBatch(feedbacks, schema)
            .Select((result, index) => new { result, index })
            .ToList();

        logger.LogInformation("\nProcessed {Count} feedback items:", results.Count);
        
        foreach (var item in results.Where(x => x.result.Success))
        {
            logger.LogInformation("\nFeedback #{Number}:", item.index + 1);
            logger.LogInformation("{Json}", JsonFormatter.Format(item.result.ExtractedJson!));
        }

        var successRate = results.Count(r => r.result.Success) / (double)results.Count;
        logger.LogInformation("\n📈 Success rate: {Rate:P0}", successRate);
    }

    private static async Task Demo3_StreamingExtraction(JsonExtractionService extractor, ILogger logger)
    {
        logger.LogInformation("\n🔄 Demo 3: Real-time Log Processing");
        
        var schema = Schema("LogEntry")
            .WithString("timestamp", "When the event occurred")
            .WithString("level", "Log level: INFO, WARN, ERROR")
            .WithString("service", "Which service generated the log")
            .WithString("message", "Log message")
            .WithObject("context", "Additional context data", required: false)
            .Build();

        // Simulate streaming logs
        var logs = new[]
        {
            "[2024-01-15 10:15:23] INFO AuthService: User john@example.com logged in from IP 192.168.1.100",
            "[2024-01-15 10:15:45] ERROR PaymentService: Transaction TX123456 failed - insufficient funds",
            "[2024-01-15 10:16:02] WARN InventoryService: Stock level low for product SKU-789 (5 units remaining)",
            "[2024-01-15 10:16:15] INFO OrderService: Order #ORD-2024-1234 processed successfully for $156.99"
        };

        logger.LogInformation("\nProcessing log stream...");
        
        await Observable
            .Interval(TimeSpan.FromSeconds(1))
            .Take(logs.Length)
            .Select(i => logs[i])
            .Do(log => logger.LogInformation("📝 Raw: {Log}", log))
            .SelectMany(log => extractor.ExtractJson(log, schema))
            .Where(r => r.Success)
            .Do(result => logger.LogInformation("✨ Structured: {Json}", 
                JsonFormatter.Format(result.ExtractedJson!)))
            .LastOrDefaultAsync();
    }

    private static async Task Demo4_ComplexSchema(JsonExtractionService extractor, ILogger logger)
    {
        logger.LogInformation("\n🏢 Demo 4: Complex Nested Schema - Company Profile");
        
        var companyText = @"
            TechVision Inc. is a leading AI software company founded in 2018 by 
            Dr. Emily Chen (CEO) and Marcus Rodriguez (CTO). The company is 
            headquartered in San Francisco with offices in London and Tokyo.
            
            With 450 employees and annual revenue of $125 million (2023), 
            TechVision specializes in computer vision and natural language processing.
            Their main products include VisionAPI (pricing starts at $99/month) and 
            TextAnalyzer Pro (enterprise pricing).
            
            The company has raised $75 million in Series B funding led by 
            Venture Partners, with participation from Tech Capital and AI Ventures.
            Notable clients include Fortune 500 companies in healthcare, 
            finance, and retail sectors.
        ";

        var schema = Schema("CompanyProfile")
            .WithString("company_name")
            .WithInteger("founded_year")
            .WithObject("leadership", "Company leadership")
            .WithObject("headquarters", "HQ location")
            .WithArray("offices", "Other office locations")
            .WithInteger("employee_count")
            .WithObject("financials", "Financial information")
            .WithArray("products", "Product offerings")
            .WithObject("funding", "Funding information")
            .WithArray("industries_served", "Target industries")
            .Build();

        var result = await extractor
            .ExtractJson(companyText, schema, 
                options: new ExtractionOptions 
                { 
                    MaxTokens = 1500,
                    Temperature = 0.2 
                })
            .FirstAsync();

        if (result.Success)
        {
            logger.LogInformation("\n✅ Extracted company profile:");
            logger.LogInformation("{Json}", JsonFormatter.Format(result.ExtractedJson!));
            
            // Parse and display some key metrics
            if (result.ParsedData.HasValue)
            {
                var data = result.ParsedData.Value;
                if (data.TryGetProperty("company_name", out var name))
                {
                    logger.LogInformation("\n📊 Key Metrics for {Company}:", name.GetString());
                }
                if (data.TryGetProperty("employee_count", out var employees))
                {
                    logger.LogInformation("  • Employees: {Count}", employees.GetInt32());
                }
                if (data.TryGetProperty("financials", out var financials) &&
                    financials.TryGetProperty("annual_revenue", out var revenue))
                {
                    logger.LogInformation("  • Revenue: ${Revenue}M", revenue);
                }
            }
        }
        else
        {
            logger.LogError("❌ Extraction failed: {Error}", result.Error);
        }
    }
}

/// <summary>
/// Simple JSON formatter for better console output
/// </summary>
internal static class JsonFormatter
{
    public static string Format(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch
        {
            return json;
        }
    }
}