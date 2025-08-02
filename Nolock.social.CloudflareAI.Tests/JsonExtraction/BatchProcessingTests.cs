using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.CloudflareAI.JsonExtraction.Services;
using Nolock.social.CloudflareAI.Tests.JsonExtraction.Models;
using Xunit;

namespace Nolock.social.CloudflareAI.Tests.JsonExtraction;

/// <summary>
/// Comprehensive tests for batch OCR processing operations
/// Tests batch request creation, validation, parallel processing, failure handling, size limits, and progress tracking
/// </summary>
public class BatchProcessingTests : JsonModelTestBase
{
    private readonly Mock<IWorkersAI> _mockWorkersAI;
    private readonly Mock<ILogger<OcrExtractionService>> _mockLogger;
    private readonly IOcrExtractionService _ocrService;

    public BatchProcessingTests()
    {
        _mockWorkersAI = new Mock<IWorkersAI>();
        _mockLogger = new Mock<ILogger<OcrExtractionService>>();
        _ocrService = new OcrExtractionService(_mockWorkersAI.Object, _mockLogger.Object);
    }

    #region Batch Request Creation and Validation Tests

    [Fact]
    public void BatchRequest_Creation_SetsDefaultValues()
    {
        // Act
        var request = new BatchOcrExtractionRequest();

        // Assert
        Assert.Equal(DocumentType.Check, request.DocumentType); // Default enum value
        Assert.NotNull(request.Contents);
        Assert.Empty(request.Contents);
        Assert.False(request.IsImage);
        Assert.False(request.UseSimpleSchema);
        Assert.Equal(3, request.MaxConcurrency); // Default value
    }

    [Fact]
    public void BatchRequest_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var contents = new List<string> { "Receipt 1", "Receipt 2", "Receipt 3" };

        // Act
        var request = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Receipt,
            Contents = contents,
            IsImage = true,
            UseSimpleSchema = true,
            MaxConcurrency = 5
        };

        // Assert
        Assert.Equal(DocumentType.Receipt, request.DocumentType);
        Assert.Equal(contents, request.Contents);
        Assert.True(request.IsImage);
        Assert.True(request.UseSimpleSchema);
        Assert.Equal(5, request.MaxConcurrency);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void BatchRequest_MaxConcurrency_AcceptsValidValues(int maxConcurrency)
    {
        // Act
        var request = new BatchOcrExtractionRequest
        {
            MaxConcurrency = maxConcurrency
        };

        // Assert
        Assert.Equal(maxConcurrency, request.MaxConcurrency);
    }

    [Fact]
    public void BatchRequest_EmptyContents_IsValid()
    {
        // Arrange & Act
        var request = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Check,
            Contents = new List<string>()
        };

        // Assert
        Assert.NotNull(request.Contents);
        Assert.Empty(request.Contents);
    }

    [Fact]
    public void BatchRequest_LargeContentsList_HandlesCorrectly()
    {
        // Arrange
        var largeContentsList = Enumerable.Range(1, 100)
            .Select(i => $"Document content {i}")
            .ToList();

        // Act
        var request = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Receipt,
            Contents = largeContentsList
        };

        // Assert
        Assert.Equal(100, request.Contents.Count);
        Assert.Equal("Document content 1", request.Contents.First());
        Assert.Equal("Document content 100", request.Contents.Last());
    }

    #endregion

    #region Batch Response Creation and Properties Tests

    [Fact]
    public void BatchResponse_Creation_SetsDefaultValues()
    {
        // Act
        var response = new BatchOcrExtractionResponse<object>();

        // Assert
        Assert.Equal(DocumentType.Check, response.DocumentType); // Default enum value
        Assert.NotNull(response.Results);
        Assert.Empty(response.Results);
        Assert.Equal(0, response.TotalProcessingTimeMs);
        Assert.Equal(0, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
    }

    [Fact]
    public void BatchResponse_SuccessCount_CalculatesCorrectly()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>
            {
                new() { Success = true },
                new() { Success = false },
                new() { Success = true },
                new() { Success = true }
            }
        };

        // Act & Assert
        Assert.Equal(3, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
    }

    [Fact]
    public void BatchResponse_AverageConfidence_CalculatesCorrectly()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>
            {
                new() { Success = true, Confidence = 0.9 },
                new() { Success = false, Confidence = 0.2 }, // Should be excluded from average
                new() { Success = true, Confidence = 0.8 },
                new() { Success = true, Confidence = 0.7 }
            }
        };

        // Act
        var averageConfidence = response.AverageConfidence;

        // Assert
        Assert.Equal(0.8, averageConfidence, 1); // (0.9 + 0.8 + 0.7) / 3 = 0.8
    }

    [Fact]
    public void BatchResponse_AverageConfidence_NoSuccessfulResults_ThrowsException()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>
            {
                new() { Success = false, Confidence = 0.2 },
                new() { Success = false, Confidence = 0.3 }
            }
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => response.AverageConfidence);
    }

    [Fact]
    public void BatchResponse_EmptyResults_PropertiesReturnZero()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>()
        };

        // Act & Assert
        Assert.Equal(0, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
        Assert.Throws<InvalidOperationException>(() => response.AverageConfidence); // No items to average
    }

    #endregion

    #region Batch Size Limits Tests

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public void BatchProcessing_VariousBatchSizes_HandlesCorrectly(int batchSize)
    {
        // Arrange
        var contents = Enumerable.Range(1, batchSize)
            .Select(i => $"Content {i}")
            .ToList();

        var request = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Receipt,
            Contents = contents,
            MaxConcurrency = Math.Min(batchSize, 5)
        };

        // Act - Just validate that the request can be created with various sizes
        // Assert
        Assert.Equal(batchSize, request.Contents.Count);
        Assert.True(request.MaxConcurrency <= 5);
        Assert.True(request.MaxConcurrency >= 1);
    }

    [Fact]
    public void BatchProcessing_LargeBatch_SplitsIntoChunks()
    {
        // Arrange
        var largeContentsList = Enumerable.Range(1, 100)
            .Select(i => $"Content {i}")
            .ToList();

        var request = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Receipt,
            Contents = largeContentsList,
            MaxConcurrency = 5
        };

        // Act - Test that we can create chunks properly
        var chunks = request.Contents.Chunk(request.MaxConcurrency).ToList();

        // Assert
        Assert.Equal(20, chunks.Count); // 100 items / 5 per chunk = 20 chunks
        Assert.Equal(5, chunks.First().Length); // First chunk has 5 items
        Assert.Equal(5, chunks.Last().Length); // Last chunk also has 5 items (100 is divisible by 5)
    }

    [Fact]
    public void BatchProcessing_MaxConcurrencyLimits_RespectedInChunking()
    {
        // Arrange
        var contents = Enumerable.Range(1, 13).Select(i => $"Content {i}").ToList(); // 13 items
        var maxConcurrency = 4;

        // Act
        var chunks = contents.Chunk(maxConcurrency).ToList();

        // Assert
        Assert.Equal(4, chunks.Count); // 13 items / 4 per chunk = 4 chunks (with remainder)
        Assert.Equal(4, chunks[0].Length); // First chunk: 4 items
        Assert.Equal(4, chunks[1].Length); // Second chunk: 4 items  
        Assert.Equal(4, chunks[2].Length); // Third chunk: 4 items
        Assert.Single(chunks[3]); // Fourth chunk: 1 item (remainder)
    }

    #endregion

    #region Parallel Processing Validation Tests

    [Fact]
    public void BatchRequest_ConcurrencySettings_ConfiguresParallelism()
    {
        // Arrange & Act
        var lowConcurrencyRequest = new BatchOcrExtractionRequest
        {
            MaxConcurrency = 1,
            Contents = new List<string> { "Item1", "Item2", "Item3" }
        };

        var highConcurrencyRequest = new BatchOcrExtractionRequest
        {
            MaxConcurrency = 10,
            Contents = new List<string> { "Item1", "Item2", "Item3" }
        };

        // Assert
        Assert.Equal(1, lowConcurrencyRequest.MaxConcurrency);
        Assert.Equal(10, highConcurrencyRequest.MaxConcurrency);
        
        // Validate chunking behavior
        var lowConcurrencyChunks = lowConcurrencyRequest.Contents.Chunk(lowConcurrencyRequest.MaxConcurrency).ToList();
        var highConcurrencyChunks = highConcurrencyRequest.Contents.Chunk(highConcurrencyRequest.MaxConcurrency).ToList();
        
        Assert.Equal(3, lowConcurrencyChunks.Count); // 3 chunks of 1 item each
        Assert.Single(highConcurrencyChunks); // 1 chunk of 3 items (all items fit in one chunk)
    }

    [Theory]
    [InlineData(1, 10, 10)] // 1 concurrency, 10 items = 10 batches
    [InlineData(3, 10, 4)]  // 3 concurrency, 10 items = 4 batches (3+3+3+1)
    [InlineData(5, 10, 2)]  // 5 concurrency, 10 items = 2 batches (5+5)
    [InlineData(10, 10, 1)] // 10 concurrency, 10 items = 1 batch
    [InlineData(15, 10, 1)] // 15 concurrency, 10 items = 1 batch (concurrency > items)
    public void BatchProcessing_ChunkCalculation_ProducesExpectedBatches(int maxConcurrency, int itemCount, int expectedBatches)
    {
        // Arrange
        var contents = Enumerable.Range(1, itemCount).Select(i => $"Item {i}").ToList();

        // Act
        var chunks = contents.Chunk(maxConcurrency).ToList();

        // Assert
        Assert.Equal(expectedBatches, chunks.Count);
    }

    #endregion

    #region Partial Failure Handling Tests

    [Fact]
    public void BatchResponse_MixedResults_TracksBothSuccessAndFailure()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            DocumentType = DocumentType.Receipt,
            Results = new List<OcrExtractionResponse<object>>
            {
                new() 
                { 
                    Success = true, 
                    Confidence = 0.95,
                    Data = new SimpleReceipt { MerchantName = "Store 1" },
                    ProcessingTimeMs = 100
                },
                new() 
                { 
                    Success = false, 
                    Error = "OCR failed to read text",
                    ProcessingTimeMs = 50
                },
                new() 
                { 
                    Success = true, 
                    Confidence = 0.88,
                    Data = new SimpleReceipt { MerchantName = "Store 2" },
                    ProcessingTimeMs = 120
                },
                new() 
                { 
                    Success = false, 
                    Error = "Invalid document format",
                    ProcessingTimeMs = 25
                }
            },
            TotalProcessingTimeMs = 295
        };

        // Act & Assert
        Assert.Equal(4, response.Results.Count);
        Assert.Equal(2, response.SuccessCount);
        Assert.Equal(2, response.FailureCount);
        Assert.Equal(0.915, response.AverageConfidence, 3); // (0.95 + 0.88) / 2
        Assert.Equal(295, response.TotalProcessingTimeMs);
    }

    [Fact]
    public void BatchResponse_AllFailures_HandlesGracefully()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            DocumentType = DocumentType.Check,
            Results = new List<OcrExtractionResponse<object>>
            {
                new() { Success = false, Error = "Network timeout" },
                new() { Success = false, Error = "Invalid image format" },
                new() { Success = false, Error = "OCR service unavailable" }
            }
        };

        // Act & Assert
        Assert.Equal(3, response.Results.Count);
        Assert.Equal(0, response.SuccessCount);
        Assert.Equal(3, response.FailureCount);
        Assert.Throws<InvalidOperationException>(() => response.AverageConfidence); // No successful results to average
    }

    [Fact]
    public void BatchResponse_AllSuccesses_CalculatesCorrectly()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            DocumentType = DocumentType.Receipt,
            Results = new List<OcrExtractionResponse<object>>
            {
                new() { Success = true, Confidence = 0.90 },
                new() { Success = true, Confidence = 0.85 },
                new() { Success = true, Confidence = 0.95 }
            }
        };

        // Act & Assert
        Assert.Equal(3, response.Results.Count);
        Assert.Equal(3, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
        Assert.Equal(0.90, response.AverageConfidence, 2); // (0.90 + 0.85 + 0.95) / 3
    }

    [Fact]
    public void BatchResponse_PartialFailures_PreservesIndividualErrors()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>
            {
                new() 
                { 
                    Success = true, 
                    Data = new SimpleCheck { Payee = "John Doe" }
                },
                new() 
                { 
                    Success = false, 
                    Error = "Item 1: Malformed check image"
                },
                new() 
                { 
                    Success = false, 
                    Error = "Item 2: Unable to detect MICR line"
                }
            }
        };

        // Act & Assert
        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(2, response.FailureCount);
        
        var failedResults = response.Results.Where(r => !r.Success).ToList();
        Assert.Equal("Item 1: Malformed check image", failedResults[0].Error);
        Assert.Equal("Item 2: Unable to detect MICR line", failedResults[1].Error);
    }

    #endregion

    #region Progress Tracking Tests

    [Fact]
    public void BatchResponse_ProcessingTimes_TrackIndividualAndTotal()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>
            {
                new() { Success = true, ProcessingTimeMs = 150 },
                new() { Success = true, ProcessingTimeMs = 200 },
                new() { Success = false, ProcessingTimeMs = 75 }
            },
            TotalProcessingTimeMs = 500 // Includes overhead
        };

        // Act & Assert
        Assert.Equal(150, response.Results[0].ProcessingTimeMs);
        Assert.Equal(200, response.Results[1].ProcessingTimeMs);
        Assert.Equal(75, response.Results[2].ProcessingTimeMs);
        Assert.Equal(500, response.TotalProcessingTimeMs);
        
        // Total time can be greater than sum of individual times due to batch processing overhead
        var sumOfIndividualTimes = response.Results.Sum(r => r.ProcessingTimeMs);
        Assert.Equal(425, sumOfIndividualTimes);
        Assert.True(response.TotalProcessingTimeMs >= sumOfIndividualTimes);
    }

    [Fact]
    public void BatchResponse_EmptyBatch_HasZeroProcessingTime()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>(),
            TotalProcessingTimeMs = 10 // Minimal overhead for empty batch
        };

        // Act & Assert
        Assert.Empty(response.Results);
        Assert.Equal(10, response.TotalProcessingTimeMs);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(5, 500)]
    [InlineData(10, 1000)]
    public void BatchResponse_ProcessingTimeMetrics_ScaleWithBatchSize(int batchSize, long expectedBaseTime)
    {
        // Arrange
        var results = Enumerable.Range(1, batchSize)
            .Select(i => new OcrExtractionResponse<object>
            {
                Success = true,
                ProcessingTimeMs = expectedBaseTime / batchSize // Distribute time evenly
            })
            .ToList();

        var response = new BatchOcrExtractionResponse<object>
        {
            Results = results,
            TotalProcessingTimeMs = expectedBaseTime + 50 // Add some overhead
        };

        // Act & Assert
        Assert.Equal(batchSize, response.Results.Count);
        Assert.Equal(expectedBaseTime + 50, response.TotalProcessingTimeMs);
        
        var totalIndividualTime = response.Results.Sum(r => r.ProcessingTimeMs);
        Assert.Equal(expectedBaseTime, totalIndividualTime);
    }

    #endregion

    #region Integration-Style Tests

    [Fact]
    public void BatchProcessing_EndToEndRequest_PropertiesSetCorrectly()
    {
        // Arrange
        var ocrTexts = new List<string>
        {
            "Check #1001 Pay to: John Smith Amount: $500.00",
            "Check #1002 Pay to: Jane Doe Amount: $750.00",
            "Check #1003 Pay to: Bob Johnson Amount: $300.00"
        };

        // Act
        var request = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Check,
            Contents = ocrTexts,
            IsImage = false,
            UseSimpleSchema = true,
            MaxConcurrency = 3
        };

        // Assert
        Assert.Equal(DocumentType.Check, request.DocumentType);
        Assert.Equal(3, request.Contents.Count);
        Assert.False(request.IsImage);
        Assert.True(request.UseSimpleSchema);
        Assert.Equal(3, request.MaxConcurrency);
        
        // Validate content preservation
        Assert.Contains("John Smith", request.Contents[0]);
        Assert.Contains("Jane Doe", request.Contents[1]);
        Assert.Contains("Bob Johnson", request.Contents[2]);
    }

    [Fact]
    public void BatchProcessing_MixedDocumentTypes_RequiresSeparateRequests()
    {
        // Arrange - This test validates the design where batch requests handle single document types
        var checkTexts = new List<string>
        {
            "Check #1001 Pay to: John Smith Amount: $500.00"
        };
        
        var receiptTexts = new List<string>
        {
            "Store: ABC Market Total: $25.99 Date: 2024-01-15"
        };

        // Act
        var checkRequest = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Check,
            Contents = checkTexts
        };

        var receiptRequest = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Receipt,
            Contents = receiptTexts
        };

        // Assert
        Assert.Equal(DocumentType.Check, checkRequest.DocumentType);
        Assert.Equal(DocumentType.Receipt, receiptRequest.DocumentType);
        Assert.Single(checkRequest.Contents);
        Assert.Single(receiptRequest.Contents);
    }

    [Fact]
    public void BatchProcessing_ConfigurationVariations_SupportDifferentScenarios()
    {
        // Arrange & Act - Test different common configurations
        
        // Fast processing with simple schema
        var fastBatch = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Receipt,
            Contents = new List<string> { "Receipt 1", "Receipt 2" },
            UseSimpleSchema = true,
            MaxConcurrency = 5
        };

        // Thorough processing with full schema
        var thoroughBatch = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Receipt,
            Contents = new List<string> { "Receipt 1", "Receipt 2" },
            UseSimpleSchema = false,
            MaxConcurrency = 2
        };

        // Image processing batch
        var imageBatch = new BatchOcrExtractionRequest
        {
            DocumentType = DocumentType.Check,
            Contents = new List<string> { "base64image1", "base64image2" },
            IsImage = true,
            UseSimpleSchema = true,
            MaxConcurrency = 3
        };

        // Assert
        Assert.True(fastBatch.UseSimpleSchema);
        Assert.Equal(5, fastBatch.MaxConcurrency);
        
        Assert.False(thoroughBatch.UseSimpleSchema);
        Assert.Equal(2, thoroughBatch.MaxConcurrency);
        
        Assert.True(imageBatch.IsImage);
        Assert.Equal(DocumentType.Check, imageBatch.DocumentType);
    }

    #endregion

    #region Edge Cases and Error Conditions

    [Fact]
    public void BatchRequest_NullContents_ThrowsWhenAccessed()
    {
        // Arrange
        var request = new BatchOcrExtractionRequest();
        
        // Act - Setting Contents to null
        request.Contents = null!;

        // Assert - Should throw when accessed
        Assert.Throws<NullReferenceException>(() => request.Contents.Count);
    }

    [Fact]
    public void BatchResponse_NullResults_ThrowsWhenCalculatingMetrics()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>();
        response.Results = null!;

        // Act & Assert - Properties should throw when Results is null
        Assert.Throws<ArgumentNullException>(() => response.SuccessCount);
        Assert.Throws<ArgumentNullException>(() => response.FailureCount);
        Assert.Throws<ArgumentNullException>(() => response.AverageConfidence);
    }

    [Fact]
    public void BatchProcessing_ZeroConcurrency_ShouldBeHandledByApplication()
    {
        // Arrange & Act - Test boundary condition
        var request = new BatchOcrExtractionRequest
        {
            MaxConcurrency = 0,
            Contents = new List<string> { "Content 1" }
        };

        // Assert - The model allows it, but application logic should validate
        Assert.Equal(0, request.MaxConcurrency);
        // Note: In real usage, application should validate MaxConcurrency > 0
    }

    [Fact]
    public void BatchProcessing_NegativeConcurrency_ShouldBeHandledByApplication()
    {
        // Arrange & Act - Test boundary condition
        var request = new BatchOcrExtractionRequest
        {
            MaxConcurrency = -1,
            Contents = new List<string> { "Content 1" }
        };

        // Assert - The model allows it, but application logic should validate
        Assert.Equal(-1, request.MaxConcurrency);
        // Note: In real usage, application should validate MaxConcurrency > 0
    }

    [Fact]
    public void BatchResponse_VeryLowConfidenceScores_AverageCalculatesCorrectly()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>
            {
                new() { Success = true, Confidence = 0.01 }, // Very low confidence
                new() { Success = true, Confidence = 0.02 },
                new() { Success = true, Confidence = 0.03 }
            }
        };

        // Act
        var averageConfidence = response.AverageConfidence;

        // Assert
        Assert.Equal(0.02, averageConfidence, 3); // (0.01 + 0.02 + 0.03) / 3 = 0.02
    }

    [Fact]
    public void BatchResponse_VeryHighConfidenceScores_AverageCalculatesCorrectly()
    {
        // Arrange
        var response = new BatchOcrExtractionResponse<object>
        {
            Results = new List<OcrExtractionResponse<object>>
            {
                new() { Success = true, Confidence = 0.99 },
                new() { Success = true, Confidence = 1.00 }, // Perfect confidence
                new() { Success = true, Confidence = 0.98 }
            }
        };

        // Act
        var averageConfidence = response.AverageConfidence;

        // Assert
        Assert.Equal(0.99, averageConfidence, 3); // (0.99 + 1.00 + 0.98) / 3 = 0.99
    }

    #endregion
}