using System.Reflection;

namespace Nolock.social.MistralOcr.IntegrationTests.Helpers;

public static class TestImageHelper
{
    private static readonly Assembly Assembly = typeof(TestImageHelper).Assembly;
    private static readonly string ResourcePrefix = "Nolock.social.MistralOcr.IntegrationTests.TestImages";

    public static byte[] GetReceiptImageBytes(int receiptNumber)
    {
        if (receiptNumber < 1 || receiptNumber > 5)
            throw new ArgumentOutOfRangeException(nameof(receiptNumber), "Receipt number must be between 1 and 5");

        var resourceName = $"{ResourcePrefix}.receipt{receiptNumber}.jpg";
        return GetEmbeddedResourceBytes(resourceName);
    }

    public static Stream GetReceiptImageStream(int receiptNumber)
    {
        if (receiptNumber < 1 || receiptNumber > 5)
            throw new ArgumentOutOfRangeException(nameof(receiptNumber), "Receipt number must be between 1 and 5");

        var resourceName = $"{ResourcePrefix}.receipt{receiptNumber}.jpg";
        return GetEmbeddedResourceStream(resourceName);
    }

    public static string GetReceiptImageDataUrl(int receiptNumber)
    {
        var bytes = GetReceiptImageBytes(receiptNumber);
        var base64 = Convert.ToBase64String(bytes);
        return $"data:image/jpeg;base64,{base64}";
    }

    public static List<(int Number, string FileName)> GetAllReceiptInfo()
    {
        return new List<(int, string)>
        {
            (1, "receipt1.jpg"),
            (2, "receipt2.jpg"),
            (3, "receipt3.jpg"),
            (4, "receipt4.jpg"),
            (5, "receipt5.jpg")
        };
    }

    private static byte[] GetEmbeddedResourceBytes(string resourceName)
    {
        using var stream = GetEmbeddedResourceStream(resourceName);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static Stream GetEmbeddedResourceStream(string resourceName)
    {
        var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            var availableResources = Assembly.GetManifestResourceNames();
            throw new InvalidOperationException(
                $"Resource '{resourceName}' not found. Available resources: {string.Join(", ", availableResources)}");
        }
        return stream;
    }
}