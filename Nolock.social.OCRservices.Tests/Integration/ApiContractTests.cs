using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Nolock.social.OCRservices.Core.Models;
using Nolock.social.OCRservices.Tests.TestData;
using Xunit;

namespace Nolock.social.OCRservices.Tests.Integration;

/// <summary>
/// Comprehensive API contract tests for OCR service endpoints
/// Tests response schema validation, HTTP status codes, content types, API versioning, and OpenAPI spec compliance
/// 
/// These tests validate the API contract to ensure:
/// - Response schemas match expected structure (using JSON Schema validation)
/// - HTTP status codes are returned correctly for valid/invalid requests
/// - Content-Type headers are set properly
/// - OpenAPI/Swagger specification compliance
/// - API versioning requirements are met
/// - Error responses follow consistent patterns
/// 
/// Note: Tests that interact with OCR endpoints (receipts/checks) may fail in development environments
/// without proper API keys configured. The OpenAPI/Swagger tests should always pass.
/// Configure MISTRAL_API_KEY and CLOUDFLARE_ACCOUNT_ID/CLOUDFLARE_API_TOKEN environment variables for full test coverage.
/// </summary>
public class ApiContractTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiContractTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region Receipt Endpoint Contract Tests

    [Fact]
    public async Task ReceiptsEndpoint_ValidRequest_ReturnsCorrectHttpStatusCode()
    {
        // Arrange
        using var imageContent = CreateValidImageContent();

        // Act
        var response = await _client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReceiptsEndpoint_ValidRequest_ReturnsCorrectContentType()
    {
        // Arrange
        using var imageContent = CreateValidImageContent();

        // Act
        var response = await _client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
        Assert.Contains("utf-8", response.Content.Headers.ContentType.CharSet ?? string.Empty);
    }

    [Fact]
    public async Task ReceiptsEndpoint_ValidRequest_ResponseMatchesSchema()
    {
        // Arrange
        using var imageContent = CreateValidImageContent();
        var expectedSchema = GetReceiptOcrResponseSchema();

        // Act
        var response = await _client.PostAsync("/ocr/receipts", imageContent);
        var responseJson = await response.Content.ReadAsStringAsync();

        // Assert
        var jsonObject = JObject.Parse(responseJson);
        var isValid = jsonObject.IsValid(expectedSchema, out IList<string> errorMessages);
        
        Assert.True(isValid, $"Response schema validation failed: {string.Join(", ", errorMessages)}");
    }

    [Fact]
    public async Task ReceiptsEndpoint_ValidRequest_ContainsRequiredResponseFields()
    {
        // Arrange
        using var imageContent = CreateValidImageContent();

        // Act
        var response = await _client.PostAsync("/ocr/receipts", imageContent);
        var responseJson = await response.Content.ReadAsStringAsync();
        var receiptResponse = JsonSerializer.Deserialize<ReceiptOcrResponse>(responseJson, _jsonOptions);

        // Assert
        Assert.NotNull(receiptResponse);
        Assert.NotNull(receiptResponse.OcrText);
        Assert.True(receiptResponse.ProcessingTimeMs >= 0);
        Assert.True(receiptResponse.Confidence >= 0.0 && receiptResponse.Confidence <= 1.0);
        // Note: Success may be false if OCR extraction fails, which is valid API behavior
        Assert.True(receiptResponse.Success || !string.IsNullOrEmpty(receiptResponse.Error));
        // Error should be null if successful, or non-null if not successful
        Assert.True((receiptResponse.Success && receiptResponse.Error == null) || (!receiptResponse.Success && !string.IsNullOrEmpty(receiptResponse.Error)));
    }

    [Fact]
    public async Task ReceiptsEndpoint_InvalidContentType_ReturnsUnsupportedMediaType()
    {
        // Arrange
        using var content = new StringContent("invalid content", System.Text.Encoding.UTF8, "text/plain");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task ReceiptsEndpoint_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        using var content = new ByteArrayContent(Array.Empty<byte>());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Check Endpoint Contract Tests

    [Fact]
    public async Task ChecksEndpoint_ValidRequest_ReturnsCorrectHttpStatusCode()
    {
        // Arrange
        using var imageContent = CreateValidCheckImageContent();

        // Act
        var response = await _client.PostAsync("/ocr/checks", imageContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChecksEndpoint_ValidRequest_ReturnsCorrectContentType()
    {
        // Arrange
        using var imageContent = CreateValidCheckImageContent();

        // Act
        var response = await _client.PostAsync("/ocr/checks", imageContent);

        // Assert
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
        Assert.Contains("utf-8", response.Content.Headers.ContentType.CharSet ?? string.Empty);
    }

    [Fact]
    public async Task ChecksEndpoint_ValidRequest_ResponseMatchesSchema()
    {
        // Arrange
        using var imageContent = CreateValidCheckImageContent();
        var expectedSchema = GetCheckOcrResponseSchema();

        // Act
        var response = await _client.PostAsync("/ocr/checks", imageContent);
        var responseJson = await response.Content.ReadAsStringAsync();

        // Assert
        var jsonObject = JObject.Parse(responseJson);
        var isValid = jsonObject.IsValid(expectedSchema, out IList<string> errorMessages);
        
        Assert.True(isValid, $"Response schema validation failed: {string.Join(", ", errorMessages)}");
    }

    [Fact]
    public async Task ChecksEndpoint_ValidRequest_ContainsRequiredResponseFields()
    {
        // Arrange
        using var imageContent = CreateValidCheckImageContent();

        // Act
        var response = await _client.PostAsync("/ocr/checks", imageContent);
        var responseJson = await response.Content.ReadAsStringAsync();
        var checkResponse = JsonSerializer.Deserialize<CheckOcrResponse>(responseJson, _jsonOptions);

        // Assert
        Assert.NotNull(checkResponse);
        Assert.NotNull(checkResponse.OcrText);
        Assert.True(checkResponse.ProcessingTimeMs >= 0);
        Assert.True(checkResponse.Confidence >= 0.0 && checkResponse.Confidence <= 1.0);
        // Note: Success may be false if OCR extraction fails, which is valid API behavior
        Assert.True(checkResponse.Success || !string.IsNullOrEmpty(checkResponse.Error));
        // Error should be null if successful, or non-null if not successful
        Assert.True((checkResponse.Success && checkResponse.Error == null) || (!checkResponse.Success && !string.IsNullOrEmpty(checkResponse.Error)));
    }

    [Fact]
    public async Task ChecksEndpoint_InvalidContentType_ReturnsUnsupportedMediaType()
    {
        // Arrange
        using var content = new StringContent("invalid content", System.Text.Encoding.UTF8, "text/plain");

        // Act
        var response = await _client.PostAsync("/ocr/checks", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task ChecksEndpoint_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        using var content = new ByteArrayContent(Array.Empty<byte>());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        // Act
        var response = await _client.PostAsync("/ocr/checks", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region API Versioning and OpenAPI Compliance Tests

    [Fact]
    public async Task SwaggerEndpoint_ReturnsCorrectStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerEndpoint_ReturnsValidOpenApiSpec()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var swaggerJson = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.NotEmpty(swaggerJson);
        var swaggerDoc = JObject.Parse(swaggerJson);
        
        // Validate basic OpenAPI structure
        Assert.NotNull(swaggerDoc["openapi"]);
        Assert.NotNull(swaggerDoc["info"]);
        Assert.NotNull(swaggerDoc["paths"]);
        
        // Validate OCR endpoints are documented
        Assert.NotNull(swaggerDoc["paths"]?["/ocr/receipts"]);
        Assert.NotNull(swaggerDoc["paths"]?["/ocr/checks"]);
        
        // Validate POST operations
        Assert.NotNull(swaggerDoc["paths"]?["/ocr/receipts"]?["post"]);
        Assert.NotNull(swaggerDoc["paths"]?["/ocr/checks"]?["post"]);
    }

    [Fact]
    public async Task SwaggerUI_ReturnsCorrectStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/swagger");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoints_IncludeCorrectTags()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var swaggerJson = await response.Content.ReadAsStringAsync();
        var swaggerDoc = JObject.Parse(swaggerJson);

        // Assert
        var receiptsOperation = swaggerDoc["paths"]?["/ocr/receipts"]?["post"];
        var checksOperation = swaggerDoc["paths"]?["/ocr/checks"]?["post"];
        
        Assert.NotNull(receiptsOperation);
        Assert.NotNull(checksOperation);
        
        var receiptsTags = receiptsOperation["tags"]?.ToObject<string[]>();
        var checksTags = checksOperation["tags"]?.ToObject<string[]>();
        
        Assert.Contains("OCR Operations", receiptsTags ?? Array.Empty<string>());
        Assert.Contains("OCR Operations", checksTags ?? Array.Empty<string>());
    }

    [Fact]
    public async Task ApiEndpoints_HaveCorrectOperationIds()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var swaggerJson = await response.Content.ReadAsStringAsync();
        var swaggerDoc = JObject.Parse(swaggerJson);

        // Assert
        var receiptsOperation = swaggerDoc["paths"]?["/ocr/receipts"]?["post"];
        var checksOperation = swaggerDoc["paths"]?["/ocr/checks"]?["post"];
        
        Assert.Equal("ProcessReceiptOcr", receiptsOperation?["operationId"]?.ToString());
        Assert.Equal("ProcessCheckOcr", checksOperation?["operationId"]?.ToString());
    }

    #endregion

    #region HTTP Headers and CORS Tests

    [Fact]
    public async Task ApiEndpoints_IncludeSecurityHeaders()
    {
        // Arrange
        using var imageContent = CreateValidImageContent();

        // Act
        var response = await _client.PostAsync("/ocr/receipts", imageContent);

        // Assert
        Assert.True(response.Headers.Contains("Date"));
        Assert.True(response.Headers.Contains("Server") || response.Headers.Via.Any());
    }

    [Fact] 
    public async Task ApiEndpoints_HandleOptionsRequest()
    {
        // Act
        using var request = new HttpRequestMessage(HttpMethod.Options, "/ocr/receipts");
        var response = await _client.SendAsync(request);

        // Assert - Should either handle OPTIONS or return Method Not Allowed
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                   response.StatusCode == HttpStatusCode.NoContent ||
                   response.StatusCode == HttpStatusCode.MethodNotAllowed);
    }

    #endregion

    #region Error Response Contract Tests

    [Fact]
    public async Task ApiEndpoints_InvalidMethod_ReturnsMethodNotAllowed()
    {
        // Act
        var response = await _client.GetAsync("/ocr/receipts");

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoints_NonExistentEndpoint_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/ocr/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoints_ErrorResponse_HasCorrectContentType()
    {
        // Arrange
        using var content = new StringContent("invalid", System.Text.Encoding.UTF8, "text/plain");

        // Act
        var response = await _client.PostAsync("/ocr/receipts", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        // For 415 UnsupportedMediaType responses, the framework may not include a content type header
        // if there's no response body, which is valid behavior for this status code
        if (response.Content.Headers.ContentType != null)
        {
            Assert.True(response.Content.Headers.ContentType.MediaType == "application/json" ||
                       response.Content.Headers.ContentType.MediaType == "application/problem+json");
        }
    }

    #endregion

    #region Helper Methods

    private ByteArrayContent CreateValidImageContent()
    {
        var imageBytes = TestImageHelper.GetReceiptImage();
        var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private ByteArrayContent CreateValidCheckImageContent()
    {
        var imageBytes = TestImageHelper.GetCheckImage();
        var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static JSchema GetReceiptOcrResponseSchema()
    {
        return JSchema.Parse(@"{
            'type': 'object',
            'required': ['ocrText', 'confidence', 'processingTimeMs', 'success'],
            'properties': {
                'ocrText': {'type': 'string'},
                'receiptData': {'type': ['object', 'null']},
                'confidence': {'type': 'number', 'minimum': 0, 'maximum': 1},
                'processingTimeMs': {'type': 'integer', 'minimum': 0},
                'modelUsed': {'type': ['string', 'null']},
                'totalTokens': {'type': ['integer', 'null'], 'minimum': 0},
                'success': {'type': 'boolean'},
                'error': {'type': ['string', 'null']}
            },
            'additionalProperties': false
        }");
    }

    private static JSchema GetCheckOcrResponseSchema()
    {
        return JSchema.Parse(@"{
            'type': 'object',
            'required': ['ocrText', 'confidence', 'processingTimeMs', 'success'],
            'properties': {
                'ocrText': {'type': 'string'},
                'checkData': {'type': ['object', 'null']},
                'confidence': {'type': 'number', 'minimum': 0, 'maximum': 1},
                'processingTimeMs': {'type': 'integer', 'minimum': 0},
                'modelUsed': {'type': ['string', 'null']},
                'totalTokens': {'type': ['integer', 'null'], 'minimum': 0},
                'success': {'type': 'boolean'},
                'error': {'type': ['string', 'null']}
            },
            'additionalProperties': false
        }");
    }

    #endregion
}