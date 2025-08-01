namespace Nolock.social.CloudflareAI.Interfaces;

/// <summary>
/// Main interface for Cloudflare Workers AI client
/// </summary>
public interface IWorkersAI : IDisposable
{
    /// <summary>
    /// Run a model with the given input
    /// </summary>
    /// <param name="model">The model identifier</param>
    /// <param name="input">The input data for the model</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model output</returns>
    Task<T> RunAsync<T>(string model, object input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Run a model with the given input and return raw response
    /// </summary>
    /// <param name="model">The model identifier</param>  
    /// <param name="input">The input data for the model</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Raw response from the API</returns>
    Task<HttpResponseMessage> RunRawAsync(string model, object input, CancellationToken cancellationToken = default);
}