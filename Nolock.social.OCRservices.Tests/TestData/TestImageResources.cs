using System.Reflection;

namespace Nolock.social.OCRservices.Tests.TestData;

public static class TestImageResources
{
    /// <summary>
    /// Gets embedded receipt image as byte array
    /// </summary>
    public static byte[] GetReceiptImage()
    {
        return GetEmbeddedResource("receipt1.jpg");
    }

    /// <summary>
    /// Gets embedded check image as byte array
    /// </summary>
    public static byte[] GetCheckImage()
    {
        return GetEmbeddedResource("check1.jpg");
    }

    /// <summary>
    /// Creates a more substantial test image (larger than 1x1 pixel)
    /// This is a 100x100 white image with some text-like patterns
    /// </summary>
    public static byte[] CreateTextReceiptImage()
    {
        // Create a simple receipt-like image data
        // This is a minimal PNG with white background and black text patterns
        return new byte[]
        {
            // PNG Header
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            
            // IHDR chunk (image header) - 100x100 pixels, 8-bit grayscale
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x64, // 100x100
            0x08, 0x00, 0x00, 0x00, 0x00, 0x7D, 0x3E, 0x32, 0x8D,
            
            // IDAT chunk (image data) - compressed white image with black text patterns
            0x00, 0x00, 0x00, 0x22, 0x49, 0x44, 0x41, 0x54,
            0x78, 0x9C, 0xED, 0xC1, 0x01, 0x01, 0x00, 0x00,
            0x00, 0x80, 0x90, 0xFE, 0xAF, 0x6E, 0x48, 0x40,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x50, 0x20,
            0x05, 0x2F, 0x02, 0x0E, 0xA4, 0x54,
            
            // IEND chunk (end of PNG)
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82
        };
    }

    /// <summary>
    /// Gets embedded resource as byte array
    /// </summary>
    private static byte[] GetEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fullResourceName = $"Nolock.social.OCRservices.Tests.TestData.Images.{resourceName}";
        
        using var stream = assembly.GetManifestResourceStream(fullResourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{fullResourceName}' not found. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }
        
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Gets all available embedded resource names for debugging
    /// </summary>
    public static string[] GetAvailableResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceNames();
    }
}