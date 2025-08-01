using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.SchemaGeneration;

namespace Nolock.social.CloudflareAI.JsonExtraction.Examples;

/// <summary>
/// Examples of using reflection-based schema generation
/// </summary>
public static class ReflectionSchemaExamples
{
    /// <summary>
    /// Example: Extract data using a simple POCO class
    /// </summary>
    public static async Task SimplePocoExtraction(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var customerText = @"
            Our customer John Smith (john.smith@email.com) called today about his order.
            He's a premium member since 2020 and has spent over $5000 with us.
            His phone number is 555-123-4567.
        ";

        // Extract directly to Customer type
        var customer = await extractor
            .ExtractFromType<Customer>(customerText)
            .FirstAsync();

        logger.LogInformation("Extracted customer: {@Customer}", customer);
    }

    /// <summary>
    /// Example: Extract data using annotated classes
    /// </summary>
    public static async Task AnnotatedClassExtraction(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var meetingText = @"
            Meeting scheduled for January 15, 2024 at 2:30 PM in Conference Room A.
            Subject: Q1 Planning Session
            Attendees: Sarah Johnson (CEO), Mike Chen (CTO), Lisa Park (CFO)
            Duration: 2 hours
            Meeting will be recorded for those who cannot attend.
        ";

        // The schema is automatically generated from the Meeting class with all annotations
        var meeting = await extractor
            .ExtractFromType<Meeting>(meetingText)
            .FirstAsync();

        logger.LogInformation("Extracted meeting details:");
        logger.LogInformation("  Title: {Title}", meeting.Subject);
        logger.LogInformation("  Date: {Date}", meeting.ScheduledDate);
        logger.LogInformation("  Location: {Location}", meeting.Location);
        logger.LogInformation("  Attendees: {Count}", meeting.Attendees?.Count ?? 0);
    }

    /// <summary>
    /// Example: Batch extraction with type-based schema
    /// </summary>
    public static async Task BatchExtractionWithTypes(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var transactions = new[]
        {
            "Payment of $1,234.56 received from Acme Corp on 2024-01-10, reference: INV-2024-001",
            "Wire transfer sent: $5,000.00 to Supplier XYZ on 2024-01-11, ref: PO-789",
            "Refund processed: $99.99 to customer John Doe on 2024-01-12, order #12345"
        };

        var results = await extractor
            .ExtractFromTypeBatch<FinancialTransaction>(transactions)
            .ToList();

        logger.LogInformation("Processed {Count} transactions:", results.Count);
        foreach (var transaction in results)
        {
            logger.LogInformation("  {Type}: {Amount:C} on {Date}", 
                transaction.Type, transaction.Amount, transaction.Date);
        }
    }

    /// <summary>
    /// Example: Complex nested type extraction
    /// </summary>
    public static async Task ComplexTypeExtraction(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        var companyText = @"
            TechCorp Inc., founded in 2015, is headquartered in San Francisco, CA.
            The company has 1,200 employees and reported $150M in revenue last year.
            
            Leadership team:
            - CEO: Jane Anderson (since 2015)
            - CTO: Robert Kim (since 2016)
            - CFO: Maria Garcia (since 2018)
            
            Main products:
            - CloudSync Pro: Enterprise cloud synchronization ($99/month)
            - DataVault: Secure data storage solution ($149/month)
            - AIAssist: AI-powered productivity suite ($199/month)
            
            The company has offices in New York, London, and Tokyo.
        ";

        var company = await extractor
            .ExtractFromType<CompanyProfile>(companyText)
            .FirstAsync();

        logger.LogInformation("Company: {Name}", company.Name);
        logger.LogInformation("Founded: {Year}", company.FoundedYear);
        logger.LogInformation("Employees: {Count}", company.EmployeeCount);
        logger.LogInformation("Revenue: ${Revenue}M", company.AnnualRevenue);
        logger.LogInformation("Products: {Count}", company.Products?.Count ?? 0);
    }

    /// <summary>
    /// Example: Schema customization with attributes
    /// </summary>
    public static async Task CustomizedSchemaExtraction(IWorkersAI workersAI, ILogger logger)
    {
        using var extractor = workersAI.CreateJsonExtractor();

        // Show the generated schema for debugging
        var schema = ReflectionSchemaExtensions.FromType<CustomizedInvoice>();
        logger.LogInformation("Generated schema properties:");
        foreach (var prop in schema.Properties)
        {
            logger.LogInformation("  - {Name} ({Type}): {Desc} [Required: {Req}]", 
                prop.Name, prop.Type, prop.Description, prop.Required);
        }

        var invoiceText = @"
            Invoice #2024-0123
            Date: January 15, 2024
            Customer: Acme Corporation
            
            Items:
            - Professional Services: 40 hours @ $150/hr = $6,000
            - Software License: 1 year @ $2,400 = $2,400
            
            Subtotal: $8,400
            Tax (8%): $672
            Total: $9,072
            
            Payment due within 30 days.
        ";

        var invoice = await extractor
            .ExtractFromType<CustomizedInvoice>(invoiceText)
            .FirstAsync();

        logger.LogInformation("Invoice extracted with customized schema:");
        logger.LogInformation("  Number: {Number}", invoice.Number);
        logger.LogInformation("  Total: ${Total}", invoice.TotalAmount);
        logger.LogInformation("  Customer: {Customer}", invoice.CustomerName);
    }
}

// Example model classes

public class Customer
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public bool IsPremium { get; set; }
    public int? MemberSince { get; set; }
    public decimal? TotalSpent { get; set; }
}

[JsonSchema(Description = "Meeting information extracted from text")]
public class Meeting
{
    [JsonPropertyName("subject")]
    [Description("Meeting title or subject")]
    public string Subject { get; set; } = "";

    [JsonPropertyName("scheduled_date")]
    [Description("When the meeting is scheduled")]
    public string ScheduledDate { get; set; } = "";

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("duration")]
    [Description("Meeting duration in hours")]
    public double? Duration { get; set; }

    [JsonPropertyName("attendees")]
    public List<Attendee>? Attendees { get; set; }

    [JsonPropertyName("is_recorded")]
    public bool IsRecorded { get; set; }
}

public class Attendee
{
    public string Name { get; set; } = "";
    public string? Title { get; set; }
}

public class FinancialTransaction
{
    [JsonPropertyName("type")]
    [Description("Transaction type: payment, transfer, refund, etc.")]
    public string Type { get; set; } = "";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("party")]
    [Description("The other party in the transaction")]
    public string? Party { get; set; }
}

[Description("Company profile information")]
public class CompanyProfile
{
    public string Name { get; set; } = "";
    
    [JsonPropertyName("founded_year")]
    public int? FoundedYear { get; set; }
    
    public string? Headquarters { get; set; }
    
    [JsonPropertyName("employee_count")]
    public int? EmployeeCount { get; set; }
    
    [JsonPropertyName("annual_revenue")]
    [Description("Annual revenue in millions")]
    public decimal? AnnualRevenue { get; set; }
    
    public List<Executive>? Leadership { get; set; }
    
    public List<Product>? Products { get; set; }
    
    public List<string>? Offices { get; set; }
}

public class Executive
{
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    
    [JsonPropertyName("since_year")]
    public int? SinceYear { get; set; }
}

public class Product
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    
    [JsonPropertyName("price_per_month")]
    public decimal? PricePerMonth { get; set; }
}

// Example with custom attributes
[JsonSchema(Name = "Invoice", Description = "Invoice data with line items and totals")]
public class CustomizedInvoice
{
    [JsonProperty(Name = "invoice_number", Description = "Unique invoice identifier", Required = true)]
    public string Number { get; set; } = "";

    [JsonProperty(Description = "Invoice issue date")]
    public string Date { get; set; } = "";

    [JsonProperty(Name = "customer", Description = "Customer or company name")]
    public string CustomerName { get; set; } = "";

    [JsonProperty(Description = "Individual line items", Required = false)]
    public List<InvoiceLineItem>? Items { get; set; }

    [JsonProperty(Name = "subtotal", Required = false)]
    public decimal? SubtotalAmount { get; set; }

    [JsonProperty(Name = "tax", Description = "Tax amount")]
    public decimal? TaxAmount { get; set; }

    [JsonProperty(Name = "total", Description = "Total amount due", Required = true)]
    public decimal TotalAmount { get; set; }

    [JsonIgnoreInSchema]
    public string? InternalNotes { get; set; }

    [JsonOptional]
    public int? PaymentTermsDays { get; set; }
}

public class InvoiceLineItem
{
    public string Description { get; set; } = "";
    public int? Quantity { get; set; }
    
    [JsonPropertyName("unit_price")]
    public decimal? UnitPrice { get; set; }
    
    public decimal Amount { get; set; }
}