using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Nolock.social.CloudflareAI;
using Nolock.social.CloudflareAI.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Interfaces;
using Nolock.social.CloudflareAI.JsonExtraction.Services;
using Nolock.social.MistralOcr;
using Nolock.social.MistralOcr.Models;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.OCRservices.Core.Models;

namespace Nolock.social.OCRservices.Tests;

public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        // Set required environment variables for testing
        Environment.SetEnvironmentVariable("MISTRAL_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID", "test-account-id");
        Environment.SetEnvironmentVariable("CLOUDFLARE_API_TOKEN", "test-api-token");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MistralOcr:ApiKey"] = "test-api-key",
                ["CloudflareAI:AccountId"] = "test-account-id",
                ["CloudflareAI:ApiToken"] = "test-api-token"
            });
        });
        
        builder.ConfigureServices(services =>
        {
            // Remove real services
            var mistralService = services.FirstOrDefault(d => d.ServiceType == typeof(IMistralOcrService));
            if (mistralService != null) services.Remove(mistralService);
            
            var workersAI = services.FirstOrDefault(d => d.ServiceType == typeof(IWorkersAI));
            if (workersAI != null) services.Remove(workersAI);
            
            // Add mock services for testing
            var mockMistralOcr = new Mock<IMistralOcrService>();
            mockMistralOcr.Setup(x => x.ProcessImageDataItemAsync(It.IsAny<(string url, string mimeType)>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ValueTuple<string, string> dataItem, CancellationToken ct) =>
                {
                    // Check if the data URL contains actual image data
                    if (string.IsNullOrEmpty(dataItem.Item1) || dataItem.Item1.Length < 100)
                    {
                        // Return empty text for empty/tiny images
                        return new MistralOcrResult { Text = "", ModelUsed = "test-model", TotalTokens = 0 };
                    }
                    return new MistralOcrResult { Text = "Test OCR text", ModelUsed = "test-model", TotalTokens = 100 };
                });
            services.AddSingleton(mockMistralOcr.Object);
            
            var mockWorkersAI = new Mock<IWorkersAI>();
            services.AddSingleton(mockWorkersAI.Object);
            
            // Add mock OCR extraction service
            var mockOcrExtraction = new Mock<IOcrExtractionService>();
            mockOcrExtraction.Setup(x => x.ProcessExtractionRequestAsync(It.IsAny<OcrExtractionRequest>()))
                .ReturnsAsync(new OcrExtractionResponse<object>
                {
                    Success = true,
                    Data = new Receipt 
                    { 
                        Merchant = new MerchantInfo { Name = "Test Store" },
                        Totals = new ReceiptTotals { Total = 10.99m }
                    },
                    Confidence = 0.95,
                    ProcessingTimeMs = 100,
                    DocumentType = DocumentType.Receipt
                });
            services.AddSingleton(mockOcrExtraction.Object);
        });
        
        builder.UseEnvironment("Testing");
    }
}