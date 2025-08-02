# Security Tests for OCR API Endpoints

This document describes the comprehensive security tests implemented in `SecurityTests.cs` for the OCR API endpoints.

## Overview

The `SecurityTests.cs` file contains comprehensive security tests covering multiple attack vectors and security concerns for the OCR API endpoints (`/ocr/receipts` and `/ocr/checks`).

## Test Categories

### 1. Input Validation and Sanitization Tests

- **Null Request Rejection**: Ensures endpoints properly handle null requests
- **Empty Content Handling**: Tests behavior with empty payloads
- **Large Payload Handling**: Tests with payloads of various sizes (10MB, 50MB, 100MB)
- **Invalid Image Data**: Tests handling of non-image data sent as image content

### 2. SQL Injection Prevention Tests

Tests various SQL injection patterns:
- `'; DROP TABLE users; --`
- `' OR '1'='1`
- `admin'--`
- `' UNION SELECT * FROM users --`
- `'; EXEC xp_cmdshell('dir'); --`

Verifies that:
- No database errors are exposed in responses
- SQL injection payloads don't cause system crashes
- No sensitive database information leaks through error messages

### 3. Path Traversal Attack Prevention Tests

Tests various path traversal patterns:
- `../../../etc/passwd`
- `..\\..\\..\\windows\\system32\\config\\sam`
- `....//....//....//etc//passwd`
- URL-encoded variations (`%2e%2e%2f...`)

Verifies that:
- File system access errors don't leak sensitive path information
- Path traversal attempts don't expose system files
- No "access denied" or "file not found" errors reveal system structure

### 4. XSS (Cross-Site Scripting) Prevention Tests

Tests various XSS payloads:
- `<script>alert('XSS')</script>`
- `<img src=x onerror=alert('XSS')>`
- `javascript:alert('XSS')`
- `<svg onload=alert('XSS')>`
- `<iframe src=javascript:alert('XSS')></iframe>`

Verifies that:
- Dangerous HTML/JavaScript is properly escaped or sanitized
- Response JSON doesn't contain unescaped XSS payloads
- Content-Type headers are properly set with security flags

### 5. Authorization and Authentication Tests

- **Public Access Verification**: Confirms endpoints are publicly accessible (as designed)
- **Invalid Authentication Handling**: Tests behavior with invalid tokens/credentials
- **Token Leakage Prevention**: Ensures invalid authentication tokens don't appear in responses

### 6. Rate Limiting and DoS Prevention Tests

- **Concurrent Request Handling**: Tests with 10 simultaneous requests
- **Resource Exhaustion Protection**: Verifies graceful handling of concurrent load
- **Response Code Validation**: Ensures appropriate status codes (200, 429, 503)

### 7. Content Type Security Tests

- **Content-Type Header Validation**: Tests with various incorrect content types
- **MIME Type Enforcement**: Verifies handling of non-image MIME types
- **Security Header Verification**: Checks for security headers like `X-Content-Type-Options`

## Security Test Utilities

### Helper Methods

- **`CreateMaliciousImageWithText()`**: Creates image data with embedded malicious text
- **`AssertNoXssInResponse()`**: Validates that JSON responses don't contain XSS payloads
- **`AssertNoXssInText()`**: Ensures text content is properly sanitized

## Running the Security Tests

```bash
# Run all security tests
dotnet test --filter "FullyQualifiedName~SecurityTests"

# Run specific category of security tests
dotnet test --filter "FullyQualifiedName~SecurityTests.ReceiptsEndpoint_PreventsSqlInjection"

# Run with verbose output
dotnet test --filter "FullyQualifiedName~SecurityTests" --logger "console;verbosity=detailed"
```

## Test Results Interpretation

### Expected Behaviors

1. **Graceful Error Handling**: Tests should pass when the API handles malicious input gracefully without crashing
2. **No Information Disclosure**: Error messages should not reveal sensitive system information
3. **Proper Status Codes**: APIs should return appropriate HTTP status codes
4. **Content Sanitization**: Responses should not contain unsanitized malicious content

### Failure Analysis

If security tests fail, investigate:

1. **Exception Leakage**: Are internal exceptions exposing sensitive information?
2. **Input Validation**: Are malicious inputs being properly validated and rejected?
3. **Output Encoding**: Are responses properly encoded to prevent XSS?
4. **Error Messages**: Are error messages revealing too much system information?

## Security Best Practices Validated

- ✅ Input validation and sanitization
- ✅ SQL injection prevention
- ✅ Path traversal attack prevention
- ✅ XSS prevention in API responses
- ✅ Proper error handling without information disclosure
- ✅ Content-type security
- ✅ Concurrent request handling
- ✅ Authentication token security

## Continuous Security Testing

These tests should be:
- Run as part of the CI/CD pipeline
- Executed before each release
- Updated when new attack vectors are discovered
- Extended when new endpoints are added

## Future Enhancements

Consider adding tests for:
- CSRF protection (if authentication is added)
- XML External Entity (XXE) attacks (if XML processing is added)
- Server-Side Request Forgery (SSRF) prevention
- Command injection tests
- Deserialization attack prevention