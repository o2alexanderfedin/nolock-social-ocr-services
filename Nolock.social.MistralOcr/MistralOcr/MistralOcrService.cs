using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using Nolock.social.MistralOcr.Converters;
using Nolock.social.MistralOcr.Models;
using Polly;

namespace Nolock.social.MistralOcr;

public sealed class MistralOcrService : IMistralOcrService
{
    private readonly HttpClient _httpClient;
    private readonly MistralOcrConfiguration _configuration;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<MistralOcrService> _logger;
    private static readonly RecyclableMemoryStreamManager StreamManager = new();

    public MistralOcrService(HttpClient httpClient, IOptions<MistralOcrConfiguration> configuration, ILogger<MistralOcrService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _configuration = configuration.Value;
        _logger = logger;
        
        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = _configuration.Timeout;

        // Configure retry policy
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                _configuration.MaxRetries,
                retryAttempt => _configuration.RetryDelay * Math.Pow(2, retryAttempt - 1),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var response = outcome.Result;
                    Console.WriteLine($"Retry {retryCount} after {timespan}ms, StatusCode: {response?.StatusCode}");
                });

        // Configure JSON options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters = { new MistralContentJsonConverter() }
        };
    }

    public async Task<MistralOcrResult> ProcessImageAsync(string imageUrl, string? prompt = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);

        var request = CreateOcrRequest(imageUrl, prompt);
        return await SendRequestAsync(request, cancellationToken);
    }

    public async Task<MistralOcrResult> ProcessImageDataUrlAsync(string dataUrl, string? prompt = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataUrl);

        if (!dataUrl.StartsWith("data:"))
        {
            throw new ArgumentException("Invalid data URL format", nameof(dataUrl));
        }

        var request = CreateOcrRequest(dataUrl, prompt);
        return await SendRequestAsync(request, cancellationToken);
    }

    public async Task<MistralOcrResult> ProcessImageBytesAsync(byte[] imageBytes, string mimeType, string? prompt = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        var base64 = Convert.ToBase64String(imageBytes);
        var dataUrl = $"data:{mimeType};base64,{base64}";
        
        return await ProcessImageDataUrlAsync(dataUrl, prompt, cancellationToken);
    }

    public async Task<MistralOcrResult> ProcessImageStreamAsync(Stream imageStream, string mimeType, string? prompt = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        await using var ms = StreamManager.GetStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        
        return await ProcessImageBytesAsync(bytes, mimeType, prompt, cancellationToken);
    }

    private Models.MistralOcrRequest CreateOcrRequest(string imageUrlOrDataUrl, string? prompt)
    {
        var request = new Models.MistralOcrRequest
        {
            Model = _configuration.Model,
            Messages = new List<MistralMessage>
            {
                new()
                {
                    Role = "user",
                    Content = new List<MistralContent>()
                }
            }
        };

        // Add prompt if provided
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            request.Messages[0].Content.Add(new MistralTextContent { Text = prompt });
        }
        else
        {
            request.Messages[0].Content.Add(new MistralTextContent 
            { 
                Text = "Extract all text from this image. Preserve the original formatting and structure as much as possible." 
            });
        }

        // Add image
        request.Messages[0].Content.Add(new MistralImageContent
        {
            ImageUrl = new MistralImageUrl { Url = imageUrlOrDataUrl }
        });

        return request;
    }

    private async Task<MistralOcrResult> SendRequestAsync(Models.MistralOcrRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            _logger.LogDebug("Sending request to Mistral API: {Json}", json);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("/v1/ocr", content, cancellationToken));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Mistral API error response: Status={Status}, Content={Content}", 
                    response.StatusCode, errorContent);
                
                var error = JsonSerializer.Deserialize<MistralOcrError>(errorContent, _jsonOptions);
                
                string errorMessage;
                if (error?.Detail?.Count > 0)
                {
                    // Validation error - concatenate all validation messages
                    errorMessage = string.Join("; ", error.Detail.Select(e => e.Message));
                }
                else if (error?.Error != null)
                {
                    // Regular API error
                    errorMessage = error.Error.Message;
                }
                else
                {
                    // Fallback to status code
                    errorMessage = $"{response.StatusCode} - {response.ReasonPhrase}";
                }
                
                throw new HttpRequestException(
                    $"Mistral OCR API error: {errorMessage}", 
                    null, 
                    response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var ocrResponse = JsonSerializer.Deserialize<MistralOcrResponse>(responseContent, _jsonOptions);

            if (ocrResponse?.Choices == null || ocrResponse.Choices.Count == 0)
            {
                throw new InvalidOperationException("No OCR results returned from Mistral API");
            }

            stopwatch.Stop();

            return new MistralOcrResult
            {
                Text = ocrResponse.Choices[0].Message.Content,
                ModelUsed = ocrResponse.Model,
                TotalTokens = ocrResponse.Usage?.TotalTokens ?? 0,
                ProcessingTime = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["id"] = ocrResponse.Id,
                    ["created"] = ocrResponse.Created,
                    ["finish_reason"] = ocrResponse.Choices[0].FinishReason ?? "unknown"
                }
            };
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException($"Mistral OCR API request timed out after {_configuration.Timeout}");
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to process OCR request", ex);
        }
    }
}