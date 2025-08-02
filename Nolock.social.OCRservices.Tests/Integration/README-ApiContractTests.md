# API Contract Tests

This document describes the comprehensive API contract tests for the OCR services endpoints.

## Overview

The `ApiContractTests.cs` file contains thorough contract validation tests that ensure the API meets its specifications across multiple dimensions:

## Test Categories

### 1. Response Schema Validation
- **Purpose**: Validates that API responses match expected JSON schemas
- **Implementation**: Uses Newtonsoft.Json.Schema for precise schema validation
- **Coverage**: Both `ReceiptOcrResponse` and `CheckOcrResponse` models
- **Key Features**:
  - Required field validation
  - Data type validation
  - Value range validation (e.g., confidence scores 0.0-1.0)
  - Additional properties prevention

### 2. HTTP Status Code Verification
- **Purpose**: Ensures correct HTTP status codes for various scenarios
- **Test Cases**:
  - Valid requests return `200 OK`
  - Invalid content types return `400 Bad Request`
  - Empty payloads return `400 Bad Request`
  - Unsupported methods return `405 Method Not Allowed`
  - Non-existent endpoints return `404 Not Found`

### 3. Content-Type Validation
- **Purpose**: Verifies proper Content-Type headers in responses
- **Validation**:
  - Success responses: `application/json; charset=utf-8`
  - Error responses: `application/json` or `application/problem+json`
  - Character encoding specification

### 4. API Versioning Compliance
- **Purpose**: Ensures API versioning standards are met
- **Coverage**:
  - Swagger/OpenAPI endpoint accessibility (`/swagger/v1/swagger.json`)
  - Version specification in OpenAPI document
  - Swagger UI availability (`/swagger`)

### 5. OpenAPI Spec Compliance
- **Purpose**: Validates OpenAPI specification correctness
- **Validation Points**:
  - Valid OpenAPI document structure
  - Proper endpoint documentation
  - Correct operation IDs and tags
  - Parameter specifications
  - Response schema definitions

## Test Structure

### Endpoints Tested
- `POST /ocr/receipts` - Receipt OCR processing
- `POST /ocr/checks` - Check OCR processing
- `GET /swagger/v1/swagger.json` - OpenAPI specification
- `GET /swagger` - Swagger UI

### Helper Methods
- `CreateValidImageContent()` - Creates test receipt image content
- `CreateValidCheckImageContent()` - Creates test check image content
- `GetReceiptOcrResponseSchema()` - Defines expected receipt response schema
- `GetCheckOcrResponseSchema()` - Defines expected check response schema

## Running the Tests

### Prerequisites
For full test coverage, configure these environment variables:
```bash
export MISTRAL_API_KEY="your-mistral-api-key"
export CLOUDFLARE_ACCOUNT_ID="your-cloudflare-account-id"
export CLOUDFLARE_API_TOKEN="your-cloudflare-api-token"
```

### Test Execution
```bash
# Run all API contract tests
dotnet test --filter "FullyQualifiedName~ApiContractTests"

# Run specific test categories
dotnet test --filter "FullyQualifiedName~ApiContractTests.SwaggerEndpoint"
dotnet test --filter "FullyQualifiedName~ApiContractTests.ReceiptsEndpoint"
dotnet test --filter "FullyQualifiedName~ApiContractTests.ChecksEndpoint"
```

### Expected Behavior
- **With API Keys**: All tests should pass
- **Without API Keys**: OpenAPI/Swagger tests pass, OCR endpoint tests may fail due to service dependencies

## Dependencies

The tests use these additional NuGet packages:
- `Newtonsoft.Json.Schema` (4.0.1) - JSON schema validation
- `System.Text.Json` (9.0.1) - JSON serialization
- `Microsoft.AspNetCore.Mvc.Testing` (9.0.1) - Integration testing

## Best Practices

1. **Schema Evolution**: Update JSON schemas when response models change
2. **Error Scenarios**: Add tests for new error conditions
3. **Version Changes**: Update version validation when API versions change
4. **Performance**: Consider adding performance assertions for critical endpoints
5. **Security**: Extend with authentication/authorization contract tests as needed

## Troubleshooting

### Common Issues
1. **Missing API Keys**: Configure environment variables as shown above
2. **Schema Validation Failures**: Check for model changes and update schemas
3. **Network Issues**: Ensure test environment has external API access
4. **Build Errors**: Restore NuGet packages with `dotnet restore`

### Debugging Tips
- Enable verbose test output: `dotnet test --verbosity detailed`
- Check actual vs. expected responses in test failure messages
- Validate OpenAPI spec manually at `/swagger` endpoint