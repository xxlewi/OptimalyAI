#!/bin/bash

# Test konverze zákaznického požadavku na projekt

echo "Test konverze požadavku na projekt"
echo "=================================="

# Test ID schváleného požadavku (musíte nahradit skutečným ID)
REQUEST_ID="12345678-1234-1234-1234-123456789012"

# Volání API pro konverzi
echo "Konvertuji požadavek $REQUEST_ID na projekt..."

curl -X POST "https://localhost:5005/api/customer-requests/${REQUEST_ID}/convert-to-project" \
  -H "Content-Type: application/json" \
  -k \
  | jq '.'

echo ""
echo "Test dokončen!"