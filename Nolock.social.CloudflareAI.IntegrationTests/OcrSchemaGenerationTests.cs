using System.Linq;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.SchemaGeneration;

namespace Nolock.social.CloudflareAI.IntegrationTests;

public class OcrSchemaGenerationTests
{
    [Fact]
    public void GenerateSchema_ForCheckModel_CreatesCorrectSchema()
    {
        // Act
        var schema = ReflectionSchemaExtensions.FromType<Check>();
        
        // Assert
        Assert.Equal("Check", schema.Name);
        Assert.NotEmpty(schema.Properties);
        
        // Verify key properties exist with correct names
        var checkNumberProp = schema.Properties.FirstOrDefault(p => p.Name == "checkNumber");
        Assert.NotNull(checkNumberProp);
        Assert.Equal("string", checkNumberProp.Type);
        
        var amountProp = schema.Properties.FirstOrDefault(p => p.Name == "amount");
        Assert.NotNull(amountProp);
        Assert.Equal("string", amountProp.Type);
        
        var confidenceProp = schema.Properties.FirstOrDefault(p => p.Name == "confidence");
        Assert.NotNull(confidenceProp);
        Assert.Equal("number", confidenceProp.Type);
        Assert.True(confidenceProp.Required);
    }
    
    [Fact]
    public void GenerateSchema_ForReceiptModel_CreatesCorrectSchema()
    {
        // Act
        var schema = ReflectionSchemaExtensions.FromType<Receipt>();
        
        // Assert
        Assert.Equal("Receipt", schema.Name);
        Assert.NotEmpty(schema.Properties);
        
        // Verify nested object properties
        var merchantProp = schema.Properties.FirstOrDefault(p => p.Name == "merchant");
        Assert.NotNull(merchantProp);
        Assert.Equal("object", merchantProp.Type);
        
        // Verify array properties
        var itemsProp = schema.Properties.FirstOrDefault(p => p.Name == "items");
        Assert.NotNull(itemsProp);
        Assert.Equal("array", itemsProp.Type);
        
        var taxesProp = schema.Properties.FirstOrDefault(p => p.Name == "taxes");
        Assert.NotNull(taxesProp);
        Assert.Equal("array", taxesProp.Type);
    }
    
    [Fact]
    public void GenerateSchema_ForSimpleCheck_HasCorrectJsonPropertyNames()
    {
        // Act
        var schema = ReflectionSchemaExtensions.FromType<SimpleCheck>();
        
        // Assert
        // Verify snake_case property names from JsonPropertyName attributes
        Assert.Contains(schema.Properties, p => p.Name == "check_number");
        Assert.Contains(schema.Properties, p => p.Name == "bank_name");
        Assert.Contains(schema.Properties, p => p.Name == "is_signed");
        
        // Should not contain C# property names
        Assert.DoesNotContain(schema.Properties, p => p.Name == "CheckNumber");
        Assert.DoesNotContain(schema.Properties, p => p.Name == "BankName");
    }
    
    [Fact]
    public void GenerateSchema_ForSimpleReceipt_HasCorrectJsonPropertyNames()
    {
        // Act
        var schema = ReflectionSchemaExtensions.FromType<SimpleReceipt>();
        
        // Assert
        // Verify snake_case property names
        Assert.Contains(schema.Properties, p => p.Name == "merchant_name");
        Assert.Contains(schema.Properties, p => p.Name == "total_amount");
        Assert.Contains(schema.Properties, p => p.Name == "tax_amount");
        Assert.Contains(schema.Properties, p => p.Name == "payment_method");
        Assert.Contains(schema.Properties, p => p.Name == "items_count");
    }
    
    [Fact]
    public void GenerateSchema_WithDescriptionAttributes_IncludesDescriptions()
    {
        // Act
        var schema = ReflectionSchemaExtensions.FromType<Check>();
        
        // Assert
        var routingNumberProp = schema.Properties.FirstOrDefault(p => p.Name == "routingNumber");
        Assert.NotNull(routingNumberProp);
        Assert.Contains("9 digits", routingNumberProp.Description);
        
        var confidenceProp = schema.Properties.FirstOrDefault(p => p.Name == "confidence");
        Assert.NotNull(confidenceProp);
        Assert.Contains("0-1", confidenceProp.Description);
    }
    
    [Fact]
    public void GenerateSchema_ForNestedTypes_HandlesCorrectly()
    {
        // Act
        var merchantSchema = ReflectionSchemaExtensions.FromType<MerchantInfo>();
        
        // Assert
        Assert.Equal("MerchantInfo", merchantSchema.Name);
        
        var nameProp = merchantSchema.Properties.FirstOrDefault(p => p.Name == "name");
        Assert.NotNull(nameProp);
        Assert.True(nameProp.Required); // Name is not nullable
        
        var addressProp = merchantSchema.Properties.FirstOrDefault(p => p.Name == "address");
        Assert.NotNull(addressProp);
        Assert.False(addressProp.Required); // Address is nullable
    }
    
    [Fact]
    public void GenerateSchema_HandlesDateTimeProperties()
    {
        // Act
        var checkSchema = ReflectionSchemaExtensions.FromType<Check>();
        var receiptSchema = ReflectionSchemaExtensions.FromType<Receipt>();
        
        // Assert
        var checkDateProp = checkSchema.Properties.FirstOrDefault(p => p.Name == "date");
        Assert.NotNull(checkDateProp);
        Assert.Equal("string", checkDateProp.Type); // DateTime serializes as string
        
        var receiptTimestampProp = receiptSchema.Properties.FirstOrDefault(p => p.Name == "timestamp");
        Assert.NotNull(receiptTimestampProp);
        Assert.Equal("string", receiptTimestampProp.Type);
    }
}