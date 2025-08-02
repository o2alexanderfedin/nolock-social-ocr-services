using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nolock.social.CloudflareAI.Configuration;
using Nolock.social.CloudflareAI.Models;
using Nolock.social.CloudflareAI.Services;
using RichardSzalay.MockHttp;

namespace Nolock.social.CloudflareAI.IntegrationTests;

/// <summary>
/// Comprehensive error handling and resilience tests for CloudflareAI client
/// Tests network timeouts, rate limiting, invalid responses, malformed JSON, and authentication failures
/// </summary>
[Collection("CloudflareAI")]
public sealed class ErrorHandlingTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _mockHttpClient;
    private readonly WorkersAISettings _settings;
    private readonly ILogger<WorkersAIClient> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ErrorHandlingTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _mockHttpClient = new HttpClient(_mockHandler);
        
        _settings = new WorkersAISettings
        {
            AccountId = "test-account-id",
            ApiToken = "test-api-token",
            BaseUrl = "https://api.cloudflare.com/client/v4",
            Timeout = TimeSpan.FromSeconds(30),
            MaxRetryAttempts = 3
        };

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<WorkersAIClient>();
    }

    #region Network Timeout Tests

    [Fact]
    public async Task RunAsync_NetworkTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var settings = new WorkersAISettings
        {
            AccountId = "test-account-id",
            ApiToken = "test-api-token",
            BaseUrl = "https://api.cloudflare.com/client/v4",
            Timeout = TimeSpan.FromMilliseconds(100), // Very short timeout
            MaxRetryAttempts = 1
        };

        _mockHandler
            .When($"{settings.BaseUrl}/accounts/{settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(async () =>
            {
                await Task.Delay(200); // Delay longer than timeout
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.NotNull(exception);
    }

    [Fact]
    public async Task RunAsync_HttpErrors_WithRetries_RetriesCorrectTimes()
    {
        // Arrange
        var callCount = 0;
        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(() =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("Service unavailable", Encoding.UTF8, "text/plain")
                });
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("ServiceUnavailable", exception.Message);
        Assert.Equal(3, callCount); // Should retry 3 times
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task RunAsync_RateLimited_ThrowsHttpRequestException()
    {
        // Arrange
        var rateLimitResponse = new
        {
            success = false,
            errors = new[]
            {
                new { code = 10013, message = "Rate limit exceeded" }
            }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.TooManyRequests, "application/json", JsonSerializer.Serialize(rateLimitResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Rate limit exceeded", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RateLimited_WithRetries_EventuallySucceeds()
    {
        // Arrange
        var callCount = 0;
        var rateLimitResponse = new
        {
            success = false,
            errors = new[]
            {
                new { code = 10013, message = "Rate limit exceeded" }
            }
        };

        var successResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = true,
            Result = new TextGenerationResponse { Response = "Test response" }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(() =>
            {
                callCount++;
                return Task.FromResult(callCount <= 2
                    ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(rateLimitResponse), Encoding.UTF8, "application/json")
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(successResponse), Encoding.UTF8, "application/json")
                    });
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act
        var result = await client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test response", result.Response);
        Assert.Equal(3, callCount); // Should have retried twice and succeeded on third attempt
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task RunAsync_ServerErrors_RetriesAndThrows(HttpStatusCode statusCode)
    {
        // Arrange
        var callCount = 0;
        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(() =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent("Server error", Encoding.UTF8, "text/plain")
                });
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains(statusCode.ToString(), exception.Message);
        Assert.Equal(3, callCount); // Should retry 3 times
    }

    #endregion

    #region Invalid API Response Tests

    [Fact]
    public async Task RunAsync_ApiReturnsFalseSuccess_ThrowsHttpRequestException()
    {
        // Arrange
        var apiResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = false,
            Errors = new[]
            {
                new ApiError { Code = 1001, Message = "Model not found" }
            }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(apiResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Model not found", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ApiReturnsNullSuccess_ThrowsHttpRequestException()
    {
        // Arrange
        var apiResponse = new
        {
            success = (bool?)null,
            result = new { response = "Test response" }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(apiResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<JsonException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("JSON value could not be converted", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ApiReturnsEmptyErrorArray_ThrowsGenericError()
    {
        // Arrange
        var apiResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = false,
            Errors = Array.Empty<ApiError>()
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(apiResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Unknown API error", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ApiReturnsNullErrors_ThrowsGenericError()
    {
        // Arrange
        var apiResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = false,
            Errors = null
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(apiResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Unknown API error", exception.Message);
    }

    #endregion

    #region Malformed JSON Response Tests

    [Fact]
    public async Task RunAsync_MalformedJsonResponse_ThrowsJsonException()
    {
        // Arrange
        const string malformedJson = "{ \"success\": true, \"result\": { \"response\": \"incomplete";

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", malformedJson);

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));
    }

    [Fact]
    public async Task RunAsync_EmptyJsonResponse_ThrowsJsonException()
    {
        // Arrange
        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", "");

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));
    }

    [Fact]
    public async Task RunAsync_NonJsonResponse_ThrowsJsonException()
    {
        // Arrange
        const string htmlResponse = "<html><body>Internal Server Error</body></html>";

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", htmlResponse);

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));
    }

    [Fact]
    public async Task RunAsync_InvalidJsonStructure_HandlesGracefully()
    {
        // Arrange
        const string invalidStructureJson = "{ \"completely\": \"different\", \"structure\": 123 }";

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", invalidStructureJson);

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert - Should throw because success is null/false and there are no errors
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Unknown API error", exception.Message);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"string\"")]
    [InlineData("123")]
    [InlineData("true")]
    public async Task RunAsync_UnexpectedJsonTypes_ThrowsException(string jsonValue)
    {
        // Arrange
        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.OK, "application/json", jsonValue);

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));
    }

    #endregion

    #region Authentication Failure Tests

    [Fact]
    public async Task RunAsync_InvalidApiToken_ThrowsUnauthorizedException()
    {
        // Arrange
        var unauthorizedResponse = new
        {
            success = false,
            errors = new[]
            {
                new { code = 10000, message = "Authentication failed" }
            }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.Unauthorized, "application/json", JsonSerializer.Serialize(unauthorizedResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Unauthorized", exception.Message);
    }

    [Fact]
    public async Task RunAsync_ExpiredApiToken_ThrowsUnauthorizedException()
    {
        // Arrange
        var expiredTokenResponse = new
        {
            success = false,
            errors = new[]
            {
                new { code = 10001, message = "API token expired" }
            }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.Unauthorized, "application/json", JsonSerializer.Serialize(expiredTokenResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Unauthorized", exception.Message);
    }

    [Fact]
    public async Task RunAsync_InsufficientPermissions_ThrowsForbiddenException()
    {
        // Arrange
        var forbiddenResponse = new
        {
            success = false,
            errors = new[]
            {
                new { code = 10003, message = "Insufficient permissions to access this resource" }
            }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.Forbidden, "application/json", JsonSerializer.Serialize(forbiddenResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Forbidden", exception.Message);
    }

    [Fact]
    public async Task RunAsync_InvalidAccountId_ThrowsForbiddenException()
    {
        // Arrange
        var invalidAccountResponse = new
        {
            success = false,
            errors = new[]
            {
                new { code = 7003, message = "Could not route to /accounts/invalid-account-id/ai, perhaps your object identifier is invalid?" }
            }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.Forbidden, "application/json", JsonSerializer.Serialize(invalidAccountResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("Forbidden", exception.Message);
    }

    #endregion

    #region Integration Tests with Raw Response

    [Fact]
    public async Task RunRawAsync_NetworkTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var settings = new WorkersAISettings
        {
            AccountId = "test-account-id",
            ApiToken = "test-api-token",
            BaseUrl = "https://api.cloudflare.com/client/v4",
            Timeout = TimeSpan.FromMilliseconds(100),
            MaxRetryAttempts = 1
        };

        _mockHandler
            .When($"{settings.BaseUrl}/accounts/{settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(async () =>
            {
                await Task.Delay(200);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.RunRawAsync(TextGenerationModels.Llama2_7B_Chat_Int8, request));
    }

    [Fact]
    public async Task RunRawAsync_RateLimited_ReturnsRateLimitResponse()
    {
        // Arrange
        var rateLimitResponse = new
        {
            success = false,
            errors = new[]
            {
                new { code = 10013, message = "Rate limit exceeded" }
            }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(HttpStatusCode.TooManyRequests, "application/json", JsonSerializer.Serialize(rateLimitResponse));

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act
        using var response = await client.RunRawAsync(TextGenerationModels.Llama2_7B_Chat_Int8, request);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Rate limit exceeded", content);
    }

    #endregion

    #region Parameter Validation Tests

    [Fact]
    public async Task RunAsync_NullModel_ThrowsArgumentException()
    {
        // Arrange
        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.RunAsync<TextGenerationResponse>(null!, request));
    }

    [Fact]
    public async Task RunAsync_EmptyModel_ThrowsArgumentException()
    {
        // Arrange
        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.RunAsync<TextGenerationResponse>("", request));
    }

    [Fact]
    public async Task RunAsync_WhitespaceModel_ThrowsArgumentException()
    {
        // Arrange
        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.RunAsync<TextGenerationResponse>("   ", request));
    }

    [Fact]
    public async Task RunAsync_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, null!));
    }

    [Fact]
    public async Task RunRawAsync_NullModel_ThrowsArgumentException()
    {
        // Arrange
        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.RunRawAsync(null!, request));
    }

    [Fact]
    public async Task RunRawAsync_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.RunRawAsync(TextGenerationModels.Llama2_7B_Chat_Int8, null!));
    }

    #endregion

    #region Resilience and Recovery Tests

    [Fact]
    public async Task RunAsync_IntermittentFailures_EventuallySucceeds()
    {
        // Arrange
        var callCount = 0;
        var successResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = true,
            Result = new TextGenerationResponse { Response = "Success after retries" }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(() =>
            {
                callCount++;
                return Task.FromResult(callCount <= 2
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new StringContent("Service temporarily unavailable", Encoding.UTF8, "text/plain")
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(successResponse), Encoding.UTF8, "application/json")
                    });
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act
        var result = await client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success after retries", result.Response);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task RunAsync_ExceedsMaxRetries_ThrowsHttpRequestException()
    {
        // Arrange
        var callCount = 0;
        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(() =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("Service unavailable", Encoding.UTF8, "text/plain")
                });
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request));

        Assert.Contains("ServiceUnavailable", exception.Message);
        Assert.Equal(3, callCount); // Should retry 3 times
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task RunAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var successResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = true,
            Result = new TextGenerationResponse { Response = "Test response" }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(async () =>
            {
                await Task.Delay(100); // Small delay to allow cancellation
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(successResponse), Encoding.UTF8, "application/json")
                };
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act
        await cts.CancelAsync(); // Cancel immediately

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.RunAsync<TextGenerationResponse>(TextGenerationModels.Llama2_7B_Chat_Int8, request, cts.Token));
    }

    [Fact]
    public async Task RunRawAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var successResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = true,
            Result = new TextGenerationResponse { Response = "Test response" }
        };

        _mockHandler
            .When($"{_settings.BaseUrl}/accounts/{_settings.AccountId}/ai/run/{TextGenerationModels.Llama2_7B_Chat_Int8}")
            .Respond(async () =>
            {
                await Task.Delay(100);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(successResponse), Encoding.UTF8, "application/json")
                };
            });

        using var client = new WorkersAIClient(_mockHttpClient, Options.Create(_settings), _logger);
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.RunRawAsync(TextGenerationModels.Llama2_7B_Chat_Int8, request, cts.Token));
    }

    #endregion

    public void Dispose()
    {
        _mockHandler?.Dispose();
        _mockHttpClient?.Dispose();
        _loggerFactory?.Dispose();
    }
}