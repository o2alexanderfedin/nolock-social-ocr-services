# OCR to Model Pipeline Implementation Plan

## Overview
Create a simple, clean pipeline that processes images through OCR to extract structured data models (Receipt/Check) using existing services.

## Existing Components

### 1. MistralOcrService (Image → Markdown)
- **Purpose**: Converts images to markdown-formatted text
- **Methods**:
  - `ProcessImageStreamAsync(Stream stream, string mimeType)` - Process from stream
  - `ProcessImageDataItemAsync((string dataUrl, string mimeType))` - Process from data URL
  - `ProcessImageBytesAsync(byte[] bytes, string mimeType)` - Process from byte array
- **Returns**: `MistralOcrResult` containing extracted text

### 2. JsonExtractionService (Text → Model)
- **Purpose**: Extracts structured data from text using Cloudflare Workers AI
- **Method**: `ExtractFromType<T>(string text)` 
- **Models**: Uses Llama 3.3 70B Instruct for high accuracy
- **Returns**: Observable of typed model (`Receipt` or `Check`)

### 3. Existing Models
- `Receipt` / `SimpleReceipt` - For receipt data extraction
- `Check` / `SimpleCheck` - For check data extraction
- Models already have all necessary properties and validation

## Design Principles (SOLID, KISS, DRY, YAGNI)

1. **Single Responsibility**: Each service does one thing well
2. **Keep It Simple**: Direct pipeline without unnecessary abstractions
3. **Don't Repeat**: Reuse existing services and models
4. **No Over-engineering**: Only what's needed, no fallbacks or complex error handling

## Implementation Plan

### Phase 1: Simple Pipeline Service

Create a single service that orchestrates the pipeline:

```csharp
public class OcrToModelPipeline
{
    private readonly IMistralOcrService _ocrService;
    private readonly IWorkersAI _workersAI;
    private readonly ILogger<OcrToModelPipeline> _logger;

    public async Task<Receipt> ProcessReceiptImage(Stream imageStream, string mimeType)
    {
        // Step 1: OCR
        var ocrResult = await _ocrService.ProcessImageStreamAsync(imageStream, mimeType);
        
        // Step 2: Extract Receipt
        using var extractor = _workersAI.CreateJsonExtractor();
        var receipt = await extractor
            .ExtractFromType<Receipt>(ocrResult.Text)
            .FirstAsync();
            
        return receipt;
    }

    public async Task<Check> ProcessCheckImage(Stream imageStream, string mimeType)
    {
        // Step 1: OCR
        var ocrResult = await _ocrService.ProcessImageStreamAsync(imageStream, mimeType);
        
        // Step 2: Extract Check
        using var extractor = _workersAI.CreateJsonExtractor();
        var check = await extractor
            .ExtractFromType<Check>(ocrResult.Text)
            .FirstAsync();
            
        return check;
    }
}
```

### Phase 2: Reactive Extension (Optional)

If reactive patterns are desired, wrap in Observable:

```csharp
public IObservable<Receipt> ProcessReceiptImageReactive(Stream imageStream, string mimeType)
{
    return Observable.FromAsync(async () => 
    {
        var ocrResult = await _ocrService.ProcessImageStreamAsync(imageStream, mimeType);
        using var extractor = _workersAI.CreateJsonExtractor();
        return await extractor.ExtractFromType<Receipt>(ocrResult.Text).FirstAsync();
    });
}
```

### Phase 3: Integration Points

1. **Dependency Injection**:
   ```csharp
   services.AddScoped<OcrToModelPipeline>();
   ```

2. **API Endpoint** (if needed):
   ```csharp
   [HttpPost("process/receipt")]
   public async Task<Receipt> ProcessReceipt(IFormFile image)
   {
       using var stream = image.OpenReadStream();
       return await _pipeline.ProcessReceiptImage(stream, image.ContentType);
   }
   ```

## Key Decisions

1. **No Fallbacks**: Trust the AI services to work correctly
2. **No Complex Error Handling**: Let exceptions bubble up
3. **No Retry Logic**: Keep it simple, let the caller retry if needed
4. **Direct Integration**: No abstraction layers between services
5. **Use Existing Models**: Don't create new Receipt/Check models

## Testing Strategy

1. **Unit Tests**: Mock MistralOcrService and JsonExtractionService
2. **Integration Tests**: Use real services with test images
3. **Test Data**: Use the provided test images in TestImages folder

## File Structure

```
/Nolock.social.OCRservices/
  /Pipelines/
    OcrToModelPipeline.cs     # Main pipeline implementation
  
/Nolock.social.OCRservices.Tests/
  /Pipelines/
    OcrToModelPipelineTests.cs # Tests for the pipeline
```

## Dependencies

- MistralOcr NuGet package (already referenced)
- CloudflareAI NuGet package (already referenced)  
- No additional packages needed

## Success Metrics

1. Simple, readable code (< 100 lines per class)
2. Direct integration without abstractions
3. Works with existing test images
4. No custom error handling or retry logic