using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Nolock.social.CloudflareAI.JsonExtraction.Models;

/// <summary>
/// Type of check
/// </summary>
public enum CheckType
{
    [JsonPropertyName("personal")]
    Personal,
    
    [JsonPropertyName("business")]
    Business,
    
    [JsonPropertyName("cashier")]
    Cashier,
    
    [JsonPropertyName("certified")]
    Certified,
    
    [JsonPropertyName("traveler")]
    Traveler,
    
    [JsonPropertyName("government")]
    Government,
    
    [JsonPropertyName("payroll")]
    Payroll,
    
    [JsonPropertyName("money_order")]
    MoneyOrder,
    
    [JsonPropertyName("other")]
    Other
}

/// <summary>
/// Type of bank account
/// </summary>
public enum BankAccountType
{
    [JsonPropertyName("checking")]
    Checking,
    
    [JsonPropertyName("savings")]
    Savings,
    
    [JsonPropertyName("money_market")]
    MoneyMarket,
    
    [JsonPropertyName("other")]
    Other
}

/// <summary>
/// Additional metadata about the check extraction
/// </summary>
public class CheckMetadata
{
    [JsonPropertyName("confidenceScore")]
    [Description("Overall confidence of extraction (0-1)")]
    public double ConfidenceScore { get; set; }
    
    [JsonPropertyName("sourceImageId")]
    [Description("Reference to the source image")]
    public string? SourceImageId { get; set; }
    
    [JsonPropertyName("ocrProvider")]
    [Description("Provider used for OCR")]
    public string? OcrProvider { get; set; }
    
    [JsonPropertyName("warnings")]
    [Description("List of warning messages")]
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// Check data extracted from images
/// </summary>
[Description("Schema for check data extracted from images")]
public class Check
{
    [JsonPropertyName("isValidInput")]
    [Description("Indicates if the input appears to be a valid check image")]
    public bool? IsValidInput { get; set; }
    
    [JsonPropertyName("checkNumber")]
    [Description("Check number or identifier")]
    public string? CheckNumber { get; set; }
    
    [JsonPropertyName("date")]
    [Description("Date on the check (ISO 8601 format)")]
    public DateTime? Date { get; set; }
    
    [JsonPropertyName("payee")]
    [Description("Person or entity to whom the check is payable")]
    public string? Payee { get; set; }
    
    [JsonPropertyName("payer")]
    [Description("Person or entity who wrote/signed the check")]
    public string? Payer { get; set; }
    
    [JsonPropertyName("amount")]
    [Description("Dollar amount of the check as a string to preserve exact decimal representation")]
    public string? Amount { get; set; }
    
    [JsonPropertyName("memo")]
    [Description("Memo or note on the check")]
    public string? Memo { get; set; }
    
    [JsonPropertyName("bankName")]
    [Description("Name of the bank issuing the check")]
    public string? BankName { get; set; }
    
    [JsonPropertyName("routingNumber")]
    [Description("Bank routing number (9 digits)")]
    public string? RoutingNumber { get; set; }
    
    [JsonPropertyName("accountNumber")]
    [Description("Bank account number")]
    public string? AccountNumber { get; set; }
    
    [JsonPropertyName("checkType")]
    [Description("Type of check (e.g., 'personal', 'business')")]
    public string? CheckType { get; set; }
    
    [JsonPropertyName("accountType")]
    [Description("Type of account (e.g., 'checking', 'savings')")]
    public string? AccountType { get; set; }
    
    [JsonPropertyName("signature")]
    [Description("Whether the check appears to be signed")]
    public bool? Signature { get; set; }
    
    [JsonPropertyName("signatureText")]
    [Description("Text of the signature if readable")]
    public string? SignatureText { get; set; }
    
    [JsonPropertyName("fractionalCode")]
    [Description("Fractional code on the check (alternative routing identifier)")]
    public string? FractionalCode { get; set; }
    
    [JsonPropertyName("micrLine")]
    [Description("Full MICR (Magnetic Ink Character Recognition) line on the bottom of check")]
    public string? MicrLine { get; set; }
    
    [JsonPropertyName("metadata")]
    [Description("Additional information about the extraction")]
    public CheckMetadata? Metadata { get; set; }
    
    [JsonPropertyName("confidence")]
    [Description("Overall confidence score (0-1)")]
    public double Confidence { get; set; }
}

/// <summary>
/// Simplified check model focusing on essential fields
/// </summary>
[Description("Essential check information")]
public class SimpleCheck
{
    [JsonPropertyName("check_number")]
    [Description("Check number")]
    public string? CheckNumber { get; set; }
    
    [JsonPropertyName("date")]
    [Description("Date on the check")]
    public string? Date { get; set; }
    
    [JsonPropertyName("payee")]
    [Description("Who the check is made out to")]
    public string? Payee { get; set; }
    
    [JsonPropertyName("amount")]
    [Description("Check amount in dollars")]
    public string? Amount { get; set; }
    
    [JsonPropertyName("bank_name")]
    [Description("Name of the bank")]
    public string? BankName { get; set; }
    
    [JsonPropertyName("is_signed")]
    [Description("Whether the check is signed")]
    public bool? IsSigned { get; set; }
}