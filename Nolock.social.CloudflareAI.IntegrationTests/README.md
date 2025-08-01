# Cloudflare Workers AI Integration Tests

This project contains comprehensive integration tests for the Nolock.social.CloudflareAI library that invoke actual Cloudflare AI APIs.

## Prerequisites

### Environment Variables

You must set the following environment variables before running the tests:

```bash
export CLOUDFLARE_ACCOUNT_ID="your-cloudflare-account-id"
export CLOUDFLARE_API_TOKEN="your-cloudflare-api-token"
```

### API Token Permissions

Your Cloudflare API token must have the following permissions:
- **Account**: Cloudflare Workers:Edit
- **Zone Resources**: Include All zones

## Test Categories

### Text Generation Tests (`TextGenerationIntegrationTests`)
- Tests all major text generation models (Llama, Mistral, Code Llama, Gemma)
- Validates different prompt types (simple prompts, chat messages, code generation)
- Tests parameter variations (temperature, max tokens)
- Includes creative writing and technical content generation

### Image Generation Tests (`ImageGenerationIntegrationTests`)
- Tests Stable Diffusion models (1.5, XL, DreamShaper)
- Validates different image types (portraits, landscapes, architecture)
- Tests generation parameters (steps, guidance, strength)
- Verifies image output format and quality

### Embedding Tests (`EmbeddingIntegrationTests`)
- Tests BGE embedding models (Small, Base, Large)
- Validates single and batch text embedding
- Tests semantic similarity calculations
- Includes edge cases (empty text, long text)

### Vision Tests (`VisionIntegrationTests`)
- Tests Llava and UForm vision models
- Validates image understanding and OCR capabilities
- Tests object detection and counting
- Includes various prompt types for vision tasks

## Running the Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Category
```bash
dotnet test --filter "FullyQualifiedName~TextGeneration"
dotnet test --filter "FullyQualifiedName~ImageGeneration"
dotnet test --filter "FullyQualifiedName~Embedding"
dotnet test --filter "FullyQualifiedName~Vision"
```

### Run with Detailed Output
```bash
dotnet test --verbosity normal --logger "console;verbosity=detailed"
```

## Expected Costs

⚠️ **WARNING: These tests make real API calls to Cloudflare Workers AI and will incur charges.**

Approximate costs per full test run:
- Text Generation: ~$0.10-0.50 (varies by model and tokens)
- Image Generation: ~$0.50-2.00 (varies by model and parameters)
- Embeddings: ~$0.05-0.20 (varies by text volume)
- Vision: ~$0.20-0.80 (varies by image complexity)

**Total estimated cost per full test run: $0.85-3.50**

## Test Features

### Comprehensive Model Coverage
- ✅ All major Cloudflare AI model types
- ✅ Parameter variations and edge cases
- ✅ Error handling and validation
- ✅ Performance and quality assertions

### Real API Validation
- ✅ Actual HTTP calls to Cloudflare APIs
- ✅ Response format validation
- ✅ Output quality checks
- ✅ Error scenario testing

### Quality Assurance
- ✅ Response content validation
- ✅ Output size and format checks
- ✅ Semantic correctness verification
- ✅ Performance benchmarking

## Skipping Tests

If credentials are not available, tests will be automatically skipped with an appropriate message. You can also skip specific tests using:

```bash
dotnet test --filter "Category!=Integration"
```

## Troubleshooting

### Common Issues

1. **Tests Skipped**: Ensure environment variables are set correctly
2. **API Errors**: Verify your API token has correct permissions
3. **Timeout Errors**: Integration tests use longer timeouts (5 minutes)
4. **Rate Limiting**: Cloudflare may rate limit requests; retry if needed

### Debug Mode

To see detailed HTTP requests and responses:

```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet test --verbosity diagnostic
```