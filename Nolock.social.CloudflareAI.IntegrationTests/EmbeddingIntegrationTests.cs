using Nolock.social.CloudflareAI.Models;

namespace Nolock.social.CloudflareAI.IntegrationTests;

[Collection("CloudflareAI")]
public sealed class EmbeddingIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task RunAsync_BGESmall_WithSingleText_GeneratesEmbedding()
    {
        var request = new EmbeddingRequest
        {
            Text = ["The quick brown fox jumps over the lazy dog."]
        };

        var result = await Client.RunAsync<EmbeddingResponse>(
            EmbeddingModels.BGE_Small_EN_V1_5,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.NotNull(result.Data[0]);
        Assert.True(result.Data[0].Length > 0);
        
        Logger.LogInformation("Generated embedding dimensions: {Dimensions}", result.Data[0].Length);
        
        // BGE-small typically generates 384-dimensional embeddings
        Assert.True(result.Data[0].Length >= 300, 
            $"Embedding dimension too small: {result.Data[0].Length}");
    }

    [Fact]
    public async Task RunAsync_BGEBase_WithMultipleTexts_GeneratesMultipleEmbeddings()
    {
        var request = new EmbeddingRequest
        {
            Text = [
                "Machine learning is a subset of artificial intelligence.",
                "Deep learning uses neural networks with multiple layers.",
                "Natural language processing helps computers understand human language."
            ]
        };

        var result = await Client.RunAsync<EmbeddingResponse>(
            EmbeddingModels.BGE_Base_EN_V1_5,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Length);
        
        foreach (var embedding in result.Data)
        {
            Assert.NotNull(embedding);
            Assert.True(embedding.Length > 0);
            Logger.LogInformation("Embedding dimensions: {Dimensions}", embedding.Length);
        }
        
        // All embeddings should have the same dimension
        var firstDimension = result.Data[0].Length;
        Assert.All(result.Data, data => 
            Assert.Equal(firstDimension, data.Length));
    }

    [Fact]
    public async Task RunAsync_BGELarge_WithTechnicalText_GeneratesDetailedEmbedding()
    {
        var request = new EmbeddingRequest
        {
            Text = ["Quantum computing leverages quantum mechanical phenomena such as superposition and entanglement to process information in ways that classical computers cannot."]
        };

        var result = await Client.RunAsync<EmbeddingResponse>(
            EmbeddingModels.BGE_Large_EN_V1_5,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.NotNull(result.Data[0]);
        
        Logger.LogInformation("Large model embedding dimensions: {Dimensions}", result.Data[0].Length);
        
        // BGE-large typically generates larger embeddings than base/small
        Assert.True(result.Data[0].Length >= 768, 
            $"Large model embedding dimension too small: {result.Data[0].Length}");
    }

    [Fact]
    public async Task RunAsync_WithSimilarTexts_GeneratesSimilarEmbeddings()
    {
        var request = new EmbeddingRequest
        {
            Text = [
                "The cat is sleeping on the mat.",
                "A cat is resting on the rug.",
                "Dogs are barking loudly outside."
            ]
        };

        var result = await Client.RunAsync<EmbeddingResponse>(
            EmbeddingModels.BGE_Base_EN_V1_5,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Length);

        var embedding1 = result.Data[0];
        var embedding2 = result.Data[1];
        var embedding3 = result.Data[2];

        // Calculate cosine similarity
        var similarity12 = CosineSimilarity(embedding1, embedding2);
        var similarity13 = CosineSimilarity(embedding1, embedding3);
        var similarity23 = CosineSimilarity(embedding2, embedding3);

        Logger.LogInformation("Similarity cat1-cat2: {Sim12:F4}", similarity12);
        Logger.LogInformation("Similarity cat1-dog: {Sim13:F4}", similarity13);
        Logger.LogInformation("Similarity cat2-dog: {Sim23:F4}", similarity23);

        // Similar texts (about cats) should have higher similarity than dissimilar ones
        Assert.True(similarity12 > similarity13, 
            $"Similar texts should be more similar: {similarity12} vs {similarity13}");
        Assert.True(similarity12 > similarity23, 
            $"Similar texts should be more similar: {similarity12} vs {similarity23}");
    }

    [Fact]
    public async Task RunAsync_WithEmptyText_ThrowsOrHandlesGracefully()
    {
        var request = new EmbeddingRequest
        {
            Text = [""]
        };

        // This might throw an exception or return empty embedding depending on the model
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await Client.RunAsync<EmbeddingResponse>(
                EmbeddingModels.BGE_Small_EN_V1_5,
                request);
            
            if (result?.Data != null && result.Data.Length > 0)
            {
                Logger.LogInformation("Empty text embedding dimensions: {Dimensions}", 
                    result.Data[0]?.Length ?? 0);
            }
        });

        // Either succeeds or throws a reasonable exception
        if (exception != null)
        {
            Assert.IsType<HttpRequestException>(exception);
            Logger.LogInformation("Empty text correctly resulted in error: {Message}", exception.Message);
        }
    }

    [Fact]
    public async Task RunAsync_WithLongText_GeneratesEmbedding()
    {
        var longText = string.Join(" ", Enumerable.Repeat(
            "This is a test sentence to create a very long text that exceeds typical token limits.", 20));
            
        var request = new EmbeddingRequest
        {
            Text = [longText]
        };

        var result = await Client.RunAsync<EmbeddingResponse>(
            EmbeddingModels.BGE_Base_EN_V1_5,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.NotNull(result.Data[0]);
        
        Logger.LogInformation("Long text ({Length} chars) embedding dimensions: {Dimensions}", 
            longText.Length, result.Data[0].Length);
    }

    [Fact]
    public async Task RunRawAsync_WithEmbedding_ReturnsHttpResponse()
    {
        var request = new EmbeddingRequest
        {
            Text = ["Simple test text for raw response"]
        };

        using var response = await Client.RunRawAsync(
            EmbeddingModels.BGE_Small_EN_V1_5,
            request);

        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));
        
        Logger.LogInformation("Raw embedding response length: {Length}", content.Length);
        
        // Should contain result with embedding data
        Assert.Contains("result", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("English text for embedding generation")]
    [InlineData("Scientific research in machine learning")]
    [InlineData("Financial markets and economic indicators")]
    [InlineData("Healthcare and medical diagnosis systems")]
    public async Task RunAsync_WithDifferentDomains_GeneratesDistinctEmbeddings(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        
        var request = new EmbeddingRequest
        {
            Text = [text]
        };

        var result = await Client.RunAsync<EmbeddingResponse>(
            EmbeddingModels.BGE_Base_EN_V1_5,
            request);

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.NotNull(result.Data[0]);
        
        Logger.LogInformation("Domain '{Domain}' embedding dimensions: {Dimensions}", 
            text[..Math.Min(20, text.Length)], result.Data[0].Length);
            
        // Verify embedding has reasonable magnitude
        var magnitude = Math.Sqrt(result.Data[0].Sum(x => x * x));
        Assert.True(magnitude > 0, $"Embedding magnitude should be positive: {magnitude}");
    }

    private static double CosineSimilarity(double[] a, double[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length");

        var dotProduct = a.Zip(b, (x, y) => x * y).Sum();
        var magnitudeA = Math.Sqrt(a.Sum(x => x * x));
        var magnitudeB = Math.Sqrt(b.Sum(x => x * x));

        return dotProduct / (magnitudeA * magnitudeB);
    }
}