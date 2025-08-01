# OCR Async Endpoint with Document Type

## Overview
The `/ocr/async` endpoint has been updated to support document type recognition. It now accepts a query parameter to specify whether the image contains a check or receipt.

## Endpoint Details

**URL**: `POST /ocr/async?documentType={type}`

**Query Parameters**:
- `documentType` (required): The type of document to recognize. Valid values:
  - `check` - Bank check or money order
  - `receipt` - Purchase or transaction receipt

**Request Body**: Binary image data (stream)

**Content-Type**: `application/octet-stream`

## Processing Flow

1. The endpoint receives an image stream and document type
2. Converts the image to a data URL format
3. Processes the image through Mistral OCR to extract text
4. Uses Cloudflare AI to extract structured data based on the document type
5. Returns both the raw OCR text and structured data

## Response Format

```json
{
  "documentType": "check",
  "ocrText": "Raw OCR text from the image...",
  "extractedData": {
    // Structured data specific to document type
  },
  "confidence": 0.85,
  "processingTimeMs": 1523
}
```

### Check Response Example
```json
{
  "documentType": "check",
  "ocrText": "First National Bank\nCheck #5432...",
  "extractedData": {
    "checkNumber": "5432",
    "amount": "245.67",
    "payee": "Electric Company",
    "date": "03/15/2024",
    "bank": "First National Bank",
    "routingNumber": "123456789",
    "accountNumber": "987654321",
    "memo": "March bill",
    "confidence": 0.85
  },
  "confidence": 0.85,
  "processingTimeMs": 1523
}
```

### Receipt Response Example
```json
{
  "documentType": "receipt",
  "ocrText": "COFFEE SHOP\n123 Main St...",
  "extractedData": {
    "merchant": {
      "name": "Coffee Shop",
      "address": "123 Main St"
    },
    "items": [
      {
        "name": "Cappuccino Large",
        "price": "4.50",
        "quantity": 1
      }
    ],
    "totals": {
      "subtotal": "7.75",
      "tax": "0.62",
      "total": "8.37"
    },
    "paymentMethod": "VISA ****1234",
    "transactionDate": "03/15/2024",
    "transactionTime": "08:30 AM",
    "confidence": 0.90
  },
  "confidence": 0.90,
  "processingTimeMs": 1834
}
```

## Error Responses

### Missing Document Type
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Document type is required. Valid values: check, receipt"
}
```

### OCR Processing Failed
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Failed to extract text from image"
}
```

### Data Extraction Failed
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Failed to extract check data: [error message]"
}
```

## Configuration

The endpoint requires configuration for both Mistral OCR and Cloudflare AI services:

```json
{
  "MistralOcr": {
    "ApiKey": "YOUR_MISTRAL_API_KEY",
    "Model": "mistral-ocr-latest"
  },
  "CloudflareAI": {
    "AccountId": "YOUR_CLOUDFLARE_ACCOUNT_ID",
    "ApiToken": "YOUR_CLOUDFLARE_API_TOKEN"
  }
}
```

## Usage Example (cURL)

```bash
# Process a check image
curl -X POST "http://localhost:5000/ocr/async?documentType=check" \
  -H "Content-Type: application/octet-stream" \
  --data-binary "@check-image.png"

# Process a receipt image
curl -X POST "http://localhost:5000/ocr/async?documentType=receipt" \
  -H "Content-Type: application/octet-stream" \
  --data-binary "@receipt-image.png"
```

## Dependencies

- Mistral OCR for text extraction
- Cloudflare Workers AI for structured data extraction
- Document type models from `Nolock.social.CloudflareAI.JsonExtraction.Models`