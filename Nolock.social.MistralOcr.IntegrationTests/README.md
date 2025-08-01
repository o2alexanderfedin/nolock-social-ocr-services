# Mistral OCR Integration Tests

These are real integration tests that make actual API calls to the Mistral API. No mocks or fakes are used.

## Configuration

To run these tests, you need to configure your Mistral API credentials.

### Option 1: Environment Variable
Set the environment variable:
```bash
export MistralOcr__ApiKey="your-api-key-here"
```

### Option 2: Local Configuration File
Create a file `appsettings.test.local.json` in the test project directory:
```json
{
  "MistralOcr": {
    "ApiKey": "your-api-key-here"
  }
}
```

This file is ignored by Git and should not be committed.

### Option 3: Update appsettings.test.json
Update the `ApiKey` value in `appsettings.test.json`, but be careful not to commit your API key.

## Running the Tests

```bash
# Run all integration tests
dotnet test Nolock.social.MistralOcr.IntegrationTests

# Run a specific test
dotnet test --filter "FullyQualifiedName~ProcessImageAsync_WithImageUrl"
```

## Test Coverage

The integration tests cover:
- Processing images from URLs
- Processing images from data URLs (base64)
- Processing images from byte arrays
- Processing images from streams
- Error handling scenarios
- Multiple API calls in sequence
- Large prompts
- Default prompt behavior
- Metadata verification

## Important Notes

1. These tests make real API calls and will consume your Mistral API credits
2. Tests may take longer to run due to network latency
3. Some tests may fail if the test images are no longer available at their URLs
4. Rate limiting may affect test execution if run too frequently