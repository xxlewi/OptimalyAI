#!/bin/bash

# Test script for LLM Tornado integration with ConversationOrchestrator

echo "=========================================="
echo "LLM Tornado Orchestrator Integration Test"
echo "=========================================="

BASE_URL="https://localhost:5005"
API_URL="${BASE_URL}/api/orchestrators/conversation/execute"

# Function to test orchestrator
test_orchestrator() {
    local message=$1
    local description=$2
    
    echo -e "\nTesting: $description"
    echo "Message: $message"
    
    RESPONSE=$(curl -k -s -X POST "$API_URL" \
        -H "Content-Type: application/json" \
        -d "{
            \"message\": \"$message\",
            \"modelId\": \"llama3.2:1b\",
            \"userId\": \"test-user\",
            \"sessionId\": \"test-session-$(date +%s)\",
            \"conversationId\": \"test-conversation-1\",
            \"systemPrompt\": \"You are a helpful AI assistant.\",
            \"maxToolCalls\": 3,
            \"enableTools\": true,
            \"stream\": false
        }")
    
    if echo "$RESPONSE" | grep -q '"success":true'; then
        echo "✓ Success"
        
        # Check if tools were used
        if echo "$RESPONSE" | grep -q '"toolsUsed"'; then
            echo "Tools used:"
            echo "$RESPONSE" | jq -r '.data.toolsUsed[]' 2>/dev/null | sed 's/^/  - /'
        fi
        
        # Show AI response
        echo "Response:"
        echo "$RESPONSE" | jq -r '.data.messages[-1].content' 2>/dev/null | head -3
    else
        echo "✗ Failed"
        echo "Error: $(echo "$RESPONSE" | jq -r '.message' 2>/dev/null || echo "$RESPONSE")"
    fi
}

# Test 1: Web Search (should use web_search tool)
test_orchestrator "vyhledej informace o počasí v Praze" "Web search in Czech"

# Test 2: Analysis (should use llm_tornado tool)
test_orchestrator "analyzuj výhody a nevýhody elektromobilů" "Analysis task in Czech"

# Test 3: Translation (should use llm_tornado tool)
test_orchestrator "přelož 'Hello world' do češtiny" "Translation task"

# Test 4: Generation (should use llm_tornado tool)
test_orchestrator "vygeneruj krátký příběh o robotovi" "Content generation in Czech"

# Test 5: Summary (should use llm_tornado tool)
test_orchestrator "shrň hlavní body umělé inteligence" "Summarization task"

# Test 6: Multiple tools (might use both web_search and llm_tornado)
test_orchestrator "najdi aktuální kurz EUR a analyzuj trend" "Multi-tool task"

echo -e "\n=========================================="
echo "Test completed!"
echo "=========================================="