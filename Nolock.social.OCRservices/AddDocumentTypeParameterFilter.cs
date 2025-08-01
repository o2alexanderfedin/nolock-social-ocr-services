using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Nolock.social.OCRservices;

/// <summary>
/// Adds the documentType query parameter to the OCR async endpoint
/// </summary>
#pragma warning disable CA1812 // Internal class is instantiated via reflection by Swagger
internal sealed class AddDocumentTypeParameterFilter : IOperationFilter
#pragma warning restore CA1812
{
    /// <summary>
    /// Apply the filter to add documentType parameter
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        
        if (context.ApiDescription.RelativePath?.Contains("ocr/async") == true && 
            context.ApiDescription.HttpMethod == "POST")
        {
            operation.Parameters ??= new List<OpenApiParameter>();
            
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "documentType",
                In = ParameterLocation.Query,
                Required = true,
                Description = "Type of document to process",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Enum = new List<IOpenApiAny>
                    {
                        new OpenApiString("check"),
                        new OpenApiString("receipt")
                    }
                }
            });
        }
    }
}