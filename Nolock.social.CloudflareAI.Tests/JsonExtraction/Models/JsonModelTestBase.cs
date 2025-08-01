using System.Text.Json;
using Xunit;

namespace Nolock.social.CloudflareAI.Tests.JsonExtraction.Models;

/// <summary>
/// Base class for JSON model tests with common setup and utilities
/// </summary>
public abstract class JsonModelTestBase
{
    /// <summary>
    /// Common JSON serializer options for testing
    /// </summary>
    protected readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Asserts that a decimal property is properly serialized as a JSON number
    /// </summary>
    /// <typeparam name="T">The model type</typeparam>
    /// <param name="model">The model instance</param>
    /// <param name="propertyGetter">Function to get the property value</param>
    /// <param name="jsonPropertyName">The JSON property name</param>
    protected void AssertDecimalSerialization<T>(T model, Func<T, decimal?> propertyGetter, string jsonPropertyName)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(propertyGetter);
        ArgumentNullException.ThrowIfNull(jsonPropertyName);
        
        // Arrange
        var expectedValue = propertyGetter(model);
        
        // Act
        var json = JsonSerializer.Serialize(model, JsonOptions);
        var parsed = JsonDocument.Parse(json);
        
        // Assert
        if (expectedValue.HasValue)
        {
            Assert.Equal(JsonValueKind.Number, parsed.RootElement.GetProperty(jsonPropertyName).ValueKind);
            Assert.Equal(expectedValue.Value, parsed.RootElement.GetProperty(jsonPropertyName).GetDecimal());
        }
        else
        {
            // Property should be null or not present
            if (parsed.RootElement.TryGetProperty(jsonPropertyName, out var property))
            {
                Assert.Equal(JsonValueKind.Null, property.ValueKind);
            }
        }
    }

    /// <summary>
    /// Asserts successful round-trip serialization/deserialization
    /// </summary>
    /// <typeparam name="T">The model type</typeparam>
    /// <param name="original">The original model instance</param>
    /// <param name="propertyAssertions">Additional property assertions to perform</param>
    protected void AssertRoundTripSerialization<T>(T original, Action<T, T>? propertyAssertions = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        
        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<T>(json, JsonOptions);
        
        // Assert
        Assert.NotNull(deserialized);
        
        // Allow additional property-specific assertions
        propertyAssertions?.Invoke(original, deserialized);
    }

    /// <summary>
    /// Creates a test JSON string with the specified numeric value
    /// </summary>
    /// <param name="propertyName">The JSON property name</param>
    /// <param name="numericValue">The numeric value</param>
    /// <returns>A JSON string for testing</returns>
    protected static string CreateTestJson(string propertyName, object numericValue)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(numericValue);
        
        return $$"""
        {
            "{{propertyName}}": {{numericValue}}
        }
        """;
    }
}