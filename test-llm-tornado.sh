#!/bin/bash

# Test script for LLM Tornado Tool

echo "=========================================="
echo "LLM Tornado Tool Test"
echo "=========================================="

BASE_URL="https://localhost:5005"
API_URL="${BASE_URL}/api/tools/execute"

# Function to test LLM Tornado actions
test_llm_tornado() {
    local action=$1
    local provider=$2
    local description=$3
    local data=$4
    
    echo -e "\nTesting: $description"
    echo "Provider: $provider, Action: $action"
    
    RESPONSE=$(curl -k -s -X POST "$API_URL" \
        -H "Content-Type: application/json" \
        -d "$data")
    
    if echo "$RESPONSE" | grep -q '"isSuccess":true'; then
        echo "✓ Success"
        echo "Response: $(echo "$RESPONSE" | jq -r '.data.data' 2>/dev/null || echo "$RESPONSE")"
    else
        echo "✗ Failed"
        echo "Error: $(echo "$RESPONSE" | jq -r '.data.error' 2>/dev/null || echo "$RESPONSE")"
    fi
}

# Test 1: List models from Ollama
echo -e "\n1. Testing List Models (Ollama)"
test_llm_tornado "list_models" "ollama" "List available Ollama models" '{
    "toolId": "llm_tornado",
    "parameters": {
        "provider": "ollama",
        "action": "list_models"
    }
}'

# Test 2: Chat with Ollama
echo -e "\n2. Testing Chat Completion (Ollama)"
test_llm_tornado "chat" "ollama" "Simple chat with Ollama" '{
    "toolId": "llm_tornado",
    "parameters": {
        "provider": "ollama",
        "action": "chat",
        "model": "llama3.2:1b",
        "messages": [
            {"role": "system", "content": "You are a helpful assistant."},
            {"role": "user", "content": "What is 2+2? Answer in one word."}
        ],
        "temperature": 0.1,
        "max_tokens": 10
    }
}'

# Test 3: Completion with Ollama
echo -e "\n3. Testing Text Completion (Ollama)"
test_llm_tornado "completion" "ollama" "Text completion with Ollama" '{
    "toolId": "llm_tornado",
    "parameters": {
        "provider": "ollama",
        "action": "completion",
        "model": "llama3.2:1b",
        "prompt": "The capital of France is",
        "temperature": 0.1,
        "max_tokens": 5
    }
}'

# Test 4: Structured Output (requires function calling support)
echo -e "\n4. Testing Structured Output (Ollama)"
test_llm_tornado "structured_output" "ollama" "Generate structured JSON output" '{
    "toolId": "llm_tornado",
    "parameters": {
        "provider": "ollama",
        "action": "structured_output",
        "model": "llama3.2:1b",
        "prompt": "Generate a person with name and age",
        "schema": {
            "type": "object",
            "properties": {
                "name": {"type": "string"},
                "age": {"type": "integer"}
            },
            "required": ["name", "age"]
        },
        "temperature": 0.5
    }
}'

# Test 5: Verify tool is registered
echo -e "\n5. Checking if LLM Tornado tool is registered"
TOOLS_RESPONSE=$(curl -k -s "https://localhost:5005/api/tools")

if echo "$TOOLS_RESPONSE" | grep -q '"id":"llm_tornado"'; then
    echo "✓ LLM Tornado tool is registered"
    
    # Extract tool info
    TOOL_INFO=$(echo "$TOOLS_RESPONSE" | jq '.data[] | select(.id=="llm_tornado")' 2>/dev/null)
    if [ ! -z "$TOOL_INFO" ]; then
        echo "Tool Name: $(echo "$TOOL_INFO" | jq -r '.name')"
        echo "Category: $(echo "$TOOL_INFO" | jq -r '.category')"
        echo "Description: $(echo "$TOOL_INFO" | jq -r '.description')"
        echo "Version: $(echo "$TOOL_INFO" | jq -r '.version')"
    fi
else
    echo "✗ LLM Tornado tool not found in registry"
fi

echo -e "\n=========================================="
echo "Test completed!"
echo "=========================================="