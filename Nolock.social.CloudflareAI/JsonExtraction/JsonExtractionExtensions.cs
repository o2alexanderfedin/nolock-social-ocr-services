using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nolock.social.CloudflareAI.Interfaces;

namespace Nolock.social.CloudflareAI.JsonExtraction;

/// <summary>
/// Extension methods for JSON extraction
/// </summary>
public static class JsonExtractionExtensions
{
    /// <summary>
    /// Add JSON extraction service to the service collection
    /// </summary>
    public static IServiceCollection AddJsonExtraction(this IServiceCollection services)
    {
        services.AddSingleton<JsonExtractionService>();
        return services;
    }

    /// <summary>
    /// Create a JSON extraction pipeline from a Workers AI client
    /// </summary>
    public static JsonExtractionService CreateJsonExtractor(
        this IWorkersAI workersAI, 
        ILogger<JsonExtractionService>? logger = null)
    {
        if (logger == null)
        {
            using var loggerFactory = new LoggerFactory();
            logger = loggerFactory.CreateLogger<JsonExtractionService>();
        }
        return new JsonExtractionService(workersAI, logger);
    }

    /// <summary>
    /// Extract strongly typed data from text
    /// </summary>
    public static IObservable<T> ExtractAs<T>(
        this JsonExtractionService service,
        string text,
        JsonSchema schema,
        string? model = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.ExtractJson(text, schema, model)
            .Where(r => r.Success && r.ParsedData.HasValue)
            .Select(r => JsonSerializer.Deserialize<T>(r.ExtractedJson!, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!)
            .Where(obj => obj != null);
    }

    /// <summary>
    /// Create a schema builder
    /// </summary>
    public static JsonSchemaBuilder Schema(string name, string? description = null)
    {
        return new JsonSchemaBuilder(name, description);
    }

    /// <summary>
    /// Transform extraction results to specific type
    /// </summary>
    public static IObservable<T> ParseAs<T>(
        this IObservable<JsonExtractionResult> results) where T : class
    {
        return results
            .Where(r => r.Success && !string.IsNullOrEmpty(r.ExtractedJson))
            .Select(r => JsonSerializer.Deserialize<T>(r.ExtractedJson!, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!)
            .Where(obj => obj != null);
    }

    /// <summary>
    /// Handle extraction errors
    /// </summary>
    public static IObservable<JsonExtractionResult> HandleErrors(
        this IObservable<JsonExtractionResult> results,
        Action<string> errorHandler)
    {
        return results.Do(r =>
        {
            if (!r.Success && !string.IsNullOrEmpty(r.Error))
            {
                errorHandler(r.Error);
            }
        });
    }

    /// <summary>
    /// Retry failed extractions with different parameters
    /// </summary>
    public static IObservable<JsonExtractionResult> RetryWithHigherTemperature(
        this IObservable<JsonExtractionResult> results,
        JsonExtractionService service,
        int maxRetries = 2)
    {
        return results.SelectMany(r =>
        {
            if (r.Success || maxRetries <= 0)
                return Observable.Return(r);

            var newOptions = new ExtractionOptions
            {
                Temperature = Math.Min(0.9, (r.Schema?.Properties.Count ?? 0) > 5 ? 0.5 : 0.3),
                MaxTokens = 1500,
                StrictValidation = false
            };

            return service.ExtractJson(
                r.OriginalText,
                r.Schema!,
                options: newOptions)
                .RetryWithHigherTemperature(service, maxRetries - 1);
        });
    }
}

/// <summary>
/// Fluent builder for JSON schemas
/// </summary>
public sealed class JsonSchemaBuilder
{
    private readonly JsonSchema _schema;

    internal JsonSchemaBuilder(string name, string? description)
    {
        _schema = new JsonSchema
        {
            Name = name,
            Description = description
        };
    }

    /// <summary>
    /// Add a required property
    /// </summary>
    public JsonSchemaBuilder WithRequired(string name, string type, string? description = null)
    {
        _schema.Properties.Add(new JsonProperty
        {
            Name = name,
            Type = type,
            Description = description,
            Required = true
        });
        return this;
    }

    /// <summary>
    /// Add an optional property
    /// </summary>
    public JsonSchemaBuilder WithOptional(string name, string type, string? description = null)
    {
        _schema.Properties.Add(new JsonProperty
        {
            Name = name,
            Type = type,
            Description = description,
            Required = false
        });
        return this;
    }

    /// <summary>
    /// Add a string property
    /// </summary>
    public JsonSchemaBuilder WithString(string name, string? description = null, bool required = true)
    {
        return required 
            ? WithRequired(name, "string", description)
            : WithOptional(name, "string", description);
    }

    /// <summary>
    /// Add a number property
    /// </summary>
    public JsonSchemaBuilder WithNumber(string name, string? description = null, bool required = true)
    {
        return required 
            ? WithRequired(name, "number", description)
            : WithOptional(name, "number", description);
    }

    /// <summary>
    /// Add an integer property
    /// </summary>
    public JsonSchemaBuilder WithInteger(string name, string? description = null, bool required = true)
    {
        return required 
            ? WithRequired(name, "integer", description)
            : WithOptional(name, "integer", description);
    }

    /// <summary>
    /// Add a boolean property
    /// </summary>
    public JsonSchemaBuilder WithBoolean(string name, string? description = null, bool required = true)
    {
        return required 
            ? WithRequired(name, "boolean", description)
            : WithOptional(name, "boolean", description);
    }

    /// <summary>
    /// Add an array property
    /// </summary>
    public JsonSchemaBuilder WithArray(string name, string? description = null, bool required = true)
    {
        return required 
            ? WithRequired(name, "array", description)
            : WithOptional(name, "array", description);
    }

    /// <summary>
    /// Add an object property
    /// </summary>
    public JsonSchemaBuilder WithObject(string name, string? description = null, bool required = true)
    {
        return required 
            ? WithRequired(name, "object", description)
            : WithOptional(name, "object", description);
    }

    /// <summary>
    /// Build the schema
    /// </summary>
    public JsonSchema Build() => _schema;

    /// <summary>
    /// Implicit conversion to JsonSchema
    /// </summary>
    public static implicit operator JsonSchema(JsonSchemaBuilder builder) => builder?.Build() ?? throw new ArgumentNullException(nameof(builder));
}