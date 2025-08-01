using System.Reactive.Linq;
using Microsoft.AspNetCore.Mvc;
using Nolock.social.MistralOcr;
using Nolock.social.MistralOcr.Extensions;
using Nolock.social.OCRservices;
using Nolock.social.CloudflareAI;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.Services;
using Nolock.social.OCRservices.Core.Models;
using Nolock.social.OCRservices.Core.Pipelines;

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
builder.Services.AddScoped<OcrExtractionService>();

// No longer need image transformation services since we're using stream input

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "OCR Services API V1");
});

var ocrApi = app.MapGroup("/ocr")
    .WithTags("OCR Operations");

// Mistral OCR endpoint using reactive implementation with stream input
ocrApi.MapPost("/async", async (
    IReactiveMistralOcrService reactiveOcrService,
    OcrExtractionService ocrExtractionService,
    HttpContext context,
    [FromBody] Stream image) =>
{
    try
    {
        // Parse document type from query string
        var documentTypeString = context.Request.Query["documentType"].FirstOrDefault();
        if (string.IsNullOrEmpty(documentTypeString))
        {
            return Results.BadRequest("Document type is required. Valid values: check, receipt");
        }

        // Convert string to DocumentType enum
        if (!Enum.TryParse<DocumentType>(documentTypeString, true, out var documentType))
        {
            return Results.BadRequest($"Invalid document type: {documentTypeString}. Valid values: check, receipt");
        }

        // First pipeline step: convert stream to data URL
        var imageToUrlPipeline = new PipelineNodeImageToUrl();
        var dataItem = await imageToUrlPipeline.ProcessAsync(image).ConfigureAwait(false);

        // Process using reactive service with data URL to get OCR text
        var dataItemsObservable = Observable.Return(dataItem);
        var ocrResult = await reactiveOcrService
            .ProcessImageDataItems(dataItemsObservable)
            .FirstOrDefaultAsync();
        
        if (ocrResult == null || string.IsNullOrWhiteSpace(ocrResult.Text))
        {
            return Results.Problem("Failed to extract text from image");
        }

        // Extract structured data based on document type
        var extractionRequest = new OcrExtractionRequest
        {
            DocumentType = documentType,
            Content = ocrResult.Text,
            IsImage = false
        };

        var extractionResponse = await ocrExtractionService.ProcessExtractionRequestAsync(extractionRequest).ConfigureAwait(false);
        
        return !extractionResponse.Success
            ? Results.Problem($"Failed to extract {documentType} data: {extractionResponse.Error}")
            : Results.Ok(new
            {
                documentType = extractionResponse.DocumentType.ToString().ToLower(),
                ocrText = ocrResult.Text,
                extractedData = extractionResponse.Data,
                confidence = extractionResponse.Confidence,
                processingTimeMs = extractionResponse.ProcessingTimeMs
            });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("ProcessOcrAsync")
.WithSummary("Process image with OCR and extract structured data")
.WithDescription("Processes an image using Mistral OCR and extracts structured data based on document type (check or receipt). Pass documentType query parameter with value 'check' or 'receipt'.")
.Accepts<Stream>("application/octet-stream")
.Produces<OcrAsyncResponse>(StatusCodes.Status200OK)
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status400BadRequest);

app.Run();

// Make Program class accessible to tests
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Program class needs to be public for testing")]
public partial class Program { }
