using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nolock.social.CloudflareAI.Configuration;
using Nolock.social.CloudflareAI.Models;
using Nolock.social.CloudflareAI.Services;
using Xunit;

namespace Nolock.social.CloudflareAI.Tests;

public sealed class WorkersAIClientTests : IDisposable
{
    private readonly HttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly WorkersAISettings _settings;
    private readonly ILogger<WorkersAIClient> _logger;
    private readonly WorkersAIClient _client;

    public WorkersAIClientTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler);
        _settings = new WorkersAISettings
        {
            AccountId = "test-account-id",
            ApiToken = "test-api-token",
            BaseUrl = "https://api.test.com",
            Timeout = TimeSpan.FromSeconds(30),
            MaxRetryAttempts = 1
        };
        _logger = new MockLogger<WorkersAIClient>();
        _client = new WorkersAIClient(_httpClient, Options.Create(_settings), _logger);
    }

    [Fact]
    public async Task RunAsync_WithValidRequest_ReturnsExpectedResult()
    {
        var expectedResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = true,
            Result = new TextGenerationResponse { Response = "Test response" }
        };

        var mockHandler = (MockHttpMessageHandler)_mockHandler;
        mockHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(expectedResponse));

        var request = new TextGenerationRequest { Prompt = "Test prompt" };
        var result = await _client.RunAsync<TextGenerationResponse>("@cf/meta/llama-2-7b-chat-int8", request);

        Assert.NotNull(result);
        Assert.Equal("Test response", result.Response);
    }

    [Fact]
    public async Task RunAsync_WithApiError_ThrowsHttpRequestException()
    {
        var errorResponse = new ApiResponse<TextGenerationResponse>
        {
            Success = false,
            Errors = [new ApiError { Code = 400, Message = "Bad request" }]
        };

        var mockHandler = (MockHttpMessageHandler)_mockHandler;
        mockHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(errorResponse));

        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.RunAsync<TextGenerationResponse>("@cf/meta/llama-2-7b-chat-int8", request));

        Assert.Contains("Bad request", exception.Message);
    }

    [Fact]
    public async Task RunRawAsync_WithValidRequest_ReturnsHttpResponse()
    {
        var mockHandler = (MockHttpMessageHandler)_mockHandler;
        mockHandler.SetResponse(HttpStatusCode.OK, "Raw response content");

        var request = new TextGenerationRequest { Prompt = "Test prompt" };
        var response = await _client.RunRawAsync("@cf/meta/llama-2-7b-chat-int8", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Raw response content", content);
    }

    [Fact]
    public async Task RunAsync_WithNullModel_ThrowsArgumentNullException()
    {
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _client.RunAsync<TextGenerationResponse>(null!, request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_WithInvalidModel_ThrowsArgumentException(string model)
    {
        var request = new TextGenerationRequest { Prompt = "Test prompt" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _client.RunAsync<TextGenerationResponse>(model, request));
    }

    [Fact]
    public async Task RunAsync_WithNullInput_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _client.RunAsync<TextGenerationResponse>("@cf/meta/llama-2-7b-chat-int8", null!));
    }

    public void Dispose()
    {
        _client?.Dispose();
        _httpClient?.Dispose();
        _mockHandler?.Dispose();
    }
}

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private string _content = string.Empty;

    public void SetResponse(HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

public sealed class MockLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}