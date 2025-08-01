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
    [Description("Pre-tax total amount as a string to preserve exact decimal representation")]
    public string? Subtotal { get; set; }
    
    [JsonPropertyName("tax")]
    [Description("Total tax amount as a string to preserve exact decimal representation")]
    public string? Tax { get; set; }
    
    [JsonPropertyName("tip")]
    [Description("Tip/gratuity amount as a string to preserve exact decimal representation")]
    public string? Tip { get; set; }
    
    [JsonPropertyName("discount")]
    [Description("Total discount amount as a string to preserve exact decimal representation")]
    public string? Discount { get; set; }
    
    [JsonPropertyName("total")]
    [Description("Final total amount including tax, tip, and adjusting for discounts as a string to preserve exact decimal representation")]
    public string Total { get; set; } = "";
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
    [Description("Unit of measurement (e.g., 'ea', 'kg')")]
    public string? Unit { get; set; }
    
    [JsonPropertyName("unitPrice")]
    [Description("Price per unit as a string to preserve exact decimal representation")]
    public string? UnitPrice { get; set; }
    
    [JsonPropertyName("totalPrice")]
    [Description("Total price for this line item as a string to preserve exact decimal representation")]
    public string TotalPrice { get; set; } = "";
    
    [JsonPropertyName("discounted")]
    [Description("Whether the item was discounted")]
    public bool? Discounted { get; set; }
    
    [JsonPropertyName("discountAmount")]
    [Description("Amount of discount applied as a string to preserve exact decimal representation")]
    public string? DiscountAmount { get; set; }
    
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
    [Description("Type of tax")]
    public string? TaxType { get; set; }
    
    [JsonPropertyName("taxRate")]
    [Description("Tax rate as string representation of decimal (e.g., '0.1' for 10%)")]
    public string? TaxRate { get; set; }
    
    [JsonPropertyName("taxAmount")]
    [Description("Tax amount as a string to preserve exact decimal representation")]
    public string TaxAmount { get; set; } = "";
}

/// <summary>
/// Payment method details
/// </summary>
public class ReceiptPaymentMethod
{
    [JsonPropertyName("method")]
    [Description("Payment method (e.g., 'credit', 'cash')")]
    public string Method { get; set; } = "";
    
    [JsonPropertyName("cardType")]
    [Description("Type of card (e.g., 'Visa', 'Mastercard')")]
    public string? CardType { get; set; }
    
    [JsonPropertyName("lastDigits")]
    [Description("Last 4 digits of payment card")]
    public string? LastDigits { get; set; }
    
    [JsonPropertyName("amount")]
    [Description("Amount paid with this method as a string to preserve exact decimal representation")]
    public string Amount { get; set; } = "";
    
    [JsonPropertyName("transactionId")]
    [Description("Payment transaction ID")]
    public string? TransactionId { get; set; }
}

/// <summary>
/// Receipt metadata
/// </summary>
public class ReceiptMetadata
{
    [JsonPropertyName("confidenceScore")]
    [Description("Overall confidence of extraction (0-1)")]
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
    [Description("Format type (e.g., 'retail', 'restaurant')")]
    public string? ReceiptFormat { get; set; }
    
    [JsonPropertyName("sourceImageId")]
    [Description("Reference to the source image")]
    public string? SourceImageId { get; set; }
    
    [JsonPropertyName("warnings")]
    [Description("List of warning messages")]
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// Receipt data extracted from images
/// </summary>
[Description("Schema for receipt data extracted from images")]
public class Receipt
{
    [JsonPropertyName("isValidInput")]
    [Description("Indicates if the input appears to be a valid receipt image")]
    public bool? IsValidInput { get; set; }
    
    [JsonPropertyName("merchant")]
    [Description("Information about the merchant")]
    public MerchantInfo Merchant { get; set; } = new();
    
    [JsonPropertyName("receiptNumber")]
    [Description("Receipt or invoice number")]
    public string? ReceiptNumber { get; set; }
    
    [JsonPropertyName("receiptType")]
    [Description("Type of receipt (e.g., 'sale', 'return', 'refund')")]
    public string? ReceiptType { get; set; }
    
    [JsonPropertyName("timestamp")]
    [Description("Date and time of transaction (ISO 8601 format)")]
    public DateTime? Timestamp { get; set; }
    
    [JsonPropertyName("paymentMethod")]
    [Description("Method of payment (e.g., 'cash', 'credit', 'debit')")]
    public string? PaymentMethod { get; set; }
    
    [JsonPropertyName("totals")]
    [Description("Financial totals from the receipt")]
    public ReceiptTotals Totals { get; set; } = new();
    
    [JsonPropertyName("currency")]
    [Description("3-letter ISO currency code")]
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
    [Description("Overall confidence score (0-1)")]
    public double Confidence { get; set; }
}

/// <summary>
/// Simplified receipt model focusing on essential fields
/// </summary>
[Description("Essential receipt information")]
public class SimpleReceipt
{
    [JsonPropertyName("merchant_name")]
    [Description("Name of the store or merchant")]
    public string? MerchantName { get; set; }
    
    [JsonPropertyName("date")]
    [Description("Date of the transaction")]
    public string? Date { get; set; }
    
    [JsonPropertyName("total_amount")]
    [Description("Total amount paid")]
    public string? TotalAmount { get; set; }
    
    [JsonPropertyName("tax_amount")]
    [Description("Tax amount")]
    public string? TaxAmount { get; set; }
    
    [JsonPropertyName("payment_method")]
    [Description("How the payment was made")]
    public string? PaymentMethod { get; set; }
    
    [JsonPropertyName("items_count")]
    [Description("Number of items purchased")]
    public int? ItemsCount { get; set; }
}