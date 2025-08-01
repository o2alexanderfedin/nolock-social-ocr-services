using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using Nolock.social.MistralOcr.Models;
using Nolock.social.MistralOcr.Converters;
using Polly;

namespace Nolock.social.MistralOcr;

public sealed class MistralOcrApiService : IMistralOcrService
{
    private readonly HttpClient _httpClient;
    private readonly MistralOcrConfiguration _configuration;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<MistralOcrApiService> _logger;
    private static readonly RecyclableMemoryStreamManager StreamManager = new();

    public MistralOcrApiService(HttpClient httpClient, IOptions<MistralOcrConfiguration> configuration, ILogger<MistralOcrApiService> logger)
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
                    _logger.LogWarning("Retry {RetryCount} after {Timespan}ms, StatusCode: {StatusCode}",
                        retryCount, timespan.TotalMilliseconds, response?.StatusCode);
                });

        // Configure JSON options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // Use exact property names as defined
            WriteIndented = false,
            Converters = { new OcrDocumentJsonConverter() }
        };
    }

    public async Task<MistralOcrResult> ProcessImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);

        var request = new OcrRequest
        {
            Model = _configuration.Model,
            Document = new ImageUrlChunk { ImageUrl = imageUrl }
        };

        return await SendRequestAsync(request, cancellationToken);
    }

    public async Task<MistralOcrResult> ProcessImageDataItemAsync((string url, string mimeType) dataItem, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataItem.url);

        if (!dataItem.url.StartsWith("data:"))
        {
            throw new ArgumentException("Invalid data URL format", nameof(dataItem));
        }

        var request = new OcrRequest
        {
            Model = _configuration.Model,
            Document = new ImageUrlChunk { ImageUrl = dataItem.url }
        };

        return await SendRequestAsync(request, cancellationToken);
    }

    public async Task<MistralOcrResult> ProcessImageBytesAsync(byte[] imageBytes, string mimeType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes cannot be empty", nameof(imageBytes));
        }

        var base64 = Convert.ToBase64String(imageBytes);
        var dataUrl = $"data:{mimeType};base64,{base64}";

        return await ProcessImageDataItemAsync((dataUrl, mimeType), cancellationToken);
    }

    public async Task<MistralOcrResult> ProcessImageStreamAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        if (imageStream.Length == 0)
        {
            throw new ArgumentException("Stream cannot be empty", nameof(imageStream));
        }

        await using var ms = StreamManager.GetStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        return await ProcessImageBytesAsync(bytes, mimeType, cancellationToken);
    }

    private async Task<MistralOcrResult> SendRequestAsync(OcrRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            _logger.LogDebug("Sending OCR request to Mistral API: {Json}", json);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("/v1/ocr", content, cancellationToken));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Mistral OCR API error response: Status={Status}, Content={Content}",
                    response.StatusCode, errorContent);

                throw new HttpRequestException(
                    $"Mistral OCR API error: {response.StatusCode} - {errorContent}",
                    null,
                    response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("OCR API Response: {Response}", responseContent);
            var ocrResponse = JsonSerializer.Deserialize<OcrResponse>(responseContent, _jsonOptions);

            if (ocrResponse?.Pages == null || ocrResponse.Pages.Count == 0)
            {
                throw new InvalidOperationException("No OCR results returned from Mistral API");
            }

            stopwatch.Stop();

            // Combine all page markdown content
            var extractedText = string.Join("\n\n", ocrResponse.Pages.Select(p => p.Markdown));

            return new MistralOcrResult
            {
                Text = extractedText,
                ModelUsed = ocrResponse.Model,
                TotalTokens = ocrResponse.UsageInfo.TotalTokens,
                ProcessingTime = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["page_count"] = ocrResponse.Pages.Count,
                    ["input_tokens"] = ocrResponse.UsageInfo.InputTokens,
                    ["output_tokens"] = ocrResponse.UsageInfo.OutputTokens
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