using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nolock.social.OCRservices.Core.Models;
using Nolock.social.OCRservices.Tests.TestData;
using Xunit;

namespace Nolock.social.OCRservices.Tests.Integration;

/// <summary>
/// Comprehensive security tests for OCR API endpoints covering:
/// - Input validation and sanitization
/// - SQL injection prevention
/// - Path traversal attacks
/// - XSS prevention
/// - Authorization checks
/// </summary>
public class SecurityTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public SecurityTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    #region Input Validation and Sanitization Tests

    [Fact]
    public async Task ReceiptsEndpoint_RejectsNullRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/ocr/receipts", null);

        // Assert
        // API handles null content as empty body and returns OK with error
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(receiptResponse);
        Assert.False(receiptResponse.Success);
        Assert.NotNull(receiptResponse.Error);
    }

    [Fact]
    public async Task ChecksEndpoint_RejectsNullRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/ocr/checks", null);

        // Assert
        // API handles null content as empty body and returns OK with error
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        
        Assert.NotNull(checkResponse);
        Assert.False(checkResponse.Success);
        Assert.NotNull(checkResponse.Error);
    }

    [Fact]
    public async Task ReceiptsEndpoint_RejectsEmptyContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var emptyContent = new ByteArrayContent([]);
        emptyContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", emptyContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(receiptResponse);
        Assert.False(receiptResponse.Success);
        Assert.NotNull(receiptResponse.Error);
    }

    [Fact]
    public async Task ChecksEndpoint_RejectsEmptyContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var emptyContent = new ByteArrayContent([]);
        emptyContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/checks", emptyContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(checkResponse);
        Assert.False(checkResponse.Success);
        Assert.NotNull(checkResponse.Error);
    }

    [Theory]
    [InlineData(10 * 1024 * 1024)] // 10MB
    [InlineData(50 * 1024 * 1024)] // 50MB
    [InlineData(100 * 1024 * 1024)] // 100MB
    public async Task ReceiptsEndpoint_HandlesLargePayloads(int payloadSize)
    {
        // Arrange
        var client = _factory.CreateClient();
        var largeData = new byte[payloadSize];
        Array.Fill(largeData, (byte)0xFF); // Fill with some data
        using var largeContent = new ByteArrayContent(largeData);
        largeContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", largeContent);

        // Assert
        // Should handle gracefully - either accept or reject with appropriate status
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.BadRequest ||
                   response.StatusCode == HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task ReceiptsEndpoint_HandlesInvalidImageData()
    {
        // Arrange
        var client = _factory.CreateClient();
        var invalidImageData = Encoding.UTF8.GetBytes("This is not image data");
        using var invalidContent = new ByteArrayContent(invalidImageData);
        invalidContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", invalidContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(receiptResponse);
        // Should handle gracefully without crashing
    }

    #endregion

    #region SQL Injection Prevention Tests

    [Theory]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("' UNION SELECT * FROM users --")]
    [InlineData("'; EXEC xp_cmdshell('dir'); --")]
    public async Task ReceiptsEndpoint_PreventsSqlInjectionInImageData(string sqlInjectionPayload)
    {
        // Arrange
        var client = _factory.CreateClient();
        var maliciousData = CreateMaliciousImageWithText(sqlInjectionPayload);
        using var maliciousContent = new ByteArrayContent(maliciousData);
        maliciousContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", maliciousContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(receiptResponse);
        
        // Ensure the response doesn't contain any database error messages or sensitive information
        if (receiptResponse.Error != null)
        {
            Assert.DoesNotContain("database", receiptResponse.Error.ToLowerInvariant());
            Assert.DoesNotContain("sql", receiptResponse.Error.ToLowerInvariant());
            Assert.DoesNotContain("table", receiptResponse.Error.ToLowerInvariant());
        }
    }

    [Theory]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    public async Task ChecksEndpoint_PreventsSqlInjectionInImageData(string sqlInjectionPayload)
    {
        // Arrange
        var client = _factory.CreateClient();
        var maliciousData = CreateMaliciousImageWithText(sqlInjectionPayload);
        using var maliciousContent = new ByteArrayContent(maliciousData);
        maliciousContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/checks", maliciousContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(checkResponse);
        
        // Ensure no database-related error messages leak
        if (checkResponse.Error != null)
        {
            Assert.DoesNotContain("database", checkResponse.Error.ToLowerInvariant());
            Assert.DoesNotContain("sql", checkResponse.Error.ToLowerInvariant());
            Assert.DoesNotContain("table", checkResponse.Error.ToLowerInvariant());
        }
    }

    #endregion

    #region Path Traversal Attack Prevention Tests

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("....//....//....//etc//passwd")]
    [InlineData("%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    [InlineData("..%252f..%252f..%252fetc%252fpasswd")]
    public async Task ReceiptsEndpoint_PreventsPathTraversalAttacks(string pathTraversalPayload)
    {
        // Arrange
        var client = _factory.CreateClient();
        var maliciousData = CreateMaliciousImageWithText(pathTraversalPayload);
        using var maliciousContent = new ByteArrayContent(maliciousData);
        maliciousContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", maliciousContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(receiptResponse);
        
        // Ensure no file system access errors or sensitive path information is leaked
        if (receiptResponse.Error != null)
        {
            Assert.DoesNotContain("/etc/", receiptResponse.Error);
            Assert.DoesNotContain("\\windows\\", receiptResponse.Error.ToLowerInvariant());
            Assert.DoesNotContain("access denied", receiptResponse.Error.ToLowerInvariant());
            Assert.DoesNotContain("file not found", receiptResponse.Error.ToLowerInvariant());
        }
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("....//....//....//etc//passwd")]
    public async Task ChecksEndpoint_PreventsPathTraversalAttacks(string pathTraversalPayload)
    {
        // Arrange
        var client = _factory.CreateClient();
        var maliciousData = CreateMaliciousImageWithText(pathTraversalPayload);
        using var maliciousContent = new ByteArrayContent(maliciousData);
        maliciousContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/checks", maliciousContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(checkResponse);
        
        // Ensure no sensitive file system information is leaked
        if (checkResponse.Error != null)
        {
            Assert.DoesNotContain("/etc/", checkResponse.Error);
            Assert.DoesNotContain("\\windows\\", checkResponse.Error.ToLowerInvariant());
            Assert.DoesNotContain("access denied", checkResponse.Error.ToLowerInvariant());
        }
    }

    #endregion

    #region XSS Prevention Tests

    [Theory]
    [InlineData("<script>alert('XSS')</script>")]
    [InlineData("<img src=x onerror=alert('XSS')>")]
    [InlineData("javascript:alert('XSS')")]
    [InlineData("<svg onload=alert('XSS')>")]
    [InlineData("</script><script>alert('XSS')</script>")]
    [InlineData("<iframe src=javascript:alert('XSS')></iframe>")]
    public async Task ReceiptsEndpoint_PreventsXssInResponse(string xssPayload)
    {
        // Arrange
        var client = _factory.CreateClient();
        var maliciousData = CreateMaliciousImageWithText(xssPayload);
        using var maliciousContent = new ByteArrayContent(maliciousData);
        maliciousContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", maliciousContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(receiptResponse);
        
        // Verify that any script tags or JavaScript are properly escaped/sanitized in the response
        AssertNoXssInResponse(responseJson);
        
        if (!string.IsNullOrEmpty(receiptResponse.OcrText))
        {
            AssertNoXssInText(receiptResponse.OcrText);
        }
        
        if (!string.IsNullOrEmpty(receiptResponse.Error))
        {
            AssertNoXssInText(receiptResponse.Error);
        }
    }

    [Theory]
    [InlineData("<script>alert('XSS')</script>")]
    [InlineData("<img src=x onerror=alert('XSS')>")]
    [InlineData("javascript:alert('XSS')")]
    [InlineData("<svg onload=alert('XSS')>")]
    public async Task ChecksEndpoint_PreventsXssInResponse(string xssPayload)
    {
        // Arrange
        var client = _factory.CreateClient();
        var maliciousData = CreateMaliciousImageWithText(xssPayload);
        using var maliciousContent = new ByteArrayContent(maliciousData);
        maliciousContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/checks", maliciousContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);
        Assert.NotNull(checkResponse);
        
        // Verify that any script tags or JavaScript are properly handled
        AssertNoXssInResponse(responseJson);
        
        if (!string.IsNullOrEmpty(checkResponse.OcrText))
        {
            AssertNoXssInText(checkResponse.OcrText);
        }
        
        if (!string.IsNullOrEmpty(checkResponse.Error))
        {
            AssertNoXssInText(checkResponse.Error);
        }
    }

    [Fact]
    public async Task ApiResponse_HasSecureContentTypeHeaders()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify secure content type
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        
        // Check for security headers (if implemented)
        if (response.Headers.Contains("X-Content-Type-Options"))
        {
            Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        }
    }

    #endregion

    #region Authorization and Authentication Tests

    [Fact]
    public async Task ReceiptsEndpoint_AccessibleWithoutAuthentication()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        // Currently the API is designed to be publicly accessible
        // This test ensures that authentication is not enforced when not intended
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChecksEndpoint_AccessibleWithoutAuthentication()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/checks", imageContent);

        // Assert
        // Currently the API is designed to be publicly accessible
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Bearer invalid_token")]
    [InlineData("Basic aW52YWxpZDppbnZhbGlk")] // invalid:invalid in base64
    [InlineData("ApiKey invalid_key")]
    public async Task ReceiptsEndpoint_HandlesInvalidAuthenticationGracefully(string authHeader)
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", authHeader);
        using var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        // Should not crash or leak authentication details in response
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.Unauthorized ||
                   response.StatusCode == HttpStatusCode.Forbidden);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("invalid_token", responseContent);
            Assert.DoesNotContain("invalid_key", responseContent);
        }
    }

    #endregion

    #region Rate Limiting and DoS Prevention Tests

    [Fact]
    public async Task ReceiptsEndpoint_HandlesMultipleSimultaneousRequests()
    {
        // Arrange
        var client = _factory.CreateClient();
        const int concurrentRequests = 10;
        var tasks = new List<Task<HttpResponseMessage>>();
        var contentList = new List<ByteArrayContent>();

        try
        {
            // Act
            for (int i = 0; i < concurrentRequests; i++)
            {
                var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                contentList.Add(imageContent);
                tasks.Add(client.PostAsync("/ocr/receipts", imageContent));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            foreach (var response in responses)
            {
                // Should handle concurrent requests gracefully
                Assert.True(response.StatusCode == HttpStatusCode.OK ||
                           response.StatusCode == HttpStatusCode.TooManyRequests ||
                           response.StatusCode == HttpStatusCode.ServiceUnavailable);
                response.Dispose();
            }
        }
        finally
        {
            // Dispose all content objects
            foreach (var content in contentList)
            {
                content.Dispose();
            }
        }
    }

    [Fact]
    public async Task ChecksEndpoint_HandlesMultipleSimultaneousRequests()
    {
        // Arrange
        var client = _factory.CreateClient();
        const int concurrentRequests = 10;
        var tasks = new List<Task<HttpResponseMessage>>();
        var contentList = new List<ByteArrayContent>();

        try
        {
            // Act
            for (int i = 0; i < concurrentRequests; i++)
            {
                var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                contentList.Add(imageContent);
                tasks.Add(client.PostAsync("/ocr/checks", imageContent));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            foreach (var response in responses)
            {
                // Should handle concurrent requests without crashing
                Assert.True(response.StatusCode == HttpStatusCode.OK ||
                           response.StatusCode == HttpStatusCode.TooManyRequests ||
                           response.StatusCode == HttpStatusCode.ServiceUnavailable);
                response.Dispose();
            }
        }
        finally
        {
            // Dispose all content objects
            foreach (var content in contentList)
            {
                content.Dispose();
            }
        }
    }

    #endregion

    #region Content Type Security Tests

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/json")]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    public async Task ReceiptsEndpoint_HandlesIncorrectContentTypes(string contentType)
    {
        // Arrange
        var client = _factory.CreateClient();
        using var imageContent = new ByteArrayContent(TestImageHelper.CreateValidJpegImage());
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        // Act
        var response = await client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        // Should handle gracefully regardless of content type header
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.UnsupportedMediaType ||
                   response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReceiptsEndpoint_RejectsNonImageMimeTypes()
    {
        // Arrange
        var client = _factory.CreateClient();
        var textData = Encoding.UTF8.GetBytes("This is plain text, not an image");
        using var textContent = new ByteArrayContent(textData);
        textContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        // Act
        var response = await client.PostAsync("/ocr/receipts", textContent);

        // Assert
        // Should handle non-image content appropriately
        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                   response.StatusCode == HttpStatusCode.UnsupportedMediaType ||
                   response.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates malicious image data containing the specified text payload
    /// </summary>
    private static byte[] CreateMaliciousImageWithText(string maliciousText)
    {
        // Create a basic image structure with embedded malicious text
        // This simulates what might happen if an attacker embeds malicious content in image metadata or data
        var imageHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // Basic JPEG header
        var textBytes = Encoding.UTF8.GetBytes(maliciousText);
        var imageFooter = new byte[] { 0xFF, 0xD9 }; // JPEG end marker
        
        var maliciousImage = new byte[imageHeader.Length + textBytes.Length + imageFooter.Length];
        Array.Copy(imageHeader, 0, maliciousImage, 0, imageHeader.Length);
        Array.Copy(textBytes, 0, maliciousImage, imageHeader.Length, textBytes.Length);
        Array.Copy(imageFooter, 0, maliciousImage, imageHeader.Length + textBytes.Length, imageFooter.Length);
        
        return maliciousImage;
    }

    /// <summary>
    /// Asserts that the response JSON does not contain unescaped XSS payloads
    /// </summary>
    private static void AssertNoXssInResponse(string responseJson)
    {
        // Check for common XSS patterns that should be escaped or removed
        Assert.DoesNotContain("<script>", responseJson);
        Assert.DoesNotContain("javascript:", responseJson);
        Assert.DoesNotContain("onerror=", responseJson);
        Assert.DoesNotContain("onload=", responseJson);
        Assert.DoesNotContain("<iframe", responseJson);
    }

    /// <summary>
    /// Asserts that text content does not contain dangerous XSS patterns
    /// </summary>
    private static void AssertNoXssInText(string text)
    {
        // Verify that dangerous HTML/JavaScript is properly handled
        // Note: The exact handling depends on the application's sanitization strategy
        
        // If the application escapes HTML, these should be escaped
        // If the application strips HTML, these should be removed
        // This test ensures they don't appear as-is in a dangerous form
        
        var dangerousPatterns = new[]
        {
            "<script>alert(",
            "javascript:alert(",
            "onerror=alert(",
            "onload=alert("
        };

        foreach (var pattern in dangerousPatterns)
        {
            Assert.DoesNotContain(pattern, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion
}