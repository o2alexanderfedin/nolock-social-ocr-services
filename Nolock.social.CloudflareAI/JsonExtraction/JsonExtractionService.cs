using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.Models;

namespace Nolock.social.CloudflareAI.JsonExtraction;

/// <summary>
/// Service for extracting JSON with specified schemas from text using Cloudflare Workers AI
/// </summary>
public sealed class JsonExtractionService : IDisposable
{
    private readonly IWorkersAI _workersAI;
    private readonly ILogger<JsonExtractionService> _logger;
    private readonly Subject<JsonExtractionTask> _extractionSubject;
    private readonly IDisposable _extractionPipeline;

    public JsonExtractionService(
        IWorkersAI workersAI,
        ILogger<JsonExtractionService> logger)
    {
        _workersAI = workersAI ?? throw new ArgumentNullException(nameof(workersAI));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _extractionSubject = new Subject<JsonExtractionTask>();

        // Set up the reactive pipeline
        _extractionPipeline = SetupPipeline();
    }

    /// <summary>
    /// Extract JSON from text with a specified schema
    /// </summary>
    public IObservable<JsonExtractionResult> ExtractJson(
        string text,
        JsonSchema schema,
        string? model = null,
        ExtractionOptions? options = null)
    {
        var task = new JsonExtractionTask
        {
            Text = text,
            Schema = schema,
            Model = model ?? TextGenerationModels.Llama3_3_70B_Instruct_FP8_Fast,
            Options = options ?? new ExtractionOptions()
        };

        return Observable.Create<JsonExtractionResult>(observer =>
        {
            var resultSubject = new Subject<JsonExtractionResult>();
            task.ResultSubject = resultSubject;
            
            var subscription = resultSubject.Subscribe(observer);
            _extractionSubject.OnNext(task);
            
            return subscription;
        });
    }

    /// <summary>
    /// Extract JSON from multiple texts in parallel
    /// </summary>
    public IObservable<JsonExtractionResult> ExtractJsonBatch(
        IEnumerable<string> texts,
        JsonSchema schema,
        string? model = null,
        ExtractionOptions? options = null)
    {
        return texts.ToObservable()
            .SelectMany(text => ExtractJson(text, schema, model, options));
    }

    private IDisposable SetupPipeline()
    {
        return _extractionSubject
            .Select(task => Observable.FromAsync(async ct => 
            {
                try
                {
                    var result = await ProcessExtractionTask(task, ct);
                    return (task, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing extraction task");
                    return (task, new JsonExtractionResult 
                    { 
                        Success = false, 
                        Error = ex.Message,
                        OriginalText = task.Text
                    });
                }
            }))
            .Merge(maxConcurrent: 5) // Process up to 5 extractions concurrently
            .Retry(3) // Retry failed extractions up to 3 times
            .Subscribe(
                result =>
                {
                    result.task.ResultSubject?.OnNext(result.Item2);
                    result.task.ResultSubject?.OnCompleted();
                },
                error => _logger.LogError(error, "Pipeline error")
            );
    }

    private async Task<JsonExtractionResult> ProcessExtractionTask(
        JsonExtractionTask task,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing extraction task for schema: {Schema}", task.Schema.Name);

        // Build the extraction prompt
        var prompt = BuildExtractionPrompt(task.Text, task.Schema, task.Options);

        // Create the request - for now, use simple prompt approach as response_format is not fully supported
        var request = new TextGenerationRequest
        {
            Messages = new[]
            {
                new Message 
                { 
                    Role = "system", 
                    Content = "You are a JSON extraction assistant. Extract the requested information from the text and return it as valid JSON matching the specified schema. Only return the JSON object, no additional text."
                },
                new Message 
                { 
                    Role = "user", 
                    Content = prompt 
                }
            },
            MaxTokens = task.Options.MaxTokens,
            Temperature = task.Options.Temperature
        };

        // Call Cloudflare AI
        var response = await _workersAI.RunAsync<TextGenerationResponse>(
            task.Model,
            request,
            cancellationToken);

        var rawJsonText = response.Response ?? response.GeneratedText ?? "";
        
        // Clean the JSON text (remove markdown formatting if present)
        var jsonText = CleanJsonText(rawJsonText);

        // Validate and parse the JSON
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonText);
            
            // Validate against schema if strict validation is enabled
            if (task.Options.StrictValidation)
            {
                var validationErrors = ValidateAgainstSchema(jsonDoc, task.Schema);
                if (validationErrors.Any())
                {
                    return new JsonExtractionResult
                    {
                        Success = false,
                        Error = $"Schema validation failed: {string.Join(", ", validationErrors)}",
                        OriginalText = task.Text,
                        ExtractedJson = jsonText
                    };
                }
            }

            return new JsonExtractionResult
            {
                Success = true,
                ExtractedJson = jsonText,
                ParsedData = jsonDoc.RootElement.Clone(),
                OriginalText = task.Text,
                Schema = task.Schema
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Invalid JSON extracted: {Json}", jsonText);
            return new JsonExtractionResult
            {
                Success = false,
                Error = $"Invalid JSON: {ex.Message}",
                OriginalText = task.Text,
                ExtractedJson = jsonText
            };
        }
    }

    /// <summary>
    /// Cleans JSON text by removing markdown formatting and other common AI response artifacts
    /// </summary>
    private static string CleanJsonText(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return rawJson;

        var cleaned = rawJson.Trim();
        
        // Remove markdown code blocks
        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            // Find the end of the opening markdown
            var startIndex = cleaned.IndexOf('\n');
            if (startIndex != -1)
            {
                cleaned = cleaned.Substring(startIndex + 1);
            }
        }
        else if (cleaned.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            // Handle generic code blocks
            var startIndex = cleaned.IndexOf('\n');
            if (startIndex != -1)
            {
                cleaned = cleaned.Substring(startIndex + 1);
            }
        }
        
        // Remove closing markdown
        if (cleaned.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            var endIndex = cleaned.LastIndexOf("```");
            if (endIndex > 0)
            {
                cleaned = cleaned.Substring(0, endIndex);
            }
        }
        
        // Remove common prefixes that AI might add
        cleaned = cleaned.Trim();
        if (cleaned.StartsWith("Here is the JSON:", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("Here's the JSON:", StringComparison.OrdinalIgnoreCase))
        {
            var colonIndex = cleaned.IndexOf(':');
            if (colonIndex != -1 && colonIndex < cleaned.Length - 1)
            {
                cleaned = cleaned.Substring(colonIndex + 1).Trim();
            }
        }
        
        return cleaned.Trim();
    }

    private string BuildExtractionPrompt(string text, JsonSchema schema, ExtractionOptions options)
    {
        var prompt = $@"Extract information from the following text and return it as JSON matching this schema:

Schema Name: {schema.Name}
{schema.Description}

Required fields:
{string.Join("\n", schema.Properties.Where(p => p.Required).Select(p => $"- {p.Name} ({p.Type}): {p.Description}"))}

Optional fields:
{string.Join("\n", schema.Properties.Where(p => !p.Required).Select(p => $"- {p.Name} ({p.Type}): {p.Description}"))}

Text to extract from:
{text}

{(options.Examples.Any() ? $"\nExamples:\n{string.Join("\n", options.Examples)}" : "")}

Remember to return ONLY valid JSON matching the schema, no additional text.";

        return prompt;
    }

    private List<string> ValidateAgainstSchema(JsonDocument json, JsonSchema schema)
    {
        var errors = new List<string>();
        var root = json.RootElement;

        // Check required properties
        foreach (var prop in schema.Properties.Where(p => p.Required))
        {
            if (!root.TryGetProperty(prop.Name, out _))
            {
                errors.Add($"Missing required property: {prop.Name}");
            }
        }

        // Check property types
        foreach (var prop in schema.Properties)
        {
            if (root.TryGetProperty(prop.Name, out var element))
            {
                if (!IsValidType(element, prop.Type))
                {
                    errors.Add($"Property '{prop.Name}' has wrong type. Expected: {prop.Type}, Got: {element.ValueKind}");
                }
            }
        }

        return errors;
    }

    private bool IsValidType(JsonElement element, string expectedType)
    {
        return expectedType.ToLower() switch
        {
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _),
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            "array" => element.ValueKind == JsonValueKind.Array,
            "object" => element.ValueKind == JsonValueKind.Object,
            _ => true
        };
    }

    public void Dispose()
    {
        _extractionPipeline?.Dispose();
        _extractionSubject?.Dispose();
    }
}

/// <summary>
/// Represents a JSON extraction task
/// </summary>
internal sealed class JsonExtractionTask
{
    public required string Text { get; init; }
    public required JsonSchema Schema { get; init; }
    public required string Model { get; init; }
    public required ExtractionOptions Options { get; init; }
    public Subject<JsonExtractionResult>? ResultSubject { get; set; }
}

/// <summary>
/// Options for JSON extraction
/// </summary>
public sealed record ExtractionOptions
{
    public int MaxTokens { get; init; } = 1000;
    public double Temperature { get; init; } = 0.1;
    public bool StrictValidation { get; init; } = true;
    public List<string> Examples { get; init; } = new();
}

/// <summary>
/// Result of JSON extraction
/// </summary>
public sealed record JsonExtractionResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public required string OriginalText { get; init; }
    public string? ExtractedJson { get; init; }
    public JsonElement? ParsedData { get; init; }
    public JsonSchema? Schema { get; init; }
}

/// <summary>
/// Defines a JSON schema for extraction
/// </summary>
public sealed class JsonSchema
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<JsonProperty> Properties { get; init; } = new();

    public object ToJsonSchema()
    {
        return new
        {
            type = "object",
            properties = Properties.ToDictionary(
                p => p.Name,
                p => new { type = p.Type.ToLower(), description = p.Description }
            ),
            required = Properties.Where(p => p.Required).Select(p => p.Name).ToArray()
        };
    }
}

/// <summary>
/// Defines a property in a JSON schema
/// </summary>
public sealed record JsonProperty
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; } = true;
}