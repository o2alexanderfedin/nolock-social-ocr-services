using System.Text.Json.Serialization;
using Nolock.social.MistralOcr;

namespace Nolock.social.OCRservices;

[JsonSerializable(typeof(MistralOcrResult))]
internal sealed partial class AppJsonSerializerContext
    : JsonSerializerContext
{
}