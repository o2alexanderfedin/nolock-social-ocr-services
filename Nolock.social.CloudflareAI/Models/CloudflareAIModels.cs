namespace Nolock.social.CloudflareAI.Models;

/// <summary>
/// Text generation models available in Cloudflare Workers AI
/// </summary>
public static class TextGenerationModels
{
    public const string Llama2_7B_Chat_Int8 = "@cf/meta/llama-2-7b-chat-int8";
    public const string Llama3_8B_Instruct = "@cf/meta/llama-3-8b-instruct";
    public const string Llama3_1_8B_Instruct = "@cf/meta/llama-3.1-8b-instruct";
    public const string Llama3_3_70B_Instruct_FP8_Fast = "@cf/meta/llama-3.3-70b-instruct-fp8-fast";
    public const string Mistral_7B_Instruct_V0_1 = "@cf/mistral/mistral-7b-instruct-v0.1";
    public const string CodeLlama_7B_Instruct = "@cf/meta/code-llama-7b-instruct";
    public const string Gemma_7B_It = "@cf/google/gemma-7b-it";
}

/// <summary>
/// Image generation models available in Cloudflare Workers AI
/// </summary>
public static class ImageGenerationModels
{
    public const string StableDiffusion_1_5 = "@cf/runwayml/stable-diffusion-v1-5";
    public const string StableDiffusion_XL_Base_1_0 = "@cf/stabilityai/stable-diffusion-xl-base-1.0";
    public const string DreamShaper_8_LCM = "@cf/lykon/dreamshaper-8-lcm";
}

/// <summary>
/// Text embedding models available in Cloudflare Workers AI
/// </summary>
public static class EmbeddingModels
{
    public const string BGE_Small_EN_V1_5 = "@cf/baai/bge-small-en-v1.5";
    public const string BGE_Base_EN_V1_5 = "@cf/baai/bge-base-en-v1.5";
    public const string BGE_Large_EN_V1_5 = "@cf/baai/bge-large-en-v1.5";
}

/// <summary>
/// Vision models available in Cloudflare Workers AI for OCR and image understanding
/// </summary>
public static class VisionModels
{
    public const string Llava_1_5_7B_HF = "@cf/llava-hf/llava-1.5-7b-hf";
    public const string UForm_Gen2_QWen_500M = "@cf/unum/uform-gen2-qwen-500m";
}