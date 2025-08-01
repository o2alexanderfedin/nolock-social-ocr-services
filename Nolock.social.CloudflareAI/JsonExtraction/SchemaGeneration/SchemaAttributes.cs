using System;
using System.ComponentModel;

namespace Nolock.social.CloudflareAI.JsonExtraction.SchemaGeneration;

/// <summary>
/// Attribute to customize schema generation for a type
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class JsonSchemaAttribute : Attribute
{
    /// <summary>
    /// The name of the schema
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of the schema
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Attribute to customize schema generation for a property
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonPropertyAttribute : Attribute
{
    /// <summary>
    /// The JSON property name (if different from C# property name)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of the property
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this property is required
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Override the inferred type
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Whether to ignore this property in schema generation
    /// </summary>
    public bool Ignore { get; set; }
}

/// <summary>
/// Attribute to mark a property as optional in the schema
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonOptionalAttribute : Attribute
{
}

/// <summary>
/// Attribute to ignore a property in schema generation
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonIgnoreInSchemaAttribute : Attribute
{
}