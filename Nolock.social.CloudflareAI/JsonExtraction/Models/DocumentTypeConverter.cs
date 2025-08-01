using System.ComponentModel;
using System.Globalization;

namespace Nolock.social.CloudflareAI.JsonExtraction.Models;

/// <summary>
/// Type converter for DocumentType enum to handle case-insensitive string conversion
/// </summary>
public sealed class DocumentTypeConverter : TypeConverter
{
    /// <summary>
    /// Returns whether this converter can convert an object of the given type to the type of this converter.
    /// </summary>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <summary>
    /// Converts the given object to the type of this converter.
    /// </summary>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return stringValue.ToLowerInvariant() switch
            {
                "check" => DocumentType.Check,
                "receipt" => DocumentType.Receipt,
                _ => throw new ArgumentException($"Invalid DocumentType value: {stringValue}")
            };
        }

        return base.ConvertFrom(context, culture, value);
    }

    /// <summary>
    /// Returns whether this converter can convert the object to the specified type.
    /// </summary>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    /// <summary>
    /// Converts the given value object to the specified type.
    /// </summary>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is DocumentType documentType)
        {
            return documentType.ToString().ToLowerInvariant();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}