namespace Nolock.social.OCRservices.Configuration;

/// <summary>
/// Configuration constants for OCR processing
/// </summary>
public static class OcrConfiguration
{
    /// <summary>
    /// Default maximum number of concurrent OCR operations
    /// </summary>
    public const int DefaultMaxConcurrency = 4;

    /// <summary>
    /// Default number of retry attempts for failed operations
    /// </summary>
    public const int DefaultRetryCount = 3;

    /// <summary>
    /// Default delay between retry attempts in seconds
    /// </summary>
    public const int DefaultRetryDelaySeconds = 1;

    /// <summary>
    /// Default maximum tokens for OCR processing
    /// </summary>
    public const int DefaultMaxTokens = 1000;

    /// <summary>
    /// Default temperature for OCR model
    /// </summary>
    public const double DefaultTemperature = 0.1;

    /// <summary>
    /// Default confidence score for simple model extractions
    /// </summary>
    public const double DefaultSimpleModelConfidence = 0.8;

    /// <summary>
    /// Maximum allowed image size in bytes (10MB)
    /// </summary>
    public const int MaxImageSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Timeout for OCR processing in seconds
    /// </summary>
    public const int ProcessingTimeoutSeconds = 60;
}