using System.Text.Json;
using System.Text.Json.Serialization;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Xunit;

namespace Nolock.social.CloudflareAI.Tests.JsonExtraction.Models;

public class ReceiptModelsTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void ReceiptTotals_DeserializesAllDecimalFields_FromNumbers()
    {
        // Arrange
        var json = """
        {
            "subtotal": 50.00,
            "tax": 4.25,
            "tip": 10.00,
            "discount": 5.00,
            "total": 59.25
        }
        """;

        // Act
        var totals = JsonSerializer.Deserialize<ReceiptTotals>(json, _jsonOptions);

        // Assert
        Assert.NotNull(totals);
        Assert.Equal(50.00m, totals.Subtotal);
        Assert.Equal(4.25m, totals.Tax);
        Assert.Equal(10.00m, totals.Tip);
        Assert.Equal(5.00m, totals.Discount);
        Assert.Equal(59.25m, totals.Total);
    }

    [Fact]
    public void ReceiptTotals_HandlesNullableFields()
    {
        // Arrange
        var json = """
        {
            "subtotal": null,
            "tax": null,
            "tip": null,
            "discount": null,
            "total": 100.00
        }
        """;

        // Act
        var totals = JsonSerializer.Deserialize<ReceiptTotals>(json, _jsonOptions);

        // Assert
        Assert.NotNull(totals);
        Assert.Null(totals.Subtotal);
        Assert.Null(totals.Tax);
        Assert.Null(totals.Tip);
        Assert.Null(totals.Discount);
        Assert.Equal(100.00m, totals.Total);
    }

    [Fact]
    public void ReceiptLineItem_DeserializesDecimalPrices()
    {
        // Arrange
        var json = """
        {
            "description": "Coffee",
            "quantity": 2,
            "unitPrice": 3.50,
            "totalPrice": 7.00,
            "discountAmount": 0.50
        }
        """;

        // Act
        var item = JsonSerializer.Deserialize<ReceiptLineItem>(json, _jsonOptions);

        // Assert
        Assert.NotNull(item);
        Assert.Equal("Coffee", item.Description);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(3.50m, item.UnitPrice);
        Assert.Equal(7.00m, item.TotalPrice);
        Assert.Equal(0.50m, item.DiscountAmount);
    }

    [Fact]
    public void ReceiptTaxItem_DeserializesTaxAmount()
    {
        // Arrange
        var json = """
        {
            "taxName": "sales",
            "taxRate": 8.25,
            "taxAmount": 12.38
        }
        """;

        // Act
        var tax = JsonSerializer.Deserialize<ReceiptTaxItem>(json, _jsonOptions);

        // Assert
        Assert.NotNull(tax);
        Assert.Equal("sales", tax.TaxName);
        Assert.Equal(8.25m, tax.TaxRate);
        Assert.Equal(12.38m, tax.TaxAmount);
    }

    [Fact]
    public void ReceiptPaymentMethod_DeserializesAmount()
    {
        // Arrange
        var json = """
        {
            "method": "credit",
            "lastDigits": "1234",
            "amount": 59.25
        }
        """;

        // Act
        var payment = JsonSerializer.Deserialize<ReceiptPaymentMethod>(json, _jsonOptions);

        // Assert
        Assert.NotNull(payment);
        Assert.Equal(PaymentMethod.Credit, payment.Method);
        Assert.Equal("1234", payment.LastDigits);
        Assert.Equal(59.25m, payment.Amount);
    }

    [Fact]
    public void Receipt_DeserializesCompleteReceiptWithDecimalValues()
    {
        // Arrange
        var json = """
        {
            "merchant": {
                "name": "Test Store"
            },
            "items": [
                {
                    "description": "Item 1",
                    "quantity": 1,
                    "unitPrice": 10.00,
                    "totalPrice": 10.00
                },
                {
                    "description": "Item 2",
                    "quantity": 2,
                    "unitPrice": 5.50,
                    "totalPrice": 11.00
                }
            ],
            "totals": {
                "subtotal": 21.00,
                "tax": 1.68,
                "total": 22.68
            },
            "taxes": [
                {
                    "taxName": "state",
                    "taxRate": 8.0,
                    "taxAmount": 1.68
                }
            ],
            "paymentMethod": "cash"
        }
        """;

        // Act
        var receipt = JsonSerializer.Deserialize<Receipt>(json, _jsonOptions);

        // Assert
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Merchant);
        Assert.Equal("Test Store", receipt.Merchant.Name);
        
        Assert.NotNull(receipt.Items);
        Assert.Equal(2, receipt.Items.Count);
        Assert.Equal(10.00m, receipt.Items[0].TotalPrice);
        Assert.Equal(11.00m, receipt.Items[1].TotalPrice);
        
        Assert.NotNull(receipt.Totals);
        Assert.Equal(21.00m, receipt.Totals.Subtotal);
        Assert.Equal(1.68m, receipt.Totals.Tax);
        Assert.Equal(22.68m, receipt.Totals.Total);
        
        Assert.NotNull(receipt.Taxes);
        Assert.Single(receipt.Taxes);
        Assert.Equal(1.68m, receipt.Taxes[0].TaxAmount);
        
        Assert.Equal("cash", receipt.PaymentMethod);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.99)]
    [InlineData(1.00)]
    [InlineData(999.99)]
    [InlineData(9999.99)]
    public void ReceiptTotals_HandlesVariousDecimalAmounts(decimal amount)
    {
        // Arrange
        var json = $$"""
        {
            "total": {{amount}}
        }
        """;

        // Act
        var totals = JsonSerializer.Deserialize<ReceiptTotals>(json, _jsonOptions);

        // Assert
        Assert.NotNull(totals);
        Assert.Equal(amount, totals.Total);
    }

    [Fact]
    public void ReceiptTotals_SerializesAsNumbers()
    {
        // Arrange
        var totals = new ReceiptTotals
        {
            Subtotal = 100.00m,
            Tax = 8.00m,
            Tip = 15.00m,
            Discount = 10.00m,
            Total = 113.00m
        };

        // Act
        var json = JsonSerializer.Serialize(totals, _jsonOptions);
        var parsed = JsonDocument.Parse(json);

        // Assert
        Assert.Equal(JsonValueKind.Number, parsed.RootElement.GetProperty("subtotal").ValueKind);
        Assert.Equal(JsonValueKind.Number, parsed.RootElement.GetProperty("tax").ValueKind);
        Assert.Equal(JsonValueKind.Number, parsed.RootElement.GetProperty("tip").ValueKind);
        Assert.Equal(JsonValueKind.Number, parsed.RootElement.GetProperty("discount").ValueKind);
        Assert.Equal(JsonValueKind.Number, parsed.RootElement.GetProperty("total").ValueKind);
        
        Assert.Equal(100.00m, parsed.RootElement.GetProperty("subtotal").GetDecimal());
        Assert.Equal(113.00m, parsed.RootElement.GetProperty("total").GetDecimal());
    }
}