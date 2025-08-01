using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nolock.social.CloudflareAI.Configuration;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.Models;

namespace Nolock.social.CloudflareAI.Services;

/// <summary>
/// HTTP client implementation for Cloudflare Workers AI
/// </summary>
public sealed class WorkersAIClient : IWorkersAI, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WorkersAISettings _settings;
    private readonly ILogger<WorkersAIClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public WorkersAIClient(
        HttpClient httpClient,
        IOptions<WorkersAISettings> settings,
        ILogger<WorkersAIClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        // Ensure BaseAddress ends with / for proper URL composition
        var baseUrl = _settings.BaseUrl.TrimEnd('/') + '/';
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = _settings.Timeout;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        _logger.LogDebug("HttpClient BaseAddress set to: {BaseAddress}", _httpClient.BaseAddress);
        _logger.LogDebug("Settings BaseUrl: {BaseUrl}", _settings.BaseUrl);
    }

    public async Task<T> RunAsync<T>(
        string model, 
        object input, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(input);

        _logger.LogDebug("Running model {Model} with input type {InputType}", model, input.GetType().Name);

        var requestUrl = $"accounts/{_settings.AccountId}/ai/run/{model}";
        var jsonContent = JsonSerializer.Serialize(input, _jsonOptions);
        
        _logger.LogDebug("Request URL: {RequestUrl}", requestUrl);
        _logger.LogDebug("Full URL will be: {BaseAddress}{RequestUrl}", _httpClient.BaseAddress, requestUrl);
        
        using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        
        var response = await SendWithRetryAsync(
            () => _httpClient.PostAsync(requestUrl, content, cancellationToken),
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        _logger.LogDebug("Received response from model {Model}: {ResponseLength} characters", 
            model, responseContent.Length);

        var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(responseContent, _jsonOptions);
        
        if (apiResponse?.Success != true)
        {
            var errorMessage = apiResponse?.Errors?.FirstOrDefault()?.Message ?? "Unknown API error";
            throw new HttpRequestException($"API request failed: {errorMessage}");
        }

        return apiResponse.Result!;
    }

    public async Task<HttpResponseMessage> RunRawAsync(
        string model, 
        object input, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(input);

        _logger.LogDebug("Running model {Model} (raw response) with input type {InputType}", 
            model, input.GetType().Name);

        var requestUrl = $"accounts/{_settings.AccountId}/ai/run/{model}";
        var jsonContent = JsonSerializer.Serialize(input, _jsonOptions);
        
        using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        
        return await SendWithRetryAsync(
            () => _httpClient.PostAsync(requestUrl, content, cancellationToken),
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> requestFactory,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var maxAttempts = _settings.MaxRetryAttempts;

        while (attempt < maxAttempts)
        {
            try
            {
                var response = await requestFactory();
                
                if (response.IsSuccessStatusCode || attempt == maxAttempts - 1)
                {
                    return response;
                }

                _logger.LogWarning("Request failed with status {StatusCode} on attempt {Attempt}/{MaxAttempts}", 
                    response.StatusCode, attempt + 1, maxAttempts);

                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts - 1)
            {
                _logger.LogWarning(ex, "HTTP request failed on attempt {Attempt}/{MaxAttempts}", 
                    attempt + 1, maxAttempts);
            }

            attempt++;
            
            if (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogDebug("Retrying request in {Delay} seconds", delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new HttpRequestException($"Request failed after {maxAttempts} attempts");
    }

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Request failed with status {response.StatusCode}: {content}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}