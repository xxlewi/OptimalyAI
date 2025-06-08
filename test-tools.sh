#!/bin/bash

echo "🚀 Testing OptimalyAI Tools API"
echo "================================"

# Wait for app to be ready
echo "⏳ Waiting for application to start..."
sleep 10

# Test API endpoint
echo "📡 Testing /api/tools endpoint..."
curl -k -s https://localhost:5005/api/tools | python -m json.tool

echo ""
echo "📡 Testing /api/tools/debug endpoint..."
curl -k -s https://localhost:5005/api/tools/debug | python -m json.tool

echo ""
echo "✅ Test complete!"