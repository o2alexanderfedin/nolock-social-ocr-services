using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

#pragma warning disable CA2000 // Dispose objects before losing scope - HttpResponseMessage is managed by Moq

namespace Nolock.social.MistralOcr.IntegrationTests;

public class ImageUrlToDataUrlTransformerTests
{
    private readonly Mock<ILogger<ImageUrlToDataUrlTransformer>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly ImageUrlToDataUrlTransformer _transformer;

    public ImageUrlToDataUrlTransformerTests()
    {
        _mockLogger = new Mock<ILogger<ImageUrlToDataUrlTransformer>>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        
        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("https://example.com")
        };
        
        _mockHttpClientFactory
            .Setup(x => x.CreateClient("ImageUrlToDataUrlTransformer"))
            .Returns(_httpClient);
        
        _transformer = new ImageUrlToDataUrlTransformer(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task TransformAsync_WithValidImageUrl_ReturnsDataUrl()
    {
        // Arrange
        var imageUrl = "https://example.com/image.jpg";
        var imageBytes = Encoding.UTF8.GetBytes("fake-image-data");
        var expectedDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
                {
                    Headers = { { "Content-Type", "image/jpeg" } }
                }
            });

        // Act
        var result = await _transformer.TransformAsync(imageUrl);

        // Assert
        result.Should().Be(expectedDataUrl);
    }

    [Fact]
    public async Task TransformAsync_WithDataUrl_ReturnsOriginal()
    {
        // Arrange
        var dataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        // Act
        var result = await _transformer.TransformAsync(dataUrl);

        // Assert
        result.Should().Be(dataUrl);
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TransformAsync_WithInvalidUrl_ThrowsArgumentException(string? invalidUrl)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _transformer.TransformAsync(invalidUrl!));
    }

    [Fact]
    public async Task TransformAsync_WithHttpError_ThrowsException()
    {
        // Arrange
        var imageUrl = "https://example.com/notfound.jpg";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                ReasonPhrase = "Not Found"
            });

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => 
            _transformer.TransformAsync(imageUrl));
    }

    [Fact]
    public async Task TransformAsync_DetectsMimeTypeFromUrl()
    {
        // Arrange
        var imageUrl = "https://example.com/image.png";
        var imageBytes = new byte[] { 137, 80, 78, 71 }; // PNG header
        var expectedDataUrl = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
                // No Content-Type header
            });

        // Act
        var result = await _transformer.TransformAsync(imageUrl);

        // Assert
        result.Should().Be(expectedDataUrl);
    }

    [Fact]
    public async Task Transform_ProcessesMultipleUrls()
    {
        // Arrange
        var urls = new[] { "https://example.com/1.jpg", "https://example.com/2.jpg", "https://example.com/3.jpg" };
        var imageBytes = Encoding.UTF8.GetBytes("fake-image");

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
                {
                    Headers = { { "Content-Type", "image/jpeg" } }
                }
            });

        // Act
        var results = await _transformer.Transform(urls.ToObservable()).ToList();

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Should().StartWith("data:image/jpeg;base64,"));
    }

    [Fact]
    public async Task Transform_FiltersEmptyUrls()
    {
        // Arrange
        var urls = new[] { "https://example.com/1.jpg", "", null, "https://example.com/2.jpg" };
        var imageBytes = Encoding.UTF8.GetBytes("fake-image");

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
                {
                    Headers = { { "Content-Type", "image/jpeg" } }
                }
            });

        // Act
        var results = await _transformer.Transform(urls.ToObservable()!).ToList();

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task TransformWithErrors_CapturesSuccessAndFailure()
    {
        // Arrange
        var urls = new[] { "https://example.com/good.jpg", "https://example.com/bad.jpg" };
        var imageBytes = Encoding.UTF8.GetBytes("fake-image");

        _mockHttpHandler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
                {
                    Headers = { { "Content-Type", "image/jpeg" } }
                }
            })
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act
        var results = await _transformer.TransformWithErrors(urls.ToObservable()).ToList();

        // Assert
        results.Should().HaveCount(2);
        
        results[0].Success.Should().BeTrue();
        results[0].DataUrl.Should().NotBeNullOrEmpty();
        results[0].Error.Should().BeNull();
        results[0].DetectedMimeType.Should().Be("image/jpeg");
        
        results[1].Success.Should().BeFalse();
        results[1].DataUrl.Should().BeNull();
        results[1].Error.Should().NotBeNull();
    }

    [Fact]
    public async Task TransformWithErrors_IncludesMetadata()
    {
        // Arrange
        var url = "https://example.com/image.png";
        var imageBytes = new byte[] { 1, 2, 3, 4, 5 };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
                {
                    Headers = 
                    { 
                        { "Content-Type", "image/png" },
                        { "Content-Length", imageBytes.Length.ToString() }
                    }
                }
            });

        // Act
        var results = await _transformer.TransformWithErrors(Observable.Return(url)).ToList();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        
        result.Success.Should().BeTrue();
        result.OriginalUrl.Should().Be(url);
        result.DetectedMimeType.Should().Be("image/png");
        result.ContentLength.Should().Be(imageBytes.Length);
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task TransformWithErrors_DataUrlPassthrough()
    {
        // Arrange
        var dataUrl = "data:image/gif;base64,R0lGODlhAQABAIAAAAUEBAAAACwAAAAAAQABAAACAkQBADs=";

        // Act
        var results = await _transformer.TransformWithErrors(Observable.Return(dataUrl)).ToList();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        
        result.Success.Should().BeTrue();
        result.OriginalUrl.Should().Be(dataUrl);
        result.DataUrl.Should().Be(dataUrl);
        result.ProcessingTime.Should().Be(TimeSpan.Zero);
        result.Error.Should().BeNull();
    }
}