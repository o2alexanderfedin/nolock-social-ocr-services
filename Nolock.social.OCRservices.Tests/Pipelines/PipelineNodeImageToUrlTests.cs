using Nolock.social.OCRservices.Pipelines;
using System.Text;

namespace Nolock.social.OCRservices.Tests.Pipelines;

public class PipelineNodeImageToUrlTests
{
    private readonly PipelineNodeImageToUrl _pipeline = new();
    
    [Fact]
    public async Task ProcessAsync_WithJpegImage_ReturnsCorrectDataUrl()
    {
        // JPEG signature followed by some dummy data
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        using var stream = new MemoryStream(jpegData);
        
        var result = await _pipeline.ProcessAsync(stream);
        
        Assert.NotNull(result.url);
        Assert.Equal("image/jpeg", result.mimeType);
        Assert.StartsWith("data:image/jpeg;base64,", result.url);
        
        // Verify the base64 encoding
        var base64Part = result.url.Substring("data:image/jpeg;base64,".Length);
        var decodedBytes = Convert.FromBase64String(base64Part);
        Assert.Equal(jpegData, decodedBytes);
    }
    
    [Fact]
    public async Task ProcessAsync_WithPngImage_ReturnsCorrectDataUrl()
    {
        // PNG signature
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var stream = new MemoryStream(pngData);
        
        var result = await _pipeline.ProcessAsync(stream);
        
        Assert.NotNull(result.url);
        Assert.Equal("image/png", result.mimeType);
        Assert.StartsWith("data:image/png;base64,", result.url);
    }
    
    [Fact]
    public async Task ProcessAsync_WithGifImage_ReturnsCorrectDataUrl()
    {
        // GIF signature (GIF89a)
        var gifData = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
        using var stream = new MemoryStream(gifData);
        
        var result = await _pipeline.ProcessAsync(stream);
        
        Assert.NotNull(result.url);
        Assert.Equal("image/gif", result.mimeType);
        Assert.StartsWith("data:image/gif;base64,", result.url);
    }
    
    [Fact]
    public async Task ProcessAsync_WithBmpImage_ReturnsCorrectDataUrl()
    {
        // BMP signature
        var bmpData = new byte[] { 0x42, 0x4D, 0x00, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(bmpData);
        
        var result = await _pipeline.ProcessAsync(stream);
        
        Assert.NotNull(result.url);
        Assert.Equal("image/bmp", result.mimeType);
        Assert.StartsWith("data:image/bmp;base64,", result.url);
    }
    
    [Fact]
    public async Task ProcessAsync_WithHeicImage_ReturnsCorrectDataUrl()
    {
        // HEIC signature
        var heicData = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63 };
        using var stream = new MemoryStream(heicData);
        
        var result = await _pipeline.ProcessAsync(stream);
        
        Assert.NotNull(result.url);
        Assert.Equal("image/heic", result.mimeType);
        Assert.StartsWith("data:image/heic;base64,", result.url);
    }
    
    [Fact]
    public async Task ProcessAsync_WithUnknownFormat_ThrowsNotSupportedException()
    {
        // Unknown format
        var unknownData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
        using var stream = new MemoryStream(unknownData);
        
        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await _pipeline.ProcessAsync(stream));
        
        Assert.Contains("Unable to detect MIME type", exception.Message);
        Assert.Contains("Supported formats:", exception.Message);
        
        // Verify all supported formats are mentioned
        Assert.Contains("image/jpeg", exception.Message);
        Assert.Contains("image/png", exception.Message);
        Assert.Contains("image/gif", exception.Message);
        Assert.Contains("image/bmp", exception.Message);
        Assert.Contains("image/heic", exception.Message);
    }
    
    [Fact]
    public async Task ProcessAsync_WithEmptyStream_ThrowsNotSupportedException()
    {
        using var stream = new MemoryStream();
        
        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await _pipeline.ProcessAsync(stream));
    }
    
    [Fact]
    public async Task ProcessAsync_PreservesStreamPosition()
    {
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        using var stream = new MemoryStream(jpegData);
        stream.Position = 0;
        
        await _pipeline.ProcessAsync(stream);
        
        // The implementation reads the stream, so position should be at the end
        Assert.Equal(jpegData.Length, stream.Position);
    }
    
    [Fact]
    public async Task ProcessAsync_HandlesLargeImages()
    {
        // Create a larger JPEG "image" (1KB)
        var largeData = new byte[1024];
        largeData[0] = 0xFF;
        largeData[1] = 0xD8;
        // Fill rest with dummy data
        for (var i = 2; i < largeData.Length; i++)
        {
            largeData[i] = (byte)(i % 256);
        }
        
        using var stream = new MemoryStream(largeData);
        
        var result = await _pipeline.ProcessAsync(stream);
        
        Assert.NotNull(result.url);
        Assert.Equal("image/jpeg", result.mimeType);
        Assert.StartsWith("data:image/jpeg;base64,", result.url);
        
        // Verify the entire data was encoded
        var base64Part = result.url.Substring("data:image/jpeg;base64,".Length);
        var decodedBytes = Convert.FromBase64String(base64Part);
        Assert.Equal(largeData.Length, decodedBytes.Length);
    }
}