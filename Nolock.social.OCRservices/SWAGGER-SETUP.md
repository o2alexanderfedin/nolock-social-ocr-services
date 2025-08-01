# Adding Swagger UI to .NET 9 with CreateSlimBuilder

## Overview

Starting with .NET 9, Swagger/Swashbuckle is no longer included by default in ASP.NET Core templates. Microsoft has introduced a new built-in OpenAPI support through `Microsoft.AspNetCore.OpenApi`, but it doesn't include a UI component.

## Options for Adding Swagger UI

### Option 1: Swashbuckle (Traditional Approach)

This is the most straightforward approach if you want the familiar Swagger experience.

#### 1. Install NuGet Package

```bash
dotnet add package Swashbuckle.AspNetCore
```

#### 2. Update Program.cs

```csharp
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateSlimBuilder(args);

// Add services
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "OCR Services API", 
        Version = "v1",
        Description = "API for OCR processing with document type recognition"
    });
    
    // Add XML comments if you have them
    // var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    // var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    // c.IncludeXmlComments(xmlPath);
});

// ... existing services ...

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OCR Services API V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

// ... existing endpoints ...
```

### Option 2: Using Built-in OpenAPI with Swagger UI

This approach uses .NET 9's new built-in OpenAPI support combined with Swagger UI.

#### 1. Install NuGet Packages

```bash
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Swashbuckle.AspNetCore.SwaggerUI
```

#### 2. Update Program.cs

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

// Add OpenAPI
builder.Services.AddOpenApi();

// ... existing services ...

var app = builder.Build();

// Map OpenAPI
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "OCR Services API v1");
        options.RoutePrefix = string.Empty;
    });
}

// ... existing endpoints ...
```

### Option 3: Using NSwag

NSwag is an alternative to Swashbuckle that's actively maintained.

#### 1. Install NuGet Package

```bash
dotnet add package NSwag.AspNetCore
```

#### 2. Update Program.cs

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.PostProcess = document =>
    {
        document.Info.Version = "v1";
        document.Info.Title = "OCR Services API";
        document.Info.Description = "API for OCR processing with document type recognition";
    };
});

// ... existing services ...

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Add OpenAPI v3 document serving middleware
    app.UseOpenApi();
    
    // Add web UI to interact with the document
    app.UseSwaggerUi(); // NSwag's UI
}

// ... existing endpoints ...
```

## Documenting Your Endpoints

### For Minimal APIs

With any of the above approaches, you can document your endpoints like this:

```csharp
ocrApi.MapPost("/async", async (
    IReactiveMistralOcrService reactiveOcrService,
    OcrExtractionService ocrExtractionService,
    [FromQuery] DocumentType? documentType,
    [FromBody] Stream image) =>
{
    // ... implementation ...
})
.WithName("ProcessOcrAsync")
.WithOpenApi(operation => new(operation)
{
    Summary = "Process image with OCR and extract structured data",
    Description = "Processes an image using Mistral OCR and extracts structured data based on document type",
    Parameters = new List<OpenApiParameter>
    {
        new OpenApiParameter
        {
            Name = "documentType",
            In = ParameterLocation.Query,
            Required = true,
            Description = "Type of document to recognize",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = new List<IOpenApiAny>
                {
                    new OpenApiString("check"),
                    new OpenApiString("receipt")
                }
            }
        }
    }
})
.Produces<OcrExtractionResponse>(StatusCodes.Status200OK)
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status400BadRequest);
```

## Recommended Approach

For your OCR Services project using `CreateSlimBuilder`, I recommend **Option 1 (Swashbuckle)** because:

1. It's the most familiar and widely documented
2. Works well with minimal APIs
3. Provides a complete UI solution out of the box
4. Has extensive customization options
5. Compatible with `CreateSlimBuilder`

## Additional Configuration

### Enable Swagger in Production (Optional)

```csharp
var enableSwagger = builder.Configuration.GetValue<bool>("EnableSwagger");

if (app.Environment.IsDevelopment() || enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

Add to `appsettings.json`:
```json
{
  "EnableSwagger": false
}
```

### Custom Swagger Theme

```csharp
app.UseSwaggerUI(c =>
{
    c.InjectStylesheet("/swagger-ui/custom.css");
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "OCR Services API V1");
});
```

## Testing

After implementing, navigate to:
- Swagger UI: `https://localhost:[port]/` (if RoutePrefix is empty)
- OpenAPI JSON: `https://localhost:[port]/swagger/v1/swagger.json`

## Notes on CreateSlimBuilder

`CreateSlimBuilder` is optimized for minimal APIs and has a smaller footprint. It works perfectly with all the Swagger options above. The main difference is that it doesn't include some of the traditional MVC pipeline components, which aren't needed for minimal APIs anyway.