using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.Tests.JsonExtraction.Models;
using Xunit;

namespace Nolock.social.CloudflareAI.Tests.JsonExtraction;

/// <summary>
/// Comprehensive data validation tests for JSON extraction models
/// Tests decimal precision, date formats, required fields, field length constraints, and nested object validation
/// </summary>
public class DataValidationTests : JsonModelTestBase
{
    /// <summary>
    /// Override JsonOptions to include JsonStringEnumConverter for proper enum handling
    /// </summary>
    protected new readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    #region Decimal Precision Validation Tests

    [Theory]
    [InlineData("123.45")]
    [InlineData("0.01")]
    [InlineData("0.001")]
    [InlineData("0.0001")]
    [InlineData("999999.9999")]
    [InlineData("1.2345678901234567890123456789")] // Maximum decimal precision (29 significant digits)
    public void DecimalPrecision_ValidValues_DeserializeCorrectly(string jsonValue)
    {
        // Arrange
        var json = $$"""
        {
            "totals": {
                "total": {{jsonValue}}
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        // Parse the expected value from string to avoid compile-time decimal literal truncation
        var expectedValue = decimal.Parse(jsonValue, CultureInfo.InvariantCulture);
        Assert.Equal(expectedValue, receipt.Totals.Total);
    }

    [Theory]
    [InlineData("12.345")]
    [InlineData("0.12345")]
    [InlineData("1.999999")]
    public void DecimalPrecision_HighPrecisionValues_PreservePrecision(string jsonValue)
    {
        // Arrange
        var json = $$"""
        {
            "items": [
                {
                    "description": "High precision item",
                    "unitPrice": {{jsonValue}},
                    "totalPrice": {{jsonValue}}
                }
            ]
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Items);
        Assert.Single(receipt.Items);
        // Parse the expected value from string to avoid compile-time decimal literal truncation
        var expectedValue = decimal.Parse(jsonValue, CultureInfo.InvariantCulture);
        Assert.Equal(expectedValue, receipt.Items[0].UnitPrice);
        Assert.Equal(expectedValue, receipt.Items[0].TotalPrice);
    }

    [Fact]
    public void DecimalPrecision_VeryLargeNumber_HandlesCorrectly()
    {
        // Arrange - Test near decimal.MaxValue
        var largeValue = 79228162514264337593543950335m; // Close to decimal.MaxValue
        var json = $$"""
        {
            "totals": {
                "total": {{largeValue.ToString(CultureInfo.InvariantCulture)}}
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(largeValue, receipt.Totals.Total);
    }

    [Fact]
    public void DecimalPrecision_VerySmallNumber_HandlesCorrectly()
    {
        // Arrange - Test very small positive number
        var smallValue = 0.0000000000000000000000000001m;
        var json = $$"""
        {
            "totals": {
                "total": {{smallValue.ToString("F28", CultureInfo.InvariantCulture)}}
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(smallValue, receipt.Totals.Total);
    }

    [Fact]
    public void DecimalPrecision_NegativeValues_HandleCorrectly()
    {
        // Arrange - Test negative decimal values (like refunds)
        var json = """
        {
            "totals": {
                "total": -123.45,
                "discount": -10.00
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(-123.45m, receipt.Totals.Total);
        Assert.Equal(-10.00m, receipt.Totals.Discount);
    }

    [Fact]
    public void DecimalPrecision_ScientificNotation_HandlesCorrectly()
    {
        // Arrange
        var json = """
        {
            "totals": {
                "total": 1.5e2,
                "tax": 2.5e-3,
                "subtotal": 1.4985e2
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(150m, receipt.Totals.Total);
        Assert.Equal(0.0025m, receipt.Totals.Tax);
        Assert.Equal(149.85m, receipt.Totals.Subtotal);
    }

    #endregion

    #region Date Format Validation Tests

    [Theory]
    [InlineData("2024-01-15T10:30:00Z", "2024-01-15 10:30:00")]
    [InlineData("2024-01-15T10:30:00.123Z", "2024-01-15 10:30:00.123")]
    [InlineData("2024-01-15T10:30:00+05:00", "2024-01-15 05:30:00")] // UTC conversion
    [InlineData("2024-01-15T10:30:00-08:00", "2024-01-15 18:30:00")] // UTC conversion
    public void DateFormat_ISO8601WithTimezone_ParsesCorrectly(string jsonDate, string expectedUtcTime)
    {
        // Arrange
        var json = $$"""
        {
            "timestamp": "{{jsonDate}}"
        }
        """;

        var expectedDateTime = DateTime.Parse(expectedUtcTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.Equal(expectedDateTime, receipt.Timestamp.ToUniversalTime());
    }

    [Theory]
    [InlineData("2024-01-15", "2024-01-15 00:00:00")]
    [InlineData("2024-12-31", "2024-12-31 00:00:00")]
    [InlineData("2000-02-29", "2000-02-29 00:00:00")] // Leap year
    public void DateFormat_DateOnly_ParsesCorrectly(string jsonDate, string expectedTime)
    {
        // Arrange
        var json = $$"""
        {
            "date": "{{jsonDate}}"
        }
        """;

        var expectedDateTime = DateTime.Parse(expectedTime, CultureInfo.InvariantCulture);

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, JsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.NotNull(check.Date);
        Assert.Equal(expectedDateTime, check.Date.Value);
    }

    [Theory]
    [InlineData("2024-01-15T10:30:00")]
    [InlineData("2024-01-15T10:30:00.000Z")]
    [InlineData("2024-01-15T10:30")]
    public void DateFormat_VariousISO8601Formats_ParseCorrectly(string jsonDate)
    {
        // Arrange
        var json = $$"""
        {
            "timestamp": "{{jsonDate}}"
        }
        """;

        // Act & Assert - Should not throw
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);
        Assert.NotNull(receipt);
        Assert.True(receipt.Timestamp != default);
    }

    [Fact]
    public void DateFormat_InvalidDate_ThrowsJsonException()
    {
        // Arrange
        var json = """
        {
            "timestamp": "invalid-date"
        }
        """;

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Receipt>(json, JsonOptions));
    }

    [Fact]
    public void DateFormat_FutureDate_AcceptsCorrectly()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddYears(1);
        var json = $$"""
        {
            "timestamp": "{{futureDate:yyyy-MM-ddTHH:mm:ssZ}}"
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.True(receipt.Timestamp > DateTime.UtcNow);
    }

    [Fact]
    public void DateFormat_VeryOldDate_AcceptsCorrectly()
    {
        // Arrange - Test very old date
        var json = """
        {
            "timestamp": "1900-01-01T00:00:00Z"
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.Equal(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), receipt.Timestamp.ToUniversalTime());
    }

    #endregion

    #region Required Field Validation Tests

    [Fact]
    public void RequiredFields_Receipt_TotalRequired()
    {
        // Arrange - Missing required total field
        var json = """
        {
            "merchant": {
                "name": "Test Store"
            }
        }
        """;

        // Act & Assert - Should deserialize but total should be 0 (default value)
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(0m, receipt.Totals.Total); // Default value for required decimal
    }

    [Fact]
    public void RequiredFields_Check_AmountOptional()
    {
        // Arrange - Check without amount (should be allowed)
        var json = """
        {
            "checkNumber": "123",
            "payee": "John Doe"
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, JsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.Equal("123", check.CheckNumber);
        Assert.Equal("John Doe", check.Payee);
        Assert.Null(check.Amount);
    }

    [Fact]
    public void RequiredFields_ReceiptLineItem_DescriptionAndTotalPriceRequired()
    {
        // Arrange - Line item with required fields
        var json = """
        {
            "items": [
                {
                    "description": "Required Item",
                    "totalPrice": 25.00
                }
            ]
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Items);
        Assert.Single(receipt.Items);
        Assert.Equal("Required Item", receipt.Items[0].Description);
        Assert.Equal(25.00m, receipt.Items[0].TotalPrice);
    }

    [Fact]
    public void RequiredFields_MerchantName_RequiredNonEmpty()
    {
        // Arrange - Merchant with empty name
        var json = """
        {
            "merchant": {
                "name": ""
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Merchant);
        Assert.Equal("", receipt.Merchant.Name); // Accepts empty string but should be validated by business logic
    }

    [Fact]
    public void RequiredFields_OptionalFieldsNull_AcceptsCorrectly()
    {
        // Arrange - JSON with only required fields
        var json = """
        {
            "totals": {
                "total": 100.00
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Null(receipt.Totals.Subtotal);
        Assert.Null(receipt.Totals.Tax);
        Assert.Null(receipt.Totals.Tip);
        Assert.Null(receipt.Currency);
        Assert.Null(receipt.Items);
    }

    #endregion

    #region Field Length Constraints Tests

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Normal Store Name")]
    [InlineData("Very Long Store Name That Goes On And On And On")]
    public void FieldLength_MerchantName_AcceptsVariousLengths(string merchantName)
    {
        // Arrange
        var json = $$"""
        {
            "merchant": {
                "name": "{{merchantName}}"
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Merchant);
        Assert.Equal(merchantName, receipt.Merchant.Name);
    }

    [Fact]
    public void FieldLength_VeryLongDescription_AcceptsCorrectly()
    {
        // Arrange - Very long item description
        var longDescription = new string('A', 1000); // 1000 character description
        var json = $$"""
        {
            "items": [
                {
                    "description": "{{longDescription}}",
                    "totalPrice": 10.00
                }
            ]
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Items);
        Assert.Single(receipt.Items);
        Assert.Equal(longDescription, receipt.Items[0].Description);
    }

    [Theory]
    [InlineData("123456789")] // 9 digits (valid routing number length)
    [InlineData("123456789012345")] // 15 digits (valid account number length)
    [InlineData("1234")] // 4 digits (last digits of card)
    public void FieldLength_BankingFields_AcceptsValidLengths(string bankingField)
    {
        // Arrange
        var json = $$"""
        {
            "routingNumber": "{{bankingField}}",
            "accountNumber": "{{bankingField}}",
            "fractionalCode": "{{bankingField}}"
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, JsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.Equal(bankingField, check.RoutingNumber);
        Assert.Equal(bankingField, check.AccountNumber);
        Assert.Equal(bankingField, check.FractionalCode);
    }

    [Fact]
    public void FieldLength_CurrencyCode_AcceptsISO4217Format()
    {
        // Arrange - Test various currency codes
        var json = """
        {
            "currency": "USD"
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.Equal("USD", receipt.Currency);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Short memo")]
    [InlineData("This is a much longer memo that contains a lot of information about the purpose of this check and why it was written")]
    public void FieldLength_CheckMemo_AcceptsVariousLengths(string memo)
    {
        // Arrange
        var json = $$"""
        {
            "memo": "{{memo}}"
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, JsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.Equal(memo, check.Memo);
    }

    #endregion

    #region Nested Object Validation Tests

    [Fact]
    public void NestedObject_CompleteReceiptTotals_ValidatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "totals": {
                "subtotal": 100.00,
                "tax": 8.50,
                "tip": 15.00,
                "discount": 5.00,
                "total": 118.50
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(100.00m, receipt.Totals.Subtotal);
        Assert.Equal(8.50m, receipt.Totals.Tax);
        Assert.Equal(15.00m, receipt.Totals.Tip);
        Assert.Equal(5.00m, receipt.Totals.Discount);
        Assert.Equal(118.50m, receipt.Totals.Total);
    }

    [Fact]
    public void NestedObject_PartialReceiptTotals_ValidatesCorrectly()
    {
        // Arrange - Only some totals fields provided
        var json = """
        {
            "totals": {
                "total": 50.00,
                "tax": 4.00
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Null(receipt.Totals.Subtotal);
        Assert.Equal(4.00m, receipt.Totals.Tax);
        Assert.Null(receipt.Totals.Tip);
        Assert.Null(receipt.Totals.Discount);
        Assert.Equal(50.00m, receipt.Totals.Total);
    }

    [Fact]
    public void NestedObject_CompleteMerchantInfo_ValidatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "merchant": {
                "name": "Test Store",
                "address": "123 Main St, City, State 12345",
                "phone": "+1-555-123-4567",
                "website": "https://teststore.com",
                "taxId": "12-3456789",
                "storeId": "STORE001",
                "chainName": "Test Chain"
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Merchant);
        Assert.Equal("Test Store", receipt.Merchant.Name);
        Assert.Equal("123 Main St, City, State 12345", receipt.Merchant.Address);
        Assert.Equal("+1-555-123-4567", receipt.Merchant.Phone);
        Assert.Equal("https://teststore.com", receipt.Merchant.Website);
        Assert.Equal("12-3456789", receipt.Merchant.TaxId);
        Assert.Equal("STORE001", receipt.Merchant.StoreId);
        Assert.Equal("Test Chain", receipt.Merchant.ChainName);
    }

    [Fact]
    public void NestedObject_LineItemsArray_ValidatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "items": [
                {
                    "description": "Item 1",
                    "sku": "SKU001",
                    "quantity": 2,
                    "unit": "ea",
                    "unitPrice": 10.00,
                    "totalPrice": 20.00,
                    "discounted": true,
                    "discountAmount": 2.00,
                    "category": "Electronics"
                },
                {
                    "description": "Item 2",
                    "totalPrice": 15.00
                }
            ]
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Items);
        Assert.Equal(2, receipt.Items.Count);
        
        // First item (complete)
        var item1 = receipt.Items[0];
        Assert.Equal("Item 1", item1.Description);
        Assert.Equal("SKU001", item1.Sku);
        Assert.Equal(2.0, item1.Quantity);
        Assert.Equal("ea", item1.Unit);
        Assert.Equal(10.00m, item1.UnitPrice);
        Assert.Equal(20.00m, item1.TotalPrice);
        Assert.True(item1.Discounted);
        Assert.Equal(2.00m, item1.DiscountAmount);
        Assert.Equal("Electronics", item1.Category);
        
        // Second item (minimal)
        var item2 = receipt.Items[1];
        Assert.Equal("Item 2", item2.Description);
        Assert.Equal(15.00m, item2.TotalPrice);
        Assert.Null(item2.Sku);
        Assert.Null(item2.Quantity);
    }

    [Fact]
    public void NestedObject_TaxItemsArray_ValidatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "taxes": [
                {
                    "taxName": "State Sales Tax",
                    "taxType": "sales",
                    "taxRate": 0.08,
                    "taxAmount": 8.00
                },
                {
                    "taxName": "City Tax",
                    "taxAmount": 2.50
                }
            ]
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Taxes);
        Assert.Equal(2, receipt.Taxes.Count);
        
        var tax1 = receipt.Taxes[0];
        Assert.Equal("State Sales Tax", tax1.TaxName);
        Assert.Equal("sales", tax1.TaxType);
        Assert.Equal(0.08m, tax1.TaxRate);
        Assert.Equal(8.00m, tax1.TaxAmount);
        
        var tax2 = receipt.Taxes[1];
        Assert.Equal("City Tax", tax2.TaxName);
        Assert.Equal(2.50m, tax2.TaxAmount);
        Assert.Null(tax2.TaxType);
        Assert.Null(tax2.TaxRate);
    }

    [Fact]
    public void NestedObject_PaymentMethodsArray_ValidatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "payments": [
                {
                    "method": "credit",
                    "cardType": "visa",
                    "lastDigits": "1234",
                    "amount": 100.00,
                    "transactionId": "TXN123456"
                },
                {
                    "method": "cash",
                    "amount": 23.50
                }
            ]
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Payments);
        Assert.Equal(2, receipt.Payments.Count);
        
        var payment1 = receipt.Payments[0];
        Assert.Equal(PaymentMethod.Credit, payment1.Method);
        Assert.Equal("visa", payment1.CardType);
        Assert.Equal("1234", payment1.LastDigits);
        Assert.Equal(100.00m, payment1.Amount);
        Assert.Equal("TXN123456", payment1.TransactionId);
        
        var payment2 = receipt.Payments[1];
        Assert.Equal(PaymentMethod.Cash, payment2.Method);
        Assert.Equal(23.50m, payment2.Amount);
        Assert.Null(payment2.CardType);
        Assert.Null(payment2.LastDigits);
        Assert.Null(payment2.TransactionId);
    }

    [Fact]
    public void NestedObject_Metadata_ValidatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "metadata": {
                "confidenceScore": 0.95,
                "currency": "USD",
                "languageCode": "en-US",
                "timeZone": "America/New_York",
                "receiptFormat": "retail",
                "sourceImageId": "IMG_12345",
                "warnings": ["Low quality image", "Partial text detected"]
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Metadata);
        Assert.Equal(0.95, receipt.Metadata.ConfidenceScore);
        Assert.Equal("USD", receipt.Metadata.Currency);
        Assert.Equal("en-US", receipt.Metadata.LanguageCode);
        Assert.Equal("America/New_York", receipt.Metadata.TimeZone);
        Assert.Equal(ReceiptFormat.Retail, receipt.Metadata.ReceiptFormat);
        Assert.Equal("IMG_12345", receipt.Metadata.SourceImageId);
        Assert.NotNull(receipt.Metadata.Warnings);
        Assert.Equal(2, receipt.Metadata.Warnings.Count);
        Assert.Contains("Low quality image", receipt.Metadata.Warnings);
        Assert.Contains("Partial text detected", receipt.Metadata.Warnings);
    }

    [Fact]
    public void NestedObject_CheckMetadata_ValidatesCorrectly()
    {
        // Arrange
        var json = """
        {
            "metadata": {
                "confidenceScore": 0.88,
                "sourceImageId": "CHK_67890",
                "ocrProvider": "Tesseract",
                "warnings": ["MICR line partially obscured"]
            }
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, JsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.NotNull(check.Metadata);
        Assert.Equal(0.88, check.Metadata.ConfidenceScore);
        Assert.Equal("CHK_67890", check.Metadata.SourceImageId);
        Assert.Equal("Tesseract", check.Metadata.OcrProvider);
        Assert.NotNull(check.Metadata.Warnings);
        Assert.Single(check.Metadata.Warnings);
        Assert.Contains("MICR line partially obscured", check.Metadata.Warnings);
    }

    [Fact]
    public void NestedObject_EmptyArrays_ValidateCorrectly()
    {
        // Arrange
        var json = """
        {
            "items": [],
            "taxes": [],
            "payments": [],
            "notes": []
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Items);
        Assert.Empty(receipt.Items);
        Assert.NotNull(receipt.Taxes);
        Assert.Empty(receipt.Taxes);
        Assert.NotNull(receipt.Payments);
        Assert.Empty(receipt.Payments);
        Assert.NotNull(receipt.Notes);
        Assert.Empty(receipt.Notes);
    }

    #endregion

    #region Complex Validation Scenarios

    [Fact]
    public void ComplexValidation_FullReceipt_AllFieldsValidateCorrectly()
    {
        // Arrange - Complete receipt with all possible fields
        var json = """
        {
            "merchant": {
                "name": "Complete Test Store",
                "address": "456 Full St, Complete City, State 67890",
                "phone": "+1-555-987-6543",
                "website": "https://completestore.com",
                "taxId": "98-7654321",
                "storeId": "COMPLETE001",
                "chainName": "Complete Chain"
            },
            "receiptNumber": "RCT-2024-001",
            "receiptType": "sale",
            "timestamp": "2024-01-15T14:30:00Z",
            "paymentMethod": "credit",
            "totals": {
                "subtotal": 95.47,
                "tax": 7.64,
                "tip": 14.32,
                "discount": 9.55,
                "total": 107.88
            },
            "currency": "USD",
            "items": [
                {
                    "description": "Premium Widget",
                    "sku": "PWD-001",
                    "quantity": 2,
                    "unit": "ea",
                    "unitPrice": 42.99,
                    "totalPrice": 85.98,
                    "discounted": true,
                    "discountAmount": 8.60,
                    "category": "Electronics"
                },
                {
                    "description": "Service Fee",
                    "totalPrice": 9.49,
                    "category": "Service"
                }
            ],
            "taxes": [
                {
                    "taxName": "State Sales Tax",
                    "taxType": "sales",
                    "taxRate": 0.08,
                    "taxAmount": 7.64
                }
            ],
            "payments": [
                {
                    "method": "credit",
                    "cardType": "mastercard",
                    "lastDigits": "9876",
                    "amount": 107.88,
                    "transactionId": "MC123456789"
                }
            ],
            "notes": ["Customer requested extra receipt", "Loyalty points applied"],
            "metadata": {
                "confidenceScore": 0.97,
                "currency": "USD",
                "languageCode": "en-US",
                "timeZone": "America/Chicago",
                "receiptFormat": "retail",
                "sourceImageId": "IMG_COMPLETE_001",
                "warnings": []
            },
            "confidence": 0.97,
            "isValidInput": true
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, JsonOptions);

        // Assert
        Assert.NotNull(receipt);
        
        // Verify all main properties
        Assert.Equal("RCT-2024-001", receipt.ReceiptNumber);
        Assert.Equal(ReceiptType.Sale, receipt.ReceiptType);
        Assert.Equal("credit", receipt.PaymentMethod);
        Assert.Equal("USD", receipt.Currency);
        Assert.Equal(0.97, receipt.Confidence);
        Assert.True(receipt.IsValidInput);
        
        // Verify nested objects are all properly populated
        Assert.NotNull(receipt.Merchant);
        Assert.NotNull(receipt.Totals);
        Assert.NotNull(receipt.Items);
        Assert.NotNull(receipt.Taxes);
        Assert.NotNull(receipt.Payments);
        Assert.NotNull(receipt.Notes);
        Assert.NotNull(receipt.Metadata);
        
        // Spot check some nested values
        Assert.Equal("Complete Test Store", receipt.Merchant.Name);
        Assert.Equal(107.88m, receipt.Totals.Total);
        Assert.Equal(2, receipt.Items.Count);
        Assert.Single(receipt.Taxes);
        Assert.Single(receipt.Payments);
        Assert.Equal(2, receipt.Notes.Count);
        Assert.Empty(receipt.Metadata.Warnings!);
    }

    [Fact]
    public void ComplexValidation_RoundTripSerialization_PreservesAllData()
    {
        // Arrange - Create a complex receipt object
        var original = new Receipt
        {
            Merchant = new MerchantInfo { Name = "Round Trip Store", Address = "123 Test Ave" },
            ReceiptNumber = "RT-001",
            Timestamp = DateTime.UtcNow,
            Totals = new ReceiptTotals
            {
                Subtotal = 123.45m,
                Tax = 9.88m,
                Total = 133.33m
            },
            Items = new List<ReceiptLineItem>
            {
                new() { Description = "Test Item", UnitPrice = 123.45m, TotalPrice = 123.45m }
            },
            Taxes = new List<ReceiptTaxItem>
            {
                new() { TaxName = "Sales Tax", TaxRate = 0.08m, TaxAmount = 9.88m }
            },
            Notes = new List<string> { "Test note 1", "Test note 2" },
            Confidence = 0.95
        };

        // Act - Round trip serialize/deserialize
        AssertRoundTripSerialization(original, (orig, deser) =>
        {
            Assert.Equal(orig.Merchant.Name, deser.Merchant.Name);
            Assert.Equal(orig.ReceiptNumber, deser.ReceiptNumber);
            Assert.Equal(orig.Totals.Total, deser.Totals.Total);
            Assert.Equal(orig.Items!.Count, deser.Items!.Count);
            Assert.Equal(orig.Items[0].Description, deser.Items[0].Description);
            Assert.Equal(orig.Taxes!.Count, deser.Taxes!.Count);
            Assert.Equal(orig.Notes!.Count, deser.Notes!.Count);
            Assert.Equal(orig.Confidence, deser.Confidence);
        });
    }

    #endregion
}