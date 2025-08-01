using Microsoft.AspNetCore.Mvc;
using Nolock.social.MistralOcr;
using Nolock.social.MistralOcr.Extensions;
using Nolock.social.OCRservices;
using Nolock.social.OCRservices.Services;
using Nolock.social.CloudflareAI;
using Nolock.social.CloudflareAI.JsonExtraction.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Services;
using Nolock.social.OCRservices.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON options for minimal APIs
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Add services required for OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Add a custom operation filter to add the documentType parameter
    options.OperationFilter<AddDocumentTypeParameterFilter>();
});

// Add Mistral OCR service
builder.Services.AddMistralOcr(options =>
{
    // Try environment variable first, then configuration
    options.ApiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY") 
        ?? builder.Configuration["MistralOcr:ApiKey"] 
        ?? throw new InvalidOperationException("MISTRAL_API_KEY environment variable is required");
    options.Model = builder.Configuration["MistralOcr:Model"] ?? "mistral-ocr-latest";
});

// Add Reactive Mistral OCR services
builder.Services.AddReactiveMistralOcr(options =>
{
    options.MaxConcurrency = 4;
    options.RetryCount = 3;
    options.RetryDelay = TimeSpan.FromSeconds(1);
});

// Add CloudflareAI services
builder.Services.AddWorkersAI(options =>
{
    // Try environment variables first, then configuration
    options.AccountId = Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID") 
        ?? builder.Configuration["CloudflareAI:AccountId"] 
        ?? throw new InvalidOperationException("CLOUDFLARE_ACCOUNT_ID environment variable is required");
    options.ApiToken = Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN") 
        ?? builder.Configuration["CloudflareAI:ApiToken"] 
        ?? throw new InvalidOperationException("CLOUDFLARE_API_TOKEN environment variable is required");
});

// Add OCR extraction service
builder.Services.AddScoped<IOcrExtractionService, OcrExtractionService>();

// Add simplified OCR request handler
builder.Services.AddScoped<IOcrRequestHandler, OcrRequestHandler>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "OCR Services API V1");
});

var ocrApi = app.MapGroup("/ocr")
    .WithTags("OCR Operations");

// Receipt OCR endpoint
ocrApi.MapPost("/receipts", async (
    IOcrRequestHandler ocrHandler,
    [FromBody] Stream image) => await ocrHandler.HandleReceiptAsync(image).ConfigureAwait(false))
.WithName("ProcessReceiptOcr")
.WithSummary("Process receipt image with OCR and extract structured data")
.WithDescription("Processes a receipt image using Mistral OCR and extracts structured receipt data including merchant info, totals, items, and payment details.")
.Accepts<Stream>("application/octet-stream")
.Produces<ReceiptOcrResponse>(StatusCodes.Status200OK)
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status400BadRequest);

// Check OCR endpoint
ocrApi.MapPost("/checks", async (
    IOcrRequestHandler ocrHandler,
    [FromBody] Stream image) => await ocrHandler.HandleCheckAsync(image).ConfigureAwait(false))
.WithName("ProcessCheckOcr")
.WithSummary("Process check image with OCR and extract structured data")
.WithDescription("Processes a check image using Mistral OCR and extracts structured check data including amount, payee, payer, bank info, and routing details.")
.Accepts<Stream>("application/octet-stream")
.Produces<CheckOcrResponse>(StatusCodes.Status200OK)
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status400BadRequest);

app.Run();

// Make Program class accessible to tests
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Program class needs to be public for testing")]
public partial class Program { }
