using System.Reactive.Linq;
using Nolock.social.MistralOcr;
using Nolock.social.MistralOcr.Extensions;
using Nolock.social.OCRservices;

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

// Add Image URL to Data URL transformation services
builder.Services.AddImageTransformation(options =>
{
    options.MaxConcurrency = 4;
    options.RetryCount = 3;
    options.RequestTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

var ocrApi = app.MapGroup("/ocr");
ocrApi.MapPut("/sync", (string type) => Results.Ok(type));

// Mistral OCR endpoint using reactive implementation
ocrApi.MapPost("/mistral", async (
    IReactiveMistralOcrService reactiveOcrService, 
    IImageUrlToDataUrlTransformer transformer,
    MistralOcrEndpointRequest request) =>
{
    try
    {
        if (string.IsNullOrEmpty(request.ImageUrl) && string.IsNullOrEmpty(request.ImageDataUrl))
        {
            return Results.BadRequest("Either ImageUrl or ImageDataUrl must be provided");
        }

        MistralOcrResult? result;

        if (!string.IsNullOrEmpty(request.ImageUrl))
        {
            // For regular URLs, use the transformation pipeline
            var imageObservable = Observable.Return(request.ImageUrl);
            result = await imageObservable
                .ProcessImagesWithTransform(reactiveOcrService, transformer, request.Prompt)
                .FirstOrDefaultAsync();
        }
        else
        {
            // For data URLs, process directly
            var dataUrlObservable = Observable.Return(request.ImageDataUrl!);
            result = await reactiveOcrService
                .ProcessImageDataUrls(dataUrlObservable, request.Prompt)
                .FirstOrDefaultAsync();
        }
        
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
