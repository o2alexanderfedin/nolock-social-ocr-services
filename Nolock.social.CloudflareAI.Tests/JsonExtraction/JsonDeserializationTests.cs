using System.Text.Json;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using System.Collections.Generic;
using Xunit;

namespace Nolock.social.CloudflareAI.Tests.JsonExtraction;

public class JsonDeserializationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void DeserializeReceipt_WithMixedNumericFormats_HandlesAllCorrectly()
    {
        // Arrange - JSON with various numeric formats
        var json = """
        {
            "merchant": {
                "name": "Test Store"
            },
            "totals": {
                "subtotal": 100,
                "tax": 8.5,
                "tip": 15.00,
                "discount": 0,
                "total": 123.50
            },
            "items": [
                {
                    "description": "Item with integer price",
                    "quantity": 1,
                    "unitPrice": 50,
                    "totalPrice": 50
                },
                {
                    "description": "Item with decimal price",
                    "quantity": 2,
                    "unitPrice": 25.25,
                    "totalPrice": 50.50
                }
            ]
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        
        // Integer values should be converted to decimal
        Assert.Equal(100m, receipt.Totals.Subtotal);
        Assert.Equal(0m, receipt.Totals.Discount);
        
        // Decimal values should be preserved
        Assert.Equal(8.5m, receipt.Totals.Tax);
        Assert.Equal(15.00m, receipt.Totals.Tip);
        Assert.Equal(123.50m, receipt.Totals.Total);
        
        // Check items
        Assert.NotNull(receipt.Items);
        Assert.Equal(2, receipt.Items.Count);
        Assert.Equal(50m, receipt.Items[0].TotalPrice);
        Assert.Equal(50.50m, receipt.Items[1].TotalPrice);
    }

    [Fact]
    public void DeserializeReceipt_WithScientificNotation_HandlesCorrectly()
    {
        // Arrange - JSON with scientific notation
        var json = """
        {
            "totals": {
                "subtotal": 1.5e2,
                "tax": 1.2e1,
                "total": 1.62e2
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(150m, receipt.Totals.Subtotal);
        Assert.Equal(12m, receipt.Totals.Tax);
        Assert.Equal(162m, receipt.Totals.Total);
    }

    [Fact]
    public void DeserializeReceipt_WithVerySmallAmounts_PreservesPrecision()
    {
        // Arrange
        var json = """
        {
            "totals": {
                "subtotal": 0.01,
                "tax": 0.001,
                "discount": 0.0001,
                "total": 0.0111
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(0.01m, receipt.Totals.Subtotal);
        Assert.Equal(0.001m, receipt.Totals.Tax);
        Assert.Equal(0.0001m, receipt.Totals.Discount);
        Assert.Equal(0.0111m, receipt.Totals.Total);
    }

    [Fact]
    public void DeserializeReceipt_WithLargeAmounts_HandlesCorrectly()
    {
        // Arrange
        var json = """
        {
            "totals": {
                "subtotal": 999999.99,
                "tax": 79999.99,
                "total": 1079999.98
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(999999.99m, receipt.Totals.Subtotal);
        Assert.Equal(79999.99m, receipt.Totals.Tax);
        Assert.Equal(1079999.98m, receipt.Totals.Total);
    }

    [Fact]
    public void DeserializeCheck_WithNegativeAmount_HandlesCorrectly()
    {
        // Arrange - Could happen with voided checks
        var json = """
        {
            "checkNumber": "VOID-123",
            "amount": -150.00
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, _jsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.Equal(-150.00m, check.Amount);
    }

    [Fact]
    public void DeserializeReceipt_WithMissingOptionalAmounts_DefaultsToNull()
    {
        // Arrange
        var json = """
        {
            "totals": {
                "total": 100.00
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Null(receipt.Totals.Subtotal);
        Assert.Null(receipt.Totals.Tax);
        Assert.Null(receipt.Totals.Tip);
        Assert.Null(receipt.Totals.Discount);
        Assert.Equal(100.00m, receipt.Totals.Total);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("0.00")]
    [InlineData("-0")]
    [InlineData("-0.0")]
    public void DeserializeReceipt_WithZeroVariations_HandlesCorrectly(string zeroValue)
    {
        // Arrange
        var json = $$"""
        {
            "totals": {
                "discount": {{zeroValue}},
                "total": 100.00
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(0m, receipt.Totals.Discount);
    }

    [Fact]
    public void DeserializeReceipt_WithTrailingDecimalZeros_PreservesValue()
    {
        // Arrange
        var json = """
        {
            "totals": {
                "subtotal": 100.00,
                "tax": 8.000,
                "total": 108.0000000
            }
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Totals);
        Assert.Equal(100.00m, receipt.Totals.Subtotal);
        Assert.Equal(8.000m, receipt.Totals.Tax);
        Assert.Equal(108.0000000m, receipt.Totals.Total);
    }

    [Fact]
    public void SerializeReceipt_WithDecimalValues_ProducesCleanJson()
    {
        // Arrange
        var receipt = new Receipt
        {
            Totals = new ReceiptTotals
            {
                Subtotal = 100.00m,
                Tax = 8.50m,
                Tip = 15.00m,
                Total = 123.50m
            }
        };

        // Act
        var json = JsonSerializer.Serialize(receipt, _jsonOptions);
        var parsed = JsonDocument.Parse(json);

        // Assert
        var totals = parsed.RootElement.GetProperty("totals");
        
        // Verify all values are numbers, not strings
        Assert.Equal(JsonValueKind.Number, totals.GetProperty("subtotal").ValueKind);
        Assert.Equal(JsonValueKind.Number, totals.GetProperty("tax").ValueKind);
        Assert.Equal(JsonValueKind.Number, totals.GetProperty("tip").ValueKind);
        Assert.Equal(JsonValueKind.Number, totals.GetProperty("total").ValueKind);
        
        // Verify values
        Assert.Equal(100.00m, totals.GetProperty("subtotal").GetDecimal());
        Assert.Equal(8.50m, totals.GetProperty("tax").GetDecimal());
        Assert.Equal(15.00m, totals.GetProperty("tip").GetDecimal());
        Assert.Equal(123.50m, totals.GetProperty("total").GetDecimal());
    }

    [Fact]
    public void RoundTrip_ReceiptWithComplexDecimalValues_PreservesAllData()
    {
        // Arrange
        var original = new Receipt
        {
            Merchant = new MerchantInfo { Name = "Test Store" },
            Items = new List<ReceiptLineItem>
            {
                new() 
                { 
                    Description = "Item 1", 
                    Quantity = 3,
                    UnitPrice = 12.99m,
                    TotalPrice = 38.97m,
                    DiscountAmount = 1.00m
                }
            },
            Totals = new ReceiptTotals
            {
                Subtotal = 38.97m,
                Tax = 3.12m,
                Tip = 6.00m,
                Discount = 1.00m,
                Total = 47.09m
            },
            Taxes = new List<ReceiptTaxItem>
            {
                new() { TaxName = "state", TaxRate = 8.0m, TaxAmount = 3.12m }
            },
            PaymentMethod = "credit"
        };

        // Act
        var json = JsonSerializer.Serialize(original, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Totals);
        Assert.Equal(original.Totals.Subtotal, deserialized.Totals.Subtotal);
        Assert.Equal(original.Totals.Tax, deserialized.Totals.Tax);
        Assert.Equal(original.Totals.Tip, deserialized.Totals.Tip);
        Assert.Equal(original.Totals.Discount, deserialized.Totals.Discount);
        Assert.Equal(original.Totals.Total, deserialized.Totals.Total);
        
        Assert.NotNull(deserialized.Items);
        Assert.Single(deserialized.Items);
        Assert.Equal(original.Items[0].UnitPrice, deserialized.Items[0].UnitPrice);
        Assert.Equal(original.Items[0].TotalPrice, deserialized.Items[0].TotalPrice);
        Assert.Equal(original.Items[0].DiscountAmount, deserialized.Items[0].DiscountAmount);
        
        Assert.NotNull(deserialized.Taxes);
        Assert.Single(deserialized.Taxes);
        Assert.Equal(original.Taxes[0].TaxAmount, deserialized.Taxes[0].TaxAmount);
        
        Assert.Equal(original.PaymentMethod, deserialized.PaymentMethod);
    }
}