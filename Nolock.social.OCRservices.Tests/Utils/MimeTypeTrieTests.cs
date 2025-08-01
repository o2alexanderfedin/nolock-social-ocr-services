using Nolock.social.OCRservices.Core.Utils;

namespace Nolock.social.OCRservices.Tests.Utils;

public class MimeTypeTrieTests
{
    [Fact]
    public void Add_SingleSignature_CanBeSearched()
    {
        var trie = new MimeTypeTrie();
        var jpegSignature = new byte[] { 0xFF, 0xD8 };
        
        trie.Add(jpegSignature, "image/jpeg");
        
        var result = trie.Search(jpegSignature);
        Assert.Equal("image/jpeg", result);
    }
    
    [Fact]
    public void Add_MultipleSignatures_AllCanBeSearched()
    {
        var trie = new MimeTypeTrie();
        var jpegSignature = new byte[] { 0xFF, 0xD8 };
        var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        
        trie.Add(jpegSignature, "image/jpeg");
        trie.Add(pngSignature, "image/png");
        
        Assert.Equal("image/jpeg", trie.Search(jpegSignature));
        Assert.Equal("image/png", trie.Search(pngSignature));
    }
    
    [Fact]
    public void Add_DuplicateSignatureWithSameMimeType_DoesNotThrow()
    {
        var trie = new MimeTypeTrie();
        var jpegSignature = new byte[] { 0xFF, 0xD8 };
        
        trie.Add(jpegSignature, "image/jpeg");
        trie.Add(jpegSignature, "image/jpeg"); // Should not throw
        
        Assert.Equal("image/jpeg", trie.Search(jpegSignature));
    }
    
    [Fact]
    public void Add_DuplicateSignatureWithDifferentMimeType_ThrowsException()
    {
        var trie = new MimeTypeTrie();
        var jpegSignature = new byte[] { 0xFF, 0xD8 };
        
        trie.Add(jpegSignature, "image/jpeg");
        
        var exception = Assert.Throws<InvalidOperationException>(() =>
            trie.Add(jpegSignature, "image/different"));
        
        Assert.Contains("Attempt to override existing MIME type", exception.Message);
        Assert.Contains("image/jpeg", exception.Message);
        Assert.Contains("image/different", exception.Message);
    }
    
    [Fact]
    public void Search_WithLongerData_FindsMatchingSignature()
    {
        var trie = new MimeTypeTrie();
        var jpegSignature = new byte[] { 0xFF, 0xD8 };
        
        trie.Add(jpegSignature, "image/jpeg");
        
        var testData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }; // JPEG with more data
        var result = trie.Search(testData);
        
        Assert.Equal("image/jpeg", result);
    }
    
    [Fact]
    public void Search_WithNonMatchingData_ReturnsNull()
    {
        var trie = new MimeTypeTrie();
        var jpegSignature = new byte[] { 0xFF, 0xD8 };
        
        trie.Add(jpegSignature, "image/jpeg");
        
        var nonMatchingData = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        var result = trie.Search(nonMatchingData);
        
        Assert.Null(result);
    }
    
    [Fact]
    public void Search_WithEmptyData_ReturnsNull()
    {
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        
        var result = trie.Search(Array.Empty<byte>());
        
        Assert.Null(result);
    }
    
    [Fact]
    public void Search_WithPartialMatch_ReturnsNull()
    {
        var trie = new MimeTypeTrie();
        var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        
        trie.Add(pngSignature, "image/png");
        
        var partialData = new byte[] { 0x89, 0x50 }; // Only first 2 bytes of PNG
        var result = trie.Search(partialData);
        
        Assert.Null(result);
    }
    
    [Fact]
    public void GetAllMimeTypes_ReturnsAllAddedMimeTypes()
    {
        var trie = new MimeTypeTrie();
        
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
        trie.Add(new byte[] { 0x47, 0x49, 0x46 }, "image/gif");
        
        var mimeTypes = trie.GetAllMimeTypes().ToList();
        
        Assert.Equal(3, mimeTypes.Count);
        Assert.Contains("image/jpeg", mimeTypes);
        Assert.Contains("image/png", mimeTypes);
        Assert.Contains("image/gif", mimeTypes);
    }
    
    [Fact]
    public void GetAllMimeTypes_WithNoEntries_ReturnsEmpty()
    {
        var trie = new MimeTypeTrie();
        
        var mimeTypes = trie.GetAllMimeTypes().ToList();
        
        Assert.Empty(mimeTypes);
    }
    
    [Fact]
    public void Search_WithOverlappingSignatures_FindsLongestMatch()
    {
        var trie = new MimeTypeTrie();
        
        // Add shorter signature first
        trie.Add(new byte[] { 0x00, 0x00 }, "format/short");
        // Add longer signature with same prefix
        trie.Add(new byte[] { 0x00, 0x00, 0x00, 0x18 }, "format/long");
        
        // Test with data matching the longer signature
        var testData = new byte[] { 0x00, 0x00, 0x00, 0x18, 0xFF };
        var result = trie.Search(testData);
        
        Assert.Equal("format/long", result);
        
        // Test with data matching only the shorter signature
        var shortData = new byte[] { 0x00, 0x00, 0xFF, 0xFF };
        var shortResult = trie.Search(shortData);
        
        Assert.Equal("format/short", shortResult);
    }
}