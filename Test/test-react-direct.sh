#!/bin/bash

echo "Testing ReAct pattern directly..."

# Test orchestrator API with ReAct enabled
curl -k -X POST https://localhost:5005/api/orchestrators/conversation/execute \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "react-test-123",
    "conversationId": "1",
    "message": "What is the weather in Prague, Czech Republic?",
    "modelId": "llama3.2",
    "userId": "test-user",
    "sessionId": "test-session",
    "enableTools": true,
    "stream": false,
    "maxToolCalls": 5,
    "temperature": 0.7,
    "maxTokens": 2000,
    "systemPrompt": "You are a helpful AI assistant.",
    "metadata": {
      "enable_react": true
    }
  }' | jq .