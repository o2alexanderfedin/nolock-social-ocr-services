using System.Reactive.Linq;
using Microsoft.AspNetCore.Mvc;
using Nolock.social.MistralOcr;
using Nolock.social.MistralOcr.Extensions;
using Nolock.social.OCRservices;
using Nolock.social.OCRservices.Pipelines;
using Nolock.social.CloudflareAI;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Add Mistral OCR service
builder.Services.AddMistralOcr(options =>
{
    options.ApiKey = builder.Configuration["MistralOcr:ApiKey"] ?? throw new InvalidOperationException("MistralOcr:ApiKey is required");
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
    options.AccountId = builder.Configuration["CloudflareAI:AccountId"] ?? throw new InvalidOperationException("CloudflareAI:AccountId is required");
    options.ApiToken = builder.Configuration["CloudflareAI:ApiToken"] ?? throw new InvalidOperationException("CloudflareAI:ApiToken is required");
});

// Add OCR extraction service
builder.Services.AddScoped<OcrExtractionService>();

// No longer need image transformation services since we're using stream input

var app = builder.Build();

var ocrApi = app.MapGroup("/ocr");
ocrApi.MapPut("/sync", (string type) => Results.Ok(type));

// Mistral OCR endpoint using reactive implementation with stream input
ocrApi.MapPost("/async", async (
    IReactiveMistralOcrService reactiveOcrService,
    OcrExtractionService ocrExtractionService,
    [FromQuery] DocumentType? documentType,
    [FromBody] Stream image) =>
{
    try
    {
        // Validate document type
        if (documentType == null)
        {
            return Results.BadRequest("Document type is required. Valid values: check, receipt");
        }

        // First pipeline step: convert stream to data URL
        var imageToUrlPipeline = new PipelineNodeImageToUrl();
        var dataItem = await imageToUrlPipeline.ProcessAsync(image);

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
            DocumentType = documentType.Value,
            Content = ocrResult.Text,
            IsImage = false
        };

        var extractionResponse = await ocrExtractionService.ProcessExtractionRequestAsync(extractionRequest);
        
        if (!extractionResponse.Success)
        {
            return Results.Problem($"Failed to extract {documentType} data: {extractionResponse.Error}");
        }
        
        return Results.Ok(new
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
});

app.Run();
