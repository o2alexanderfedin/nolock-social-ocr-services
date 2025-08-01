using System;
using System.Linq;
using System.Reflection;
using Json.Schema;
using Json.Schema.Generation;
using Nolock.social.CloudflareAI.JsonExtraction;

namespace Nolock.social.CloudflareAI.JsonExtraction.SchemaGeneration;

/// <summary>
/// Extension methods for generating JSON schemas from types using reflection
/// </summary>
public static class ReflectionSchemaExtensions
{
    /// <summary>
    /// Generate a JSON schema from a type using reflection
    /// </summary>
    public static JsonSchema FromType<T>(string? name = null, string? description = null)
    {
        return FromType(typeof(T), name, description);
    }

    /// <summary>
    /// Generate a JSON schema from a type using reflection
    /// </summary>
    public static JsonSchema FromType(Type type, string? name = null, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        
        // Use JsonSchema.Net.Generation to create the schema
        var jsonSchema = new Json.Schema.JsonSchemaBuilder().FromType(type).Build();
        
        // Convert to our JsonSchema format
        var schema = new JsonSchema
        {
            Name = name ?? type.Name,
            Description = description ?? GetTypeDescription(type)
        };

        // Extract properties from the generated schema
        if (jsonSchema.Keywords?.OfType<PropertiesKeyword>().FirstOrDefault() is { } propertiesKeyword)
        {
            // Get all properties from the type
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var kvp in propertiesKeyword.Properties)
            {
                var propertyName = kvp.Key;
                var propertySchema = kvp.Value;
                
                // Try to find the property by JSON name or C# name
                var propertyInfo = properties.FirstOrDefault(p => 
                    (GetPropertyJsonName(p) ?? p.Name).Equals(propertyName, StringComparison.OrdinalIgnoreCase));
                
                if (propertyInfo != null)
                {
                    var propertyType = GetJsonType(propertySchema);
                    
                    var jsonProperty = new JsonProperty
                    {
                        Name = propertyName, // Use the name from the generated schema
                        Type = propertyType,
                        Description = GetPropertyDescription(propertyInfo),
                        Required = IsPropertyRequired(propertyInfo, jsonSchema)
                    };
                    
                    schema.Properties.Add(jsonProperty);
                }
            }
        }

        return schema;
    }

    /// <summary>
    /// Create a schema builder from a type
    /// </summary>
    public static JsonSchemaBuilder SchemaFromType<T>(string? name = null, string? description = null)
    {
        var schema = FromType<T>(name, description);
        var builder = new JsonSchemaBuilder(schema.Name, schema.Description);
        
        foreach (var prop in schema.Properties)
        {
            if (prop.Required)
                builder.WithRequired(prop.Name, prop.Type, prop.Description);
            else
                builder.WithOptional(prop.Name, prop.Type, prop.Description);
        }
        
        return builder;
    }

    /// <summary>
    /// Extract JSON from text using a schema generated from a type
    /// </summary>
    public static IObservable<T> ExtractFromType<T>(
        this JsonExtractionService service,
        string text,
        string? model = null,
        ExtractionOptions? options = null) where T : class, new()
    {
        var schema = FromType<T>();
        return service.ExtractAs<T>(text, schema, model);
    }

    /// <summary>
    /// Extract JSON from multiple texts using a schema generated from a type
    /// </summary>
    public static IObservable<T> ExtractFromTypeBatch<T>(
        this JsonExtractionService service,
        IEnumerable<string> texts,
        string? model = null,
        ExtractionOptions? options = null) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(service);
        
        var schema = FromType<T>();
        return service.ExtractJsonBatch(texts, schema, model, options)
            .ParseAs<T>();
    }

    private static string GetJsonType(Json.Schema.JsonSchema propertySchema)
    {
        // Try to determine the type from the schema
        if (propertySchema.Keywords?.OfType<TypeKeyword>().FirstOrDefault() is { } typeKeyword)
        {
            // Handle flags - check for specific types
            if (typeKeyword.Type.HasFlag(SchemaValueType.Array))
                return "array";
            if (typeKeyword.Type.HasFlag(SchemaValueType.Object))
                return "object";
            if (typeKeyword.Type.HasFlag(SchemaValueType.String))
                return "string";
            if (typeKeyword.Type.HasFlag(SchemaValueType.Number))
                return "number";
            if (typeKeyword.Type.HasFlag(SchemaValueType.Integer))
                return "integer";
            if (typeKeyword.Type.HasFlag(SchemaValueType.Boolean))
                return "boolean";
                
            return "string"; // Default
        }

        // Check for anyOf/oneOf which might indicate nullable types
        if (propertySchema.Keywords?.Any(k => k is AnyOfKeyword || k is OneOfKeyword) == true)
        {
            // This might be a nullable type, try to find the actual type
            if (propertySchema.Keywords.OfType<AnyOfKeyword>().FirstOrDefault() is { } anyOf)
            {
                var nonNullSchema = anyOf.Schemas.FirstOrDefault(s => 
                    !s.Keywords?.Any(k => k is TypeKeyword tk && tk.Type == SchemaValueType.Null) ?? false);
                
                if (nonNullSchema != null)
                {
                    return GetJsonType(nonNullSchema);
                }
            }
        }

        return "string"; // Default to string
    }

    private static bool IsPropertyRequired(PropertyInfo property, Json.Schema.JsonSchema schema)
    {
        var jsonName = GetPropertyJsonName(property) ?? property.Name;
        
        // Check if property name is in the required array
        if (schema.Keywords?.OfType<RequiredKeyword>().FirstOrDefault() is { } requiredKeyword)
        {
            return requiredKeyword.Properties.Contains(jsonName, StringComparer.OrdinalIgnoreCase);
        }

        // Check for nullable reference types or nullable value types
        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property);
        
        return nullabilityInfo.WriteState != NullabilityState.Nullable;
    }

    private static string? GetPropertyJsonName(PropertyInfo property)
    {
        // Check for System.Text.Json attribute
        var jsonPropertyNameAttr = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
        if (jsonPropertyNameAttr != null)
            return jsonPropertyNameAttr.Name;

        // Check for our custom attribute
        var jsonPropertyAttr = property.GetCustomAttribute<JsonPropertyAttribute>();
        if (jsonPropertyAttr != null)
            return jsonPropertyAttr.Name;

        return null;
    }

    private static string? GetTypeDescription(Type type)
    {
        // Check for our custom attribute
        var schemaAttr = type.GetCustomAttribute<JsonSchemaAttribute>();
        if (schemaAttr?.Description != null)
            return schemaAttr.Description;

        // Check for DescriptionAttribute
        var descAttr = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        if (descAttr != null)
            return descAttr.Description;

        return null;
    }

    private static string? GetPropertyDescription(PropertyInfo property)
    {
        // Check for our custom attribute
        var jsonPropertyAttr = property.GetCustomAttribute<JsonPropertyAttribute>();
        if (jsonPropertyAttr?.Description != null)
            return jsonPropertyAttr.Description;

        // Check for DescriptionAttribute
        var descAttr = property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        if (descAttr != null)
            return descAttr.Description;

        // Check for JsonSchema.Net.Generation attributes
        var generationDescAttr = property.GetCustomAttribute<Json.Schema.Generation.DescriptionAttribute>();
        if (generationDescAttr != null)
            return generationDescAttr.Description;

        return null;
    }
}

/// <summary>
/// Configuration for schema generation
/// </summary>
public class SchemaGenerationConfiguration
{
    /// <summary>
    /// Configuration for JsonSchema.Net.Generation
    /// </summary>
    public SchemaGeneratorConfiguration GeneratorConfiguration { get; set; } = new();

    /// <summary>
    /// Whether to include descriptions from XML documentation
    /// </summary>
    public bool IncludeXmlDocumentation { get; set; } = true;

    /// <summary>
    /// Whether to treat nullable reference types as optional
    /// </summary>
    public bool NullableReferenceTypesAsOptional { get; set; } = true;
}