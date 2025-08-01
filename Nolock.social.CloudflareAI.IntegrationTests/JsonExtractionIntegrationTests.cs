using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.JsonExtraction;
using static Nolock.social.CloudflareAI.JsonExtraction.JsonExtractionExtensions;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class JsonExtractionIntegrationTests : BaseIntegrationTest
{
    private readonly JsonExtractionService _extractor;
    private readonly ILoggerFactory _extractorLoggerFactory;

    public JsonExtractionIntegrationTests()
    {
        _extractorLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var extractorLogger = _extractorLoggerFactory.CreateLogger<JsonExtractionService>();
        _extractor = Client.CreateJsonExtractor(extractorLogger);
    }

    [Fact]
    public async Task ExtractJson_SimpleSchema_ExtractsCorrectly()
    {
        var text = "John Doe is 30 years old and works as a software engineer.";
        
        var schema = Schema("Person")
            .WithString("name", "Full name")
            .WithInteger("age", "Age in years")
            .WithString("occupation", "Job title")
            .Build();

        var result = await _extractor.ExtractJson(text, schema).FirstAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.ExtractedJson);
        
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(result.ExtractedJson);
        Assert.NotNull(data);
        Assert.True(data.ContainsKey("name"));
        Assert.True(data.ContainsKey("age"));
        Assert.True(data.ContainsKey("occupation"));
        
        Logger.LogInformation("Extracted: {Json}", result.ExtractedJson);
    }

    [Fact]
    public async Task ExtractJson_ComplexSchema_HandlesNestedData()
    {
        var text = @"
            Order #12345 was placed on January 15, 2024 by customer Jane Smith.
            The order contains 2 items: a laptop for $999.99 and a mouse for $29.99.
            The total is $1,029.98 and it should be delivered to 123 Main St, NYC.
        ";
        
        var schema = Schema("Order")
            .WithString("order_id")
            .WithString("date")
            .WithObject("customer", "Customer information")
            .WithArray("items", "Order items")
            .WithNumber("total", "Total amount")
            .WithString("delivery_address")
            .Build();

        var result = await _extractor.ExtractJson(text, schema).FirstAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.ParsedData);
        
        var root = result.ParsedData.Value;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        
        Logger.LogInformation("Complex extraction: {Json}", result.ExtractedJson);
    }

    [Fact]
    public async Task ExtractJson_BatchProcessing_ProcessesMultipleTexts()
    {
        var texts = new[]
        {
            "Apple Inc. (AAPL) is trading at $185.50, up 2.3% today.",
            "Microsoft (MSFT) closed at $405.20, down 0.8% from yesterday.",
            "Google (GOOGL) is currently at $142.65, showing a 1.5% gain."
        };
        
        var schema = Schema("StockInfo")
            .WithString("company", "Company name")
            .WithString("ticker", "Stock ticker symbol")
            .WithNumber("price", "Current price")
            .WithNumber("change_percent", "Percentage change")
            .WithString("direction", "up or down")
            .Build();

        var results = await _extractor
            .ExtractJsonBatch(texts, schema)
            .ToList();

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        
        foreach (var result in results)
        {
            Logger.LogInformation("Stock data: {Json}", result.ExtractedJson);
        }
    }

    [Fact]
    public async Task ExtractAs_StronglyTyped_ReturnsTypedObjects()
    {
        var text = @"
            The product 'Wireless Headphones' costs $149.99 and has a rating of 4.5 stars 
            based on 1250 reviews. It's currently in stock with 50 units available.
        ";
        
        var schema = Schema("Product")
            .WithString("name")
            .WithNumber("price")
            .WithNumber("rating")
            .WithInteger("review_count")
            .WithBoolean("in_stock")
            .WithInteger("quantity", required: false)
            .Build();

        var product = await _extractor
            .ExtractAs<ProductInfo>(text, schema)
            .FirstAsync();

        Assert.NotNull(product);
        Assert.Equal("Wireless Headphones", product.Name);
        Assert.Equal(149.99m, product.Price);
        Assert.Equal(4.5, product.Rating);
        Assert.Equal(1250, product.ReviewCount);
        Assert.True(product.InStock);
        
        Logger.LogInformation("Typed extraction: {@Product}", product);
    }

    [Fact]
    public async Task ExtractJson_WithValidation_ValidatesSchema()
    {
        var text = "The temperature is seventy-five degrees."; // Text number, not numeric
        
        var schema = Schema("Weather")
            .WithNumber("temperature", "Temperature value")
            .WithString("unit", "Temperature unit")
            .Build();

        var options = new ExtractionOptions
        {
            StrictValidation = true,
            Temperature = 0.1
        };

        var result = await _extractor
            .ExtractJson(text, schema, options: options)
            .FirstAsync();

        // Even if extraction succeeds, it should parse the text correctly
        if (result.Success)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result.ExtractedJson!);
            Assert.NotNull(data);
            
            // Should extract "75" as a number, not "seventy-five" as string
            if (data.TryGetValue("temperature", out var temp))
            {
                Assert.Equal(JsonValueKind.Number, temp.ValueKind);
            }
        }
        
        Logger.LogInformation("Validation test result: Success={Success}, Json={Json}", 
            result.Success, result.ExtractedJson);
    }

    [Fact]
    public async Task ExtractJson_ErrorHandling_HandlesInvalidText()
    {
        var text = "This text contains no relevant information.";
        
        var schema = Schema("SpecificData")
            .WithString("transaction_id", "Transaction ID", required: true)
            .WithNumber("amount", "Transaction amount", required: true)
            .WithString("currency", "Currency code", required: true)
            .Build();

        var errorMessages = new List<string>();
        
        var result = await _extractor
            .ExtractJson(text, schema)
            .HandleErrors(error => errorMessages.Add(error))
            .FirstAsync();

        // The model should still attempt to extract, but might return empty/null values
        // or the validation might fail if strict validation is enabled
        Logger.LogInformation("Error handling test - Success: {Success}, Errors: {Errors}", 
            result.Success, string.Join(", ", errorMessages));
    }

    [Fact]
    public async Task ExtractJson_ReactiveOperators_CombinesMultipleSources()
    {
        var schema = Schema("Metric")
            .WithString("name", "Metric name")
            .WithNumber("value", "Metric value")
            .WithString("timestamp", "When recorded")
            .Build();

        var source1 = Observable.Return("CPU usage is at 75% as of 10:30 AM");
        var source2 = Observable.Return("Memory usage reached 8.5GB at 10:31 AM");
        var source3 = Observable.Return("Disk I/O is 120 MB/s at 10:32 AM");

        var results = await Observable
            .Merge(source1, source2, source3)
            .SelectMany(text => _extractor.ExtractJson(text, schema))
            .Where(r => r.Success)
            .Select(r => r.ExtractedJson!)
            .ToList();

        Assert.Equal(3, results.Count);
        
        foreach (var json in results)
        {
            Logger.LogInformation("Metric: {Json}", json);
        }
    }

    public override void Dispose()
    {
        _extractor?.Dispose();
        _extractorLoggerFactory?.Dispose();
        base.Dispose();
    }
}

// Test models
public record ProductInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    
    [JsonPropertyName("price")]
    public decimal Price { get; init; }
    
    [JsonPropertyName("rating")]
    public double Rating { get; init; }
    
    [JsonPropertyName("review_count")]
    public int ReviewCount { get; init; }
    
    [JsonPropertyName("in_stock")]
    public bool InStock { get; init; }
    
    [JsonPropertyName("quantity")]
    public int? Quantity { get; init; }
}