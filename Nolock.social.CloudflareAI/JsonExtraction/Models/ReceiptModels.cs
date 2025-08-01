using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Nolock.social.CloudflareAI.JsonExtraction.Models;

/// <summary>
/// Type of receipt
/// </summary>
public enum ReceiptType
{
    [JsonPropertyName("sale")]
    Sale,
    
    [JsonPropertyName("return")]
    Return,
    
    [JsonPropertyName("refund")]
    Refund,
    
    [JsonPropertyName("estimate")]
    Estimate,
    
    [JsonPropertyName("proforma")]
    Proforma,
    
    [JsonPropertyName("other")]
    Other
}

/// <summary>
/// Payment method used
/// </summary>
public enum PaymentMethod
{
    [JsonPropertyName("credit")]
    Credit,
    
    [JsonPropertyName("debit")]
    Debit,
    
    [JsonPropertyName("cash")]
    Cash,
    
    [JsonPropertyName("check")]
    Check,
    
    [JsonPropertyName("gift_card")]
    GiftCard,
    
    [JsonPropertyName("store_credit")]
    StoreCredit,
    
    [JsonPropertyName("mobile_payment")]
    MobilePayment,
    
    [JsonPropertyName("other")]
    Other
}

/// <summary>
/// Type of card used for payment
/// </summary>
public enum CardType
{
    [JsonPropertyName("visa")]
    Visa,
    
    [JsonPropertyName("mastercard")]
    Mastercard,
    
    [JsonPropertyName("amex")]
    Amex,
    
    [JsonPropertyName("discover")]
    Discover,
    
    [JsonPropertyName("diners_club")]
    DinersClub,
    
    [JsonPropertyName("jcb")]
    JCB,
    
    [JsonPropertyName("union_pay")]
    UnionPay,
    
    [JsonPropertyName("other")]
    Other
}

/// <summary>
/// Type of tax applied
/// </summary>
public enum TaxType
{
    [JsonPropertyName("sales")]
    Sales,
    
    [JsonPropertyName("vat")]
    VAT,
    
    [JsonPropertyName("gst")]
    GST,
    
    [JsonPropertyName("pst")]
    PST,
    
    [JsonPropertyName("hst")]
    HST,
    
    [JsonPropertyName("excise")]
    Excise,
    
    [JsonPropertyName("service")]
    Service,
    
    [JsonPropertyName("other")]
    Other
}

/// <summary>
/// Receipt format type
/// </summary>
public enum ReceiptFormat
{
    [JsonPropertyName("retail")]
    Retail,
    
    [JsonPropertyName("restaurant")]
    Restaurant,
    
    [JsonPropertyName("service")]
    Service,
    
    [JsonPropertyName("utility")]
    Utility,
    
    [JsonPropertyName("transportation")]
    Transportation,
    
    [JsonPropertyName("accommodation")]
    Accommodation,
    
    [JsonPropertyName("other")]
    Other
}

/// <summary>
/// Unit of measurement for items
/// </summary>
public enum UnitOfMeasure
{
    [JsonPropertyName("ea")]
    Each,
    
    [JsonPropertyName("kg")]
    Kilogram,
    
    [JsonPropertyName("g")]
    Gram,
    
    [JsonPropertyName("lb")]
    Pound,
    
    [JsonPropertyName("oz")]
    Ounce,
    
    [JsonPropertyName("l")]
    Liter,
    
    [JsonPropertyName("ml")]
    Milliliter,
    
    [JsonPropertyName("gal")]
    Gallon,
    
    [JsonPropertyName("pc")]
    Piece,
    
    [JsonPropertyName("pr")]
    Pair,
    
    [JsonPropertyName("pk")]
    Pack,
    
    [JsonPropertyName("box")]
    Box,
    
    [JsonPropertyName("other")]
    Other
}

/// <summary>
/// Merchant information
/// </summary>
public class MerchantInfo
{
    [JsonPropertyName("name")]
    [Description("Name of the merchant or store")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("address")]
    [Description("Physical address of the merchant")]
    public string? Address { get; set; }
    
    [JsonPropertyName("phone")]
    [Description("Contact phone number")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("website")]
    [Description("Website URL")]
    public string? Website { get; set; }
    
    [JsonPropertyName("taxId")]
    [Description("Tax identification number (VAT/GST ID)")]
    public string? TaxId { get; set; }
    
    [JsonPropertyName("storeId")]
    [Description("Store or branch identifier")]
    public string? StoreId { get; set; }
    
    [JsonPropertyName("chainName")]
    [Description("Name of the store chain if applicable")]
    public string? ChainName { get; set; }
}

/// <summary>
/// Financial totals from the receipt
/// </summary>
public class ReceiptTotals
{
    [JsonPropertyName("subtotal")]
    [Description("Pre-tax total amount")]
    public decimal? Subtotal { get; set; }
    
    [JsonPropertyName("tax")]
    [Description("Total tax amount")]
    public decimal? Tax { get; set; }
    
    [JsonPropertyName("tip")]
    [Description("Tip/gratuity amount")]
    public decimal? Tip { get; set; }
    
    [JsonPropertyName("discount")]
    [Description("Total discount amount")]
    public decimal? Discount { get; set; }
    
    [JsonPropertyName("total")]
    [Description("Final total amount including tax, tip, and adjusting for discounts")]
    public decimal Total { get; set; }
}

/// <summary>
/// Line item on the receipt
/// </summary>
public class ReceiptLineItem
{
    [JsonPropertyName("description")]
    [Description("Item description or name")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("sku")]
    [Description("Stock keeping unit or product code")]
    public string? Sku { get; set; }
    
    [JsonPropertyName("quantity")]
    [Description("Quantity purchased")]
    public double? Quantity { get; set; }
    
    [JsonPropertyName("unit")]
    [Description("Unit of measurement - can be enum or string for flexibility")]
    public string? Unit { get; set; }
    
    [JsonPropertyName("unitPrice")]
    [Description("Price per unit - converted from string to decimal for C# type safety")]
    public decimal? UnitPrice { get; set; }
    
    [JsonPropertyName("totalPrice")]
    [Description("Total price for this line item - converted from string to decimal for C# type safety")]
    public decimal TotalPrice { get; set; }
    
    [JsonPropertyName("discounted")]
    [Description("Whether the item was discounted")]
    public bool? Discounted { get; set; }
    
    [JsonPropertyName("discountAmount")]
    [Description("Amount of discount applied - converted from string to decimal for C# type safety")]
    public decimal? DiscountAmount { get; set; }
    
    [JsonPropertyName("category")]
    [Description("Product category")]
    public string? Category { get; set; }
}

/// <summary>
/// Tax breakdown item
/// </summary>
public class ReceiptTaxItem
{
    [JsonPropertyName("taxName")]
    [Description("Name of tax (e.g., 'VAT', 'GST', 'Sales Tax')")]
    public string TaxName { get; set; } = "";
    
    [JsonPropertyName("taxType")]
    [Description("Type of tax - can be enum or string for flexibility")]
    public string? TaxType { get; set; }
    
    [JsonPropertyName("taxRate")]
    [Description("Tax rate as decimal (e.g., 0.07 for 7%) - converted from string to decimal for C# type safety")]
    public decimal? TaxRate { get; set; }
    
    [JsonPropertyName("taxAmount")]
    [Description("Tax amount - converted from string to decimal for C# type safety")]
    public decimal? TaxAmount { get; set; }
}

/// <summary>
/// Payment method details
/// </summary>
public class ReceiptPaymentMethod
{
    [JsonPropertyName("method")]
    [Description("Payment method")]
    public PaymentMethod Method { get; set; }
    
    [JsonPropertyName("cardType")]
    [Description("Type of card - can be enum or string for flexibility")]
    public string? CardType { get; set; }
    
    [JsonPropertyName("lastDigits")]
    [Description("Last 4 digits of payment card")]
    public string? LastDigits { get; set; }
    
    [JsonPropertyName("amount")]
    [Description("Amount paid with this method - converted from string to decimal for C# type safety")]
    public decimal Amount { get; set; }
    
    [JsonPropertyName("transactionId")]
    [Description("Payment transaction ID")]
    public string? TransactionId { get; set; }
}

/// <summary>
/// Receipt metadata
/// </summary>
public sealed class ReceiptMetadata
{
    [JsonPropertyName("confidenceScore")]
    [Description("Confidence score of the extraction")]
    public double ConfidenceScore { get; set; }
    
    [JsonPropertyName("currency")]
    [Description("ISO currency code detected")]
    public string? Currency { get; set; }
    
    [JsonPropertyName("languageCode")]
    [Description("ISO language code of the receipt")]
    public string? LanguageCode { get; set; }
    
    [JsonPropertyName("timeZone")]
    [Description("Time zone identifier")]
    public string? TimeZone { get; set; }
    
    [JsonPropertyName("receiptFormat")]
    [Description("Format type of the receipt")]
    public ReceiptFormat? ReceiptFormat { get; set; }
    
    [JsonPropertyName("sourceImageId")]
    [Description("Identifier of the source image")]
    public string? SourceImageId { get; set; }
    
    [JsonPropertyName("warnings")]
    [Description("List of warnings from the extraction process")]
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// Base interface for receipt-like documents
/// </summary>
public class ReceiptBase
{
    [JsonPropertyName("merchant")]
    [Description("Information about the merchant")]
    public MerchantInfo Merchant { get; set; } = new();
    
    [JsonPropertyName("receiptNumber")]
    [Description("Receipt or invoice number")]
    public string? ReceiptNumber { get; set; }
    
    [JsonPropertyName("receiptType")]
    [Description("Type of receipt")]
    public ReceiptType? ReceiptType { get; set; }
    
    [JsonPropertyName("timestamp")]
    [Description("Date and time of transaction (ISO 8601 format when transmitted as string)")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("paymentMethod")]
    [Description("Method of payment - can be enum or string for flexibility")]
    public string? PaymentMethod { get; set; }
}

/// <summary>
/// Receipt data extracted from images
/// </summary>
[Description("Schema for receipt data extracted from images")]
public sealed class Receipt : ReceiptBase
{
    [JsonPropertyName("totals")]
    [Description("Financial totals from the receipt")]
    public ReceiptTotals Totals { get; set; } = new();
    
    [JsonPropertyName("currency")]
    [Description("ISO 4217 currency code (3-letter codes like USD, EUR, GBP)")]
    public string? Currency { get; set; }
    
    [JsonPropertyName("items")]
    [Description("List of line items on the receipt")]
    public List<ReceiptLineItem>? Items { get; set; }
    
    [JsonPropertyName("taxes")]
    [Description("Breakdown of taxes")]
    public List<ReceiptTaxItem>? Taxes { get; set; }
    
    [JsonPropertyName("payments")]
    [Description("Details about payment methods used")]
    public List<ReceiptPaymentMethod>? Payments { get; set; }
    
    [JsonPropertyName("notes")]
    [Description("Additional notes or comments")]
    public List<string>? Notes { get; set; }
    
    [JsonPropertyName("metadata")]
    [Description("Additional information about the extraction")]
    public ReceiptMetadata? Metadata { get; set; }
    
    [JsonPropertyName("confidence")]
    [Description("Overall confidence score of the extraction")]
    public double Confidence { get; set; }
    
    [JsonPropertyName("isValidInput")]
    [Description("Indicates if the input appears to be a valid receipt image. False if the system has detected potential hallucinations")]
    public bool? IsValidInput { get; set; }
}

/// <summary>
/// Simplified receipt model focusing on essential fields - flexible for various document types
/// </summary>
[Description("Essential receipt information that can be extracted from any transactional document including receipts, invoices, reservations, and rental confirmations")]
public class SimpleReceipt
{
    [JsonPropertyName("merchant_name")]
    [Description("Name of the business, store, merchant, or service provider")]
    public string? MerchantName { get; set; }
    
    [JsonPropertyName("date")]
    [Description("Date of the transaction, reservation, or service")]
    public string? Date { get; set; }
    
    [JsonPropertyName("total_amount")]
    [Description("Total amount paid, charged, or due - extract any monetary amount mentioned")]
    public decimal? TotalAmount { get; set; }
    
    [JsonPropertyName("tax_amount")]
    [Description("Tax amount if mentioned, or any additional fees")]
    public decimal? TaxAmount { get; set; }
    
    [JsonPropertyName("payment_method")]
    [Description("How payment was made, will be made, or any payment-related information")]
    public string? PaymentMethod { get; set; }
    
    [JsonPropertyName("items_count")]
    [Description("Number of items, services, or products mentioned in the document")]
    public int? ItemsCount { get; set; }
}