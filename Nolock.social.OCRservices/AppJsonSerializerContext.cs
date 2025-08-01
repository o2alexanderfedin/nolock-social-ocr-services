using System.Text.Json.Serialization;
using Nolock.social.MistralOcr;
using Nolock.social.CloudflareAI.JsonExtraction.Models;
using Nolock.social.OCRservices.Core.Models;

namespace Nolock.social.OCRservices;

[JsonSerializable(typeof(MistralOcrResult))]
[JsonSerializable(typeof(DocumentType))]
[JsonSerializable(typeof(DocumentType?))]
[JsonSerializable(typeof(OcrAsyncResponse))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Stream))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class AppJsonSerializerContext
    : JsonSerializerContext
{
}