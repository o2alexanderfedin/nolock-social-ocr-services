using Moq;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.MistralOcr;
using Nolock.social.OCRservices.Pipelines;
using Xunit;

namespace Nolock.social.OCRservices.Tests.Pipelines;

public class OcrToModelPipelineTests
{
    [Fact]
    public void Constructor_WithNullOcrService_ThrowsArgumentNullException()
    {
        // Arrange
        var mockWorkersAI = new Mock<IWorkersAI>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OcrToModelPipeline(null!, mockWorkersAI.Object));
    }

    [Fact]
    public void Constructor_WithNullWorkersAI_ThrowsArgumentNullException()
    {
        // Arrange
        var mockOcrService = new Mock<IMistralOcrService>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OcrToModelPipeline(mockOcrService.Object, null!));
    }

    [Fact]
    public void Constructor_WithValidServices_CreatesInstance()
    {
        // Arrange
        var mockOcrService = new Mock<IMistralOcrService>();
        var mockWorkersAI = new Mock<IWorkersAI>();

        // Act
        var pipeline = new OcrToModelPipeline(mockOcrService.Object, mockWorkersAI.Object);

        // Assert
        Assert.NotNull(pipeline);
    }
}