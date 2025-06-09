#!/bin/bash

# Simple test script for OptimalyAI Chat functionality

echo "=========================================="
echo "OptimalyAI Chat Test - Simplified"
echo "=========================================="

# Test if application is running
echo -e "\n1. Testing if application is running..."
APP_STATUS=$(curl -k -s -o /dev/null -w "%{http_code}" "https://localhost:5005")
if [ "$APP_STATUS" = "200" ]; then
    echo "✓ Application is running"
else
    echo "✗ Application is not accessible (status: $APP_STATUS)"
    exit 1
fi

# Test if Ollama is running
echo -e "\n2. Testing Ollama connectivity..."
OLLAMA_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:11434/api/tags")
if [ "$OLLAMA_STATUS" = "200" ]; then
    echo "✓ Ollama is running"
else
    echo "✗ Ollama is not running (required for chat)"
    exit 1
fi

# Test chat conversation creation
echo -e "\n3. Testing chat conversation..."
CONVERSATION_ID=1

# Send a test message via API
echo "Sending test message via orchestrator..."
RESPONSE=$(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
    -H "Content-Type: application/json" \
    -d '{
        "requestId": "test-'$(date +%s)'",
        "conversationId": "'$CONVERSATION_ID'",
        "message": "Hello, this is a test message",
        "modelId": "llama3.2:1b",
        "userId": "test-user",
        "sessionId": "test-session",
        "enableTools": false,
        "stream": false,
        "maxToolCalls": 0,
        "temperature": 0.7,
        "maxTokens": 100,
        "systemPrompt": "You are a helpful AI assistant."
    }')

# Check if response contains success
if echo "$RESPONSE" | grep -q '"success":true'; then
    echo "✓ Chat orchestrator responded successfully"
    
    # Extract response message
    AI_RESPONSE=$(echo "$RESPONSE" | grep -o '"response":"[^"]*"' | cut -d'"' -f4)
    echo "AI Response: $AI_RESPONSE"
else
    echo "✗ Chat orchestrator failed"
    echo "Response: $RESPONSE"
fi

# Test web search tool
echo -e "\n4. Testing web search tool..."
TOOL_RESPONSE=$(curl -k -s -X POST "https://localhost:5005/api/tools/execute" \
    -H "Content-Type: application/json" \
    -d '{
        "toolId": "web_search",
        "parameters": {
            "query": "OpenAI GPT",
            "maxResults": 3
        }
    }')

if echo "$TOOL_RESPONSE" | grep -q '"isSuccess":true'; then
    echo "✓ Web search tool executed successfully"
else
    echo "✗ Web search tool failed"
    echo "Response: $TOOL_RESPONSE"
fi

# Test chat with tool usage
echo -e "\n5. Testing chat with tool usage..."
TOOL_RESPONSE=$(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
    -H "Content-Type: application/json" \
    -d '{
        "requestId": "test-tool-'$(date +%s)'",
        "conversationId": "'$CONVERSATION_ID'",
        "message": "Search for information about Prague weather",
        "modelId": "llama3.2:1b",
        "userId": "test-user",
        "sessionId": "test-session",
        "enableTools": true,
        "stream": false,
        "maxToolCalls": 5,
        "temperature": 0.7,
        "maxTokens": 500,
        "systemPrompt": "You are a helpful AI assistant."
    }')

if echo "$TOOL_RESPONSE" | grep -q '"toolsDetected":true'; then
    echo "✓ AI detected and used tools"
    
    # Check which tools were considered
    TOOLS_CONSIDERED=$(echo "$TOOL_RESPONSE" | grep -o '"toolsConsidered":\[[^]]*\]')
    echo "Tools considered: $TOOLS_CONSIDERED"
else
    echo "✗ AI did not detect tool usage need"
fi

echo -e "\n=========================================="
echo "Test completed!"
echo "=========================================="