#!/bin/bash
# Set environment variables for OCR Services
# Copy this file to set-env-vars.local.sh and fill in your actual values

export MISTRAL_API_KEY="your-mistral-api-key-here"
export CLOUDFLARE_ACCOUNT_ID="your-cloudflare-account-id-here"
export CLOUDFLARE_API_TOKEN="your-cloudflare-api-token-here"

echo "Environment variables set:"
echo "- MISTRAL_API_KEY: ${MISTRAL_API_KEY:0:10}..."
echo "- CLOUDFLARE_ACCOUNT_ID: ${CLOUDFLARE_ACCOUNT_ID:0:10}..."
echo "- CLOUDFLARE_API_TOKEN: ${CLOUDFLARE_API_TOKEN:0:10}..."