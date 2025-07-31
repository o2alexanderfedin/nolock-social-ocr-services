using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Nolock.social.OCRservices.Utils;

namespace Nolock.social.OCRservices.Pipelines;

public class PipelineNodeImageToUrl : IPipelineNode<Stream, Uri>
{
    private static readonly MimeTypeTrie MimeTrie = BuildMimeTrie();
    
    private static MimeTypeTrie BuildMimeTrie()
    {
        var trie = new MimeTypeTrie();
        trie.Add([0xFF, 0xD8], "image/jpeg");        // JPEG
        trie.Add([0x89, 0x50, 0x4E, 0x47], "image/png"); // PNG
        trie.Add("GIF"u8.ToArray(), "image/gif");    // GIF
        trie.Add("BM"u8.ToArray(), "image/bmp");          // BMP
        trie.Add([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], "image/heic"); // HEIC
        return trie;
    }

    private readonly IPipelineNode<Stream, Uri> _impl = PipelineNodeRelay.Create<Stream, Uri>(async stream =>
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var mime = DetectMimeType(bytes);
        var base64 = Convert.ToBase64String(bytes);
        var dataUrl = $"data:{mime};base64,{base64}";

        return new Uri(dataUrl);
    });

    public ValueTask<Uri> ProcessAsync(Stream input)
        => _impl.ProcessAsync(input);

    private static string DetectMimeType(byte[] bytes)
    {
        var mimeType = MimeTrie.Search(bytes);
        if (mimeType != null)
            return mimeType;
        
        var supportedFormats = string.Join(", ", MimeTrie.GetAllMimeTypes());
        throw new NotSupportedException($"Unable to detect MIME type from the provided data. Supported formats: {supportedFormats}");
    }
}