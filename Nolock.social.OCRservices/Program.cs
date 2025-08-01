using System.Text.Json.Serialization;
using Nolock.social.MistralOcr;

namespace Nolock.social.OCRservices;

public static class Program
{
    public static void Main(string[] args)
    {
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

        var app = builder.Build();

        var ocrApi = app.MapGroup("/ocr");
        ocrApi.MapPut("/sync", (string type) => Results.Ok(type));
        
        // Mistral OCR endpoint
        ocrApi.MapPost("/mistral", async (IMistralOcrService ocrService, MistralOcrEndpointRequest request) =>
        {
            try
            {
                MistralOcrResult result;
                
                if (!string.IsNullOrEmpty(request.ImageUrl))
                {
                    result = await ocrService.ProcessImageAsync(request.ImageUrl, request.Prompt);
                }
                else if (!string.IsNullOrEmpty(request.ImageDataUrl))
                {
                    result = await ocrService.ProcessImageDataUrlAsync(request.ImageDataUrl, request.Prompt);
                }
                else
                {
                    return Results.BadRequest("Either ImageUrl or ImageDataUrl must be provided");
                }
                
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.Run();
    }
}

public record Todo;

[JsonSerializable(typeof(Todo[]))]
[JsonSerializable(typeof(MistralOcrEndpointRequest))]
[JsonSerializable(typeof(MistralOcrResult))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}