using Nolock.social.CloudflareAI.JsonExtraction.Models;

namespace Nolock.social.OCRservices.Services;

/// <summary>
/// Validates document type parameters for OCR requests
/// </summary>
public interface IDocumentTypeValidator
{
    ValidationResult ValidateDocumentType(string? documentTypeString);
}

/// <summary>
/// Result of document type validation
/// </summary>
public record ValidationResult(bool IsValid, string? ErrorMessage, DocumentType DocumentType)
{
    public static ValidationResult Valid(DocumentType documentType) => new(true, null, documentType);
    public static ValidationResult Invalid(string errorMessage) => new(false, errorMessage, default);
}

/// <summary>
/// Implementation of document type validation logic
/// </summary>
public sealed class DocumentTypeValidator : IDocumentTypeValidator
{
    private const string SupportedTypesMessage = "Valid values: check, receipt";

    public ValidationResult ValidateDocumentType(string? documentTypeString)
    {
        if (string.IsNullOrEmpty(documentTypeString))
        {
            return ValidationResult.Invalid($"Document type is required. {SupportedTypesMessage}");
        }

        if (!Enum.TryParse<DocumentType>(documentTypeString, ignoreCase: true, out var documentType))
        {
            return ValidationResult.Invalid($"Invalid document type: {documentTypeString}. {SupportedTypesMessage}");
        }

        return ValidationResult.Valid(documentType);
    }
}