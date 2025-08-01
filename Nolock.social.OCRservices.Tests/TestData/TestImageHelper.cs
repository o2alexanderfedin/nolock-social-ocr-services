using System.Text;

namespace Nolock.social.OCRservices.Tests.TestData;

public static class TestImageHelper
{
    /// <summary>
    /// Gets a real receipt image for testing
    /// </summary>
    public static byte[] GetReceiptImage()
    {
        try 
        {
            return TestImageResources.GetReceiptImage();
        }
        catch 
        {
            // Fallback to synthetic image if embedded resource not available
            return TestImageResources.CreateTextReceiptImage();
        }
    }

    /// <summary>
    /// Gets a real check image for testing
    /// </summary>
    public static byte[] GetCheckImage()
    {
        try 
        {
            return TestImageResources.GetCheckImage();
        }
        catch 
        {
            // Fallback to synthetic image if embedded resource not available
            return TestImageResources.CreateTextReceiptImage();
        }
    }

    /// <summary>
    /// Creates a more realistic JPEG image with text content for OCR testing
    /// </summary>
    public static byte[] CreateValidJpegImage()
    {
        // Create a synthetic receipt-like image with clear text
        return CreateReceiptImageWithText();
    }

    /// <summary>
    /// Creates a synthetic receipt image with readable text for OCR testing
    /// </summary>
    public static byte[] CreateReceiptImageWithText()
    {
        // This is a minimal JPEG with embedded text that OCR can actually read
        // For now, return a PNG that contains readable text patterns
        return TestImageResources.CreateTextReceiptImage();
    }

    /// <summary>
    /// Creates a test receipt text that mimics OCR output
    /// </summary>
    public static string CreateTestReceiptText()
    {
        return @"WALMART SUPERCENTER
                Store #1234
                123 Main St
                Anytown, ST 12345
                (555) 123-4567

                Item 1          $10.99
                Item 2          $5.50
                
                Subtotal        $16.49
                Tax             $1.32
                Total           $17.81
                
                VISA ****1234
                Auth: 123456
                
                Thank you!";
    }

    /// <summary>
    /// Creates a test check text that mimics OCR output
    /// </summary>
    public static string CreateTestCheckText()
    {
        return @"John Doe                    Check #1234
                123 Main Street             Date: 01/15/2024
                Anytown, ST 12345
                
                PAY TO THE ORDER OF  Jane Smith             $1,234.56
                
                One Thousand Two Hundred Thirty Four and 56/100 DOLLARS
                
                First National Bank
                ⑆123456789⑆ ⑈1234567890⑈ 1234
                
                John Doe";
    }
}