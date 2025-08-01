using System.Text.Json;
using Nolock.social.CloudflareAI.JsonExtraction.Models;

namespace Nolock.social.CloudflareAI.IntegrationTests;

public class DocumentTypeTests
{
    [Fact]
    public void DocumentType_Serializes_Correctly()
    {
        // Arrange
        var request = new OcrExtractionRequest
        {
            DocumentType = DocumentType.Check,
            Content = "Test content"
        };
        
        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<OcrExtractionRequest>(json);
        
        // Assert
        Assert.Contains("\"document_type\":\"check\"", json);
        Assert.NotNull(deserialized);
        Assert.Equal(DocumentType.Check, deserialized.DocumentType);
    }
    
    [Fact]
    public void DocumentType_Deserializes_FromString()
    {
        // Arrange
        var json = @"{""document_type"":""receipt"",""content"":""test""}";
        
        // Act
        var deserialized = JsonSerializer.Deserialize<OcrExtractionRequest>(json);
        
        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(DocumentType.Receipt, deserialized.DocumentType);
    }
    
    [Theory]
    [InlineData(DocumentType.Check, "check")]
    [InlineData(DocumentType.Receipt, "receipt")]
    public void DocumentType_SerializesToCorrectString(DocumentType documentType, string expected)
    {
        // Arrange
        var obj = new { Type = documentType };
        
        // Act
        var json = JsonSerializer.Serialize(obj);
        
        // Assert
        Assert.Contains($"\"Type\":\"{expected}\"", json);
    }
    
    [Fact]
    public void OcrExtractionResponse_WithDocumentType_SerializesCorrectly()
    {
        // Arrange
        var response = new OcrExtractionResponse<SimpleCheck>
        {
            DocumentType = DocumentType.Check,
            Success = true,
            Data = new SimpleCheck 
            { 
                CheckNumber = "1234",
                Amount = 100.00m
            },
            Confidence = 0.95,
            ProcessingTimeMs = 150
        };
        
        // Act
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        // Assert
        Assert.Contains("\"document_type\": \"check\"", json);
        Assert.Contains("\"success\": true", json);
        Assert.Contains("\"check_number\": \"1234\"", json);
        Assert.Contains("\"confidence\": 0.95", json);
    }
    
    [Fact]
    public void BatchOcrExtractionRequest_WithDocumentType_Works()
    {
        // Arrange
        var request = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Receipt,
            Contents = new List<string> { "receipt1", "receipt2" },
            UseSimpleSchema = true,
            MaxConcurrency = 5
        };
        
        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<BatchOcrExtractionRequest>(json);
        
        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(DocumentType.Receipt, deserialized.DocumentType);
        Assert.Equal(2, deserialized.Contents.Count);
        Assert.True(deserialized.UseSimpleSchema);
        Assert.Equal(5, deserialized.MaxConcurrency);
    }
    
    
    [Theory]
    [InlineData("check", DocumentType.Check)]
    [InlineData("Check", DocumentType.Check)]
    [InlineData("CHECK", DocumentType.Check)]
    [InlineData("receipt", DocumentType.Receipt)]
    [InlineData("Receipt", DocumentType.Receipt)]
    [InlineData("RECEIPT", DocumentType.Receipt)]
    public void DocumentType_ParsesFromStringCaseInsensitive(string input, DocumentType expected)
    {
        // Act
        var success = Enum.TryParse<DocumentType>(input, ignoreCase: true, out var result);
        
        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void DocumentType_InvalidValue_FailsToParse()
    {
        // Act
        var success = Enum.TryParse<DocumentType>("invoice", ignoreCase: true, out var result);
        
        // Assert
        Assert.False(success);
    }
}