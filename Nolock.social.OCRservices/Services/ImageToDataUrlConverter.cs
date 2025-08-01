using Microsoft.IO;
using Nolock.social.OCRservices.Core.Utils;

namespace Nolock.social.OCRservices.Services;

/// <summary>
/// Implementation of image to data URL conversion service
/// </summary>
public sealed class ImageToDataUrlConverter : IImageToDataUrlConverter
{
    private static readonly RecyclableMemoryStreamManager StreamManager = new();
    private static readonly MimeTypeTrie MimeTrie = BuildMimeTrie();

    public async Task<(string dataUrl, string mimeType)> ConvertAsync(Stream imageStream)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        
        await using var memoryStream = StreamManager.GetStream();
        await imageStream.CopyToAsync(memoryStream).ConfigureAwait(false);
        var imageBytes = memoryStream.ToArray();

        var mimeType = DetectMimeType(imageBytes);
        var base64String = Convert.ToBase64String(imageBytes);
        var dataUrl = $"data:{mimeType};base64,{base64String}";

        return (dataUrl, mimeType);
    }

    private static string DetectMimeType(byte[] bytes)
    {
        return MimeTrie.Search(bytes) ?? "application/octet-stream";
    }

    private static MimeTypeTrie BuildMimeTrie()
    {
        var trie = new MimeTypeTrie();
        
        // Image formats
        trie.Add([0xFF, 0xD8, 0xFF], "image/jpeg");
        trie.Add([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png");
        trie.Add([0x47, 0x49, 0x46, 0x38, 0x37, 0x61], "image/gif");
        trie.Add([0x47, 0x49, 0x46, 0x38, 0x39, 0x61], "image/gif");
        trie.Add([0x52, 0x49, 0x46, 0x46], "image/webp"); // Note: simplified, original had additional check
        trie.Add([0x42, 0x4D], "image/bmp");
        trie.Add([0x00, 0x00, 0x01, 0x00], "image/x-icon");
        trie.Add([0x49, 0x49, 0x2A, 0x00], "image/tiff");
        trie.Add([0x4D, 0x4D, 0x00, 0x2A], "image/tiff");
        
        // PDF
        trie.Add([0x25, 0x50, 0x44, 0x46], "application/pdf");
        
        return trie;
    }
}