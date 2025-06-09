#!/bin/bash

echo "Testing orchestrator validation..."

# Test orchestrator API directly
curl -k -X POST https://localhost:5005/api/orchestrators/conversation/execute \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "test-123",
    "conversationId": "1",
    "message": "search what is optimaly.net",
    "modelId": "llama3.2:1b",
    "userId": "test-user",
    "sessionId": "test-session",
    "enableTools": true,
    "stream": false,
    "maxToolCalls": 5,
    "temperature": 0.7,
    "maxTokens": 2000,
    "systemPrompt": "You are a helpful AI assistant."
  }' | jq .