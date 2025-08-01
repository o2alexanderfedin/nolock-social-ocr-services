using System.Reactive.Linq;
using Microsoft.AspNetCore.Mvc;
using Nolock.social.MistralOcr;
using Nolock.social.MistralOcr.Extensions;
using Nolock.social.OCRservices;
using Nolock.social.OCRservices.Pipelines;

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

// No longer need image transformation services since we're using stream input

var app = builder.Build();

var ocrApi = app.MapGroup("/ocr");
ocrApi.MapPut("/sync", (string type) => Results.Ok(type));

// Mistral OCR endpoint using reactive implementation with stream input
ocrApi.MapPost("/async", async (
    IReactiveMistralOcrService reactiveOcrService,
    [FromBody] Stream image) =>
{
    try
    {
        // First pipeline step: convert stream to data URL
        var imageToUrlPipeline = new PipelineNodeImageToUrl();
        var dataItem = await imageToUrlPipeline.ProcessAsync(image);

        // Process using reactive service with data URL
        var dataItemsObservable = Observable.Return(dataItem);
        var result = await reactiveOcrService
            .ProcessImageDataItems(dataItemsObservable)
            .FirstOrDefaultAsync();
        
        if (result == null)
        {
            return Results.Problem("Failed to process image");
        }
        
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();
