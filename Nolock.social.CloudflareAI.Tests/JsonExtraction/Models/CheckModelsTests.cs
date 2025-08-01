using System.Text.Json;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Xunit;

namespace Nolock.social.CloudflareAI.Tests.JsonExtraction.Models;

public class CheckModelsTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Check_DeserializesAmountAsDecimal_FromNumber()
    {
        // Arrange
        var json = """
        {
            "checkNumber": "1234",
            "amount": 150.50,
            "payee": "John Doe"
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, _jsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.Equal(150.50m, check.Amount);
        Assert.Equal("1234", check.CheckNumber);
        Assert.Equal("John Doe", check.Payee);
    }

    [Fact]
    public void Check_DeserializesAmountAsNull_FromNull()
    {
        // Arrange
        var json = """
        {
            "checkNumber": "1234",
            "amount": null,
            "payee": "John Doe"
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, _jsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.Null(check.Amount);
    }

    [Fact]
    public void Check_SerializesAmountAsNumber()
    {
        // Arrange
        var check = new Check
        {
            CheckNumber = "1234",
            Amount = 150.50m,
            Payee = "John Doe"
        };

        // Act
        var json = JsonSerializer.Serialize(check, _jsonOptions);
        var parsed = JsonDocument.Parse(json);

        // Assert
        Assert.Equal(JsonValueKind.Number, parsed.RootElement.GetProperty("amount").ValueKind);
        Assert.Equal(150.50m, parsed.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public void SimpleCheck_DeserializesAmountAsDecimal_FromNumber()
    {
        // Arrange
        var json = """
        {
            "check_number": "5678",
            "amount": 999.99,
            "payee": "Jane Smith"
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<SimpleCheck>(json, _jsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.Equal(999.99m, check.Amount);
        Assert.Equal("5678", check.CheckNumber);
        Assert.Equal("Jane Smith", check.Payee);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1.0)]
    [InlineData(100.00)]
    [InlineData(1000.50)]
    [InlineData(99999.99)]
    public void Check_HandlesVariousDecimalAmounts(decimal amount)
    {
        // Arrange
        var json = $$"""
        {
            "amount": {{amount}}
        }
        """;

        // Act
        var check = JsonSerializer.Deserialize<Check>(json, _jsonOptions);

        // Assert
        Assert.NotNull(check);
        Assert.Equal(amount, check.Amount);
    }

    [Fact]
    public void Check_ThrowsOnInvalidAmountType()
    {
        // Arrange
        var json = """
        {
            "amount": "not a number"
        }
        """;

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<Check>(json, _jsonOptions));
    }
}