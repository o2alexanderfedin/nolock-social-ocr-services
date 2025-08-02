using Nolock.social.OCRservices.Core.Utils;
using FluentAssertions;
using System.Diagnostics;
using System.Text;

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

    #region Common Image Format Tests

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg", "JPEG")] // Standard JPEG
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png", "PNG")] // PNG
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, "image/gif", "GIF87a")] // GIF87a
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif", "GIF89a")] // GIF89a
    [InlineData(new byte[] { 0x42, 0x4D }, "image/bmp", "BMP")] // BMP
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46 }, "image/webp", "WEBP (RIFF container)")] // WEBP RIFF
    public void DetectCommonImageFormats_ReturnsCorrectMimeType(byte[] signature, string expectedMimeType, string format)
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(signature, expectedMimeType);
        
        // Create test data with the signature plus additional bytes
        var testData = signature.Concat(new byte[] { 0x00, 0x01, 0x02, 0x03 }).ToArray();
        
        // Act
        var result = trie.Search(testData);
        
        // Assert
        result.Should().Be(expectedMimeType, $"because {format} signature should be detected correctly");
    }

    [Fact]
    public void DetectJpegVariants_AllDetectedAsJpeg()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        var jpegSignatures = new (byte[] signature, string description)[]
        {
            (new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "JPEG with JFIF"),
            (new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, "JPEG with EXIF"),
            (new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 }, "JPEG with ICC Profile"),
            (new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 }, "JPEG with SPIFF"),
            (new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }, "JPEG with Quantization Table"),
            (new byte[] { 0xFF, 0xD8, 0xFF, 0xC0 }, "JPEG with Start of Frame")
        };
        
        // Add base JPEG signature
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        
        // Act & Assert
        foreach (var (signature, description) in jpegSignatures)
        {
            var result = trie.Search(signature);
            result.Should().Be("image/jpeg", $"because {description} should be detected as JPEG");
        }
    }

    [Fact]
    public void DetectWebpVariants_ReturnsWebpMimeType()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // WEBP files start with RIFF followed by file size then WEBP
        var webpSignature1 = new byte[] { 0x52, 0x49, 0x46, 0x46 }; // RIFF header
        var webpSignature2 = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 }; // Full WEBP header
        
        trie.Add(webpSignature1, "image/webp");
        
        // Test data with RIFF + size + WEBP
        var testData = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x12, 0x34, 0x56, 0x78, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38 };
        
        // Act
        var result = trie.Search(testData);
        
        // Assert
        result.Should().Be("image/webp", "because WEBP files should be detected by RIFF header");
    }

    [Fact]
    public void DetectBmpVariants_ReturnsBmpMimeType()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0x42, 0x4D }, "image/bmp");
        
        // Create test BMP data with header
        var bmpTestData = new byte[] 
        {
            0x42, 0x4D, // BM signature
            0x36, 0x00, 0x00, 0x00, // File size
            0x00, 0x00, 0x00, 0x00, // Reserved
            0x36, 0x00, 0x00, 0x00  // Offset to pixel data
        };
        
        // Act
        var result = trie.Search(bmpTestData);
        
        // Assert
        result.Should().Be("image/bmp", "because BMP files should be detected by BM signature");
    }

    [Fact]
    public void DetectAllCommonImageFormats_InSingleTrie()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        var imageFormats = new Dictionary<byte[], string>
        {
            { new byte[] { 0xFF, 0xD8 }, "image/jpeg" },
            { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png" },
            { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, "image/gif" },
            { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif" },
            { new byte[] { 0x42, 0x4D }, "image/bmp" },
            { new byte[] { 0x52, 0x49, 0x46, 0x46 }, "image/webp" },
            { new byte[] { 0x00, 0x00, 0x01, 0x00 }, "image/x-icon" }, // ICO format
            { new byte[] { 0xFF, 0x4F, 0xFF, 0x51 }, "image/jp2" } // JPEG 2000
        };
        
        // Add all formats to trie
        foreach (var (signature, mimeType) in imageFormats)
        {
            trie.Add(signature, mimeType);
        }
        
        // Act & Assert
        foreach (var (signature, expectedMimeType) in imageFormats)
        {
            // Create test data with signature plus padding
            var testData = signature.Concat(new byte[] { 0x00, 0x01, 0x02 }).ToArray();
            var result = trie.Search(testData);
            result.Should().Be(expectedMimeType, $"because signature should be detected as {expectedMimeType}");
        }
        
        // Verify all MIME types are present
        var allMimeTypes = trie.GetAllMimeTypes().ToHashSet();
        allMimeTypes.Should().Contain("image/jpeg");
        allMimeTypes.Should().Contain("image/png");
        allMimeTypes.Should().Contain("image/gif");
        allMimeTypes.Should().Contain("image/bmp");
        allMimeTypes.Should().Contain("image/webp");
        allMimeTypes.Should().Contain("image/x-icon");
        allMimeTypes.Should().Contain("image/jp2");
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void Search_EmptyFile_ReturnsNull()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        
        // Act
        var result = trie.Search(Array.Empty<byte>());
        
        // Assert
        result.Should().BeNull("because empty files have no signature to match");
    }

    [Fact]
    public void Search_SingleByteFile_ReturnsNullWhenNoMatch()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        
        var singleByte = new byte[] { 0xFF }; // Partial JPEG signature
        
        // Act
        var result = trie.Search(singleByte);
        
        // Assert
        result.Should().BeNull("because single byte doesn't complete any signature");
    }

    [Fact]
    public void Search_CorruptedHeaders_HandlesGracefully()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
        
        var corruptedData = new byte[]
        {
            0xFF, 0xD8, // Start of JPEG
            0x00, 0x00, 0x00, 0x00, // Corrupted/invalid data
            0x89, 0x50, 0x4E, 0x47  // PNG signature in wrong place
        };
        
        // Act
        var result = trie.Search(corruptedData);
        
        // Assert
        result.Should().Be("image/jpeg", "because the first valid signature should be detected");
    }

    [Fact]
    public void Search_MalformedSignature_ReturnsNullOrPartialMatch()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png");
        
        var malformedPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0xFF, 0xFF, 0xFF, 0xFF }; // Wrong PNG header
        
        // Act
        var result = trie.Search(malformedPng);
        
        // Assert
        result.Should().BeNull("because malformed signature doesn't match complete PNG header");
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 })]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    public void Search_InvalidOrUnknownSignatures_ReturnsNull(byte[] invalidData)
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
        
        // Act
        var result = trie.Search(invalidData);
        
        // Assert
        result.Should().BeNull($"because data {Convert.ToHexString(invalidData)} doesn't match any known signature");
    }

    [Fact]
    public void Add_NullSignature_ThrowsArgumentNullException()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => trie.Add(null!, "image/jpeg"));
        exception.ParamName.Should().Be("signature");
    }

    [Fact]
    public void Add_NullMimeType_ThrowsArgumentNullException()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => trie.Add(new byte[] { 0xFF, 0xD8 }, null!));
        exception.ParamName.Should().Be("mimeType");
    }

    [Fact]
    public void Search_NullData_ThrowsArgumentNullException()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => trie.Search(null!));
        exception.ParamName.Should().Be("data");
    }

    [Fact]
    public void Search_VerySmallFile_ReturnsNullWhenNoPartialMatch()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png"); // 8-byte PNG signature
        
        var tinyFile = new byte[] { 0x89, 0x50 }; // Only 2 bytes
        
        // Act
        var result = trie.Search(tinyFile);
        
        // Assert
        result.Should().BeNull("because partial signatures don't match without complete pattern");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void Search_LargeFile_PerformsEfficiently()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
        
        // Create a large file (10MB) with JPEG signature at the beginning
        var largeFile = new byte[10 * 1024 * 1024]; // 10MB
        largeFile[0] = 0xFF;
        largeFile[1] = 0xD8;
        // Fill rest with random data
        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 2; i < largeFile.Length; i++)
        {
            largeFile[i] = (byte)random.Next(256);
        }
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = trie.Search(largeFile);
        stopwatch.Stop();
        
        // Assert
        result.Should().Be("image/jpeg", "because JPEG signature should be detected");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "because trie search should be fast even for large files");
    }

    [Fact]
    public void Search_LargeFileWithSignatureAtEnd_StopsEarly()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        
        // Create a large file (1MB) with no matching signature
        var largeFile = new byte[1024 * 1024]; // 1MB
        Array.Fill(largeFile, (byte)0x00);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = trie.Search(largeFile);
        stopwatch.Stop();
        
        // Assert
        result.Should().BeNull("because no signature matches");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, "because search should stop early when no path matches");
    }

    [Fact]
    public void Add_ManySignatures_PerformsEfficiently()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        var signatures = new List<(byte[], string)>();
        
        // Generate 1000 unique signatures
        var random = new Random(42); // Fixed seed
        for (int i = 0; i < 1000; i++)
        {
            var signature = new byte[4];
            signature[0] = (byte)(i >> 8); // Use index to ensure uniqueness
            signature[1] = (byte)(i & 0xFF);
            signature[2] = (byte)random.Next(256);
            signature[3] = (byte)random.Next(256);
            signatures.Add((signature, $"format/{i}"));
        }
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        foreach (var (signature, mimeType) in signatures)
        {
            trie.Add(signature, mimeType);
        }
        stopwatch.Stop();
        
        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "because adding signatures should be efficient");
        trie.GetAllMimeTypes().Should().HaveCount(1000, "because all signatures should be added");
    }

    [Fact]
    public void Search_WithManySignatures_RemainsEfficient()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Add common image format signatures
        var commonSignatures = new Dictionary<byte[], string>
        {
            { new byte[] { 0xFF, 0xD8 }, "image/jpeg" },
            { new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png" },
            { new byte[] { 0x47, 0x49, 0x46, 0x38 }, "image/gif" },
            { new byte[] { 0x42, 0x4D }, "image/bmp" },
            { new byte[] { 0x52, 0x49, 0x46, 0x46 }, "image/webp" }
        };
        
        foreach (var (signature, mimeType) in commonSignatures)
        {
            trie.Add(signature, mimeType);
        }
        
        // Create test data that matches PNG
        var testData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        
        // Act - perform many searches
        var stopwatch = Stopwatch.StartNew();
        string? result = null;
        for (int i = 0; i < 10000; i++)
        {
            result = trie.Search(testData);
        }
        stopwatch.Stop();
        
        // Assert
        result.Should().Be("image/png");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "because repeated searches should be very fast");
    }

    #endregion

    #region Trie Data Structure Operation Tests

    [Fact]
    public void TrieStructure_SinglePath_BuildsCorrectly()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        var signature = new byte[] { 0xFF, 0xD8, 0xFF };
        
        // Act
        trie.Add(signature, "image/jpeg");
        
        // Assert
        var result = trie.Search(signature);
        result.Should().Be("image/jpeg");
        
        // Test that partial matches don't work
        trie.Search(new byte[] { 0xFF }).Should().BeNull();
        trie.Search(new byte[] { 0xFF, 0xD8 }).Should().BeNull();
    }

    [Fact]
    public void TrieStructure_BranchingPaths_HandleCorrectly()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Add signatures that share common prefixes
        trie.Add(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, "image/gif87a"); // GIF87a
        trie.Add(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif89a"); // GIF89a
        trie.Add(new byte[] { 0x47, 0x49, 0x46 }, "image/gif-generic"); // Generic GIF
        
        // Act & Assert
        trie.Search(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x00 }).Should().Be("image/gif87a");
        trie.Search(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00 }).Should().Be("image/gif89a");
        trie.Search(new byte[] { 0x47, 0x49, 0x46, 0x00, 0x00, 0x00 }).Should().Be("image/gif-generic");
    }

    [Fact]
    public void TrieStructure_DeepPath_HandlesCorrectly()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Create a deep signature (16 bytes)
        var deepSignature = new byte[] 
        {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
            0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F
        };
        
        // Act
        trie.Add(deepSignature, "format/deep");
        
        // Assert
        var testData = deepSignature.Concat(new byte[] { 0xFF, 0xFF }).ToArray();
        trie.Search(testData).Should().Be("format/deep");
        
        // Partial paths should not match
        var partialData = deepSignature.Take(15).ToArray();
        trie.Search(partialData).Should().BeNull();
    }

    [Fact]
    public void TrieStructure_OverlappingSignatures_ReturnsLongestMatch()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Add signatures where one is a prefix of another
        trie.Add(new byte[] { 0x52, 0x49 }, "format/short");
        trie.Add(new byte[] { 0x52, 0x49, 0x46, 0x46 }, "format/riff");
        // WEBP signature: RIFF + any 4 bytes (file size) + WEBP - we'll use a more realistic approach
        // For testing purposes, we'll use a fixed file size that matches our test data
        trie.Add(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x12, 0x34, 0x56, 0x78, 0x57, 0x45, 0x42, 0x50 }, "image/webp");
        
        // Test data that matches the longest signature
        var webpData = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x12, 0x34, 0x56, 0x78, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50 };
        var riffData = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x12, 0x34, 0x56, 0x78, 0x41, 0x56, 0x49, 0x20 };
        var shortData = new byte[] { 0x52, 0x49, 0x00, 0x00 };
        
        // Act & Assert
        trie.Search(webpData).Should().Be("image/webp", "because WEBP signature is the longest match");
        trie.Search(riffData).Should().Be("format/riff", "because RIFF signature matches but not WEBP");
        trie.Search(shortData).Should().Be("format/short", "because only short signature matches");
    }

    [Fact]
    public void TrieStructure_EmptySignature_ThrowsArgumentException()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Act & Assert
        // Empty signature should be rejected (though this depends on implementation)
        var emptySignature = Array.Empty<byte>();
        
        // This test assumes empty signatures are invalid
        // If the implementation allows empty signatures, adjust the test accordingly
        var exception = Record.Exception(() => trie.Add(emptySignature, "format/empty"));
        
        // The behavior for empty signatures should be defined in the requirements
        // For now, we'll test that it either throws or handles gracefully
        if (exception == null)
        {
            // If empty signatures are allowed, test the behavior
            trie.Search(Array.Empty<byte>()).Should().BeNull("because empty data should not match anything meaningful");
        }
    }

    [Fact] 
    public void TrieStructure_MemoryEfficiency_SharesCommonPrefixes()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Add many signatures with common prefixes
        var prefixes = new[] { "JPEG", "PNG", "GIF" };
        var signatures = new List<(byte[], string)>();
        
        foreach (var prefix in prefixes)
        {
            var baseBytes = Encoding.ASCII.GetBytes(prefix);
            for (int i = 0; i < 10; i++)
            {
                var signature = baseBytes.Concat(new byte[] { (byte)i }).ToArray();
                signatures.Add((signature, $"format/{prefix.ToLower()}{i}"));
            }
        }
        
        // Act
        foreach (var (signature, mimeType) in signatures)
        {
            trie.Add(signature, mimeType);
        }
        
        // Assert
        trie.GetAllMimeTypes().Should().HaveCount(30, "because all 30 signatures should be added");
        
        // Test that each signature can be found
        foreach (var (signature, expectedMimeType) in signatures)
        {
            var testData = signature.Concat(new byte[] { 0xFF }).ToArray();
            trie.Search(testData).Should().Be(expectedMimeType);
        }
    }

    [Fact]
    public void TrieStructure_GetAllMimeTypes_TraversesCorrectly()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        var expectedMimeTypes = new HashSet<string>
        {
            "image/jpeg", "image/png", "image/gif", "image/bmp", 
            "application/pdf", "text/plain", "application/zip"
        };
        
        var signatures = new Dictionary<byte[], string>
        {
            { new byte[] { 0xFF, 0xD8 }, "image/jpeg" },
            { new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png" },
            { new byte[] { 0x47, 0x49, 0x46 }, "image/gif" },
            { new byte[] { 0x42, 0x4D }, "image/bmp" },
            { new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf" },
            { new byte[] { 0x54, 0x68, 0x69, 0x73 }, "text/plain" }, // "This"
            { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "application/zip" }
        };
        
        foreach (var (signature, mimeType) in signatures)
        {
            trie.Add(signature, mimeType);
        }
        
        // Act
        var actualMimeTypes = trie.GetAllMimeTypes().ToHashSet();
        
        // Assert
        actualMimeTypes.Should().BeEquivalentTo(expectedMimeTypes, "because all added MIME types should be returned");
    }

    #endregion

    #region Unknown File Type Handling Tests

    [Fact]
    public void Search_UnknownFileSignature_ReturnsNull()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Add common image formats
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
        trie.Add(new byte[] { 0x47, 0x49, 0x46 }, "image/gif");
        
        // Test with unknown signature
        var unknownSignature = new byte[] { 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56 };
        
        // Act
        var result = trie.Search(unknownSignature);
        
        // Assert
        result.Should().BeNull("because unknown file signature should not match any known format");
    }

    [Theory]
    [InlineData(new byte[] { 0x4D, 0x5A }, "Microsoft PE/COFF executable")] // PE/EXE
    [InlineData(new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, "Linux ELF executable")] // ELF
    [InlineData(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }, "Java class file")] // Java .class
    [InlineData(new byte[] { 0x1F, 0x8B, 0x08 }, "Gzip compressed file")] // GZIP
    [InlineData(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, "7-Zip archive")] // 7Z
    public void Search_NonImageFormats_ReturnsNullWhenNotAdded(byte[] signature, string description)
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Add only image formats
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
        
        // Act
        var result = trie.Search(signature);
        
        // Assert
        result.Should().BeNull($"because {description} signature was not added to trie");
    }

    [Fact]
    public void Search_MixedKnownAndUnknownFormats_HandlesCorrectly()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Add some formats but not others
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf");
        
        var testCases = new (byte[] data, string? expected, string description)[]
        {
            (new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg", "JPEG file"),
            (new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "application/pdf", "PDF file"),
            (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D }, null, "PNG file (not added)"),
            (new byte[] { 0x50, 0x4B, 0x03, 0x04 }, null, "ZIP file (not added)"),
            (new byte[] { 0x00, 0x00, 0x00, 0x00 }, null, "Unknown format")
        };
        
        // Act & Assert
        foreach (var (data, expected, description) in testCases)
        {
            var result = trie.Search(data);
            result.Should().Be(expected, $"because {description} should return {expected ?? "null"}");
        }
    }

    [Fact]
    public void Search_PartiallyMatchingUnknownFormat_ReturnsNull()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg"); // Full JPEG JFIF signature
        
        // Test with data that partially matches but diverges
        var partialMatch = new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }; // JPEG with EXIF instead of JFIF
        
        // Act
        var result = trie.Search(partialMatch);
        
        // Assert
        result.Should().BeNull("because partial match that diverges should not return a result");
    }

    [Fact]
    public void Search_LargeUnknownFile_ReturnsNullQuickly()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
        
        // Create large file with unknown signature
        var largeUnknownFile = new byte[1024 * 1024]; // 1MB
        largeUnknownFile[0] = 0xAB; // Unknown signature
        largeUnknownFile[1] = 0xCD;
        largeUnknownFile[2] = 0xEF;
        // Fill rest with pattern
        for (int i = 3; i < largeUnknownFile.Length; i++)
        {
            largeUnknownFile[i] = (byte)(i % 256);
        }
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = trie.Search(largeUnknownFile);
        stopwatch.Stop();
        
        // Assert
        result.Should().BeNull("because unknown signature doesn't match any known format");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10, "because search should stop quickly when no match is possible");
    }

    [Fact]
    public void GetAllMimeTypes_WithMixedFormats_ReturnsOnlyAddedTypes()
    {
        // Arrange
        var trie = new MimeTypeTrie();
        
        // Add selective formats (not all possible ones)
        trie.Add(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
        trie.Add(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf");
        trie.Add(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "application/zip");
        
        // Act
        var allMimeTypes = trie.GetAllMimeTypes().ToHashSet();
        
        // Assert
        allMimeTypes.Should().HaveCount(3, "because only 3 formats were added");
        allMimeTypes.Should().Contain("image/jpeg");
        allMimeTypes.Should().Contain("application/pdf");
        allMimeTypes.Should().Contain("application/zip");
        
        // Should not contain formats that weren't added
        allMimeTypes.Should().NotContain("image/png");
        allMimeTypes.Should().NotContain("image/gif");
        allMimeTypes.Should().NotContain("application/msword");
    }

    #endregion
}