#!/bin/bash

# Get the form and extract CSRF token
echo "Getting CSRF token..."
RESPONSE=$(curl -s -k https://localhost:5005/Projects/Create)
TOKEN=$(echo "$RESPONSE" | grep -oP '(?<=__RequestVerificationToken" type="hidden" value=")[^"]+' | head -1)

if [ -z "$TOKEN" ]; then
    echo "Failed to get CSRF token"
    exit 1
fi

echo "Token: $TOKEN"

# Create project
echo "Creating project..."
curl -k -X POST https://localhost:5005/Projects/Create \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -H "Cookie: .AspNetCore.Antiforgery.xxx=test" \
  -d "__RequestVerificationToken=$TOKEN" \
  -d "Name=Test+Project+$(date +%s)" \
  -d "Description=Test+description" \
  -d "ProjectType=DemandAnalyzer" \
  -d "CustomerName=Test+Customer" \
  -v 2>&1 | grep -E "(HTTP|Location|Set-Cookie)"