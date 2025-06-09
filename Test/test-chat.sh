#!/bin/bash

# Test script for OptimalyAI Chat functionality

echo "=========================================="
echo "OptimalyAI Chat Functionality Test"
echo "=========================================="

BASE_URL="https://localhost:5005"
API_URL="${BASE_URL}/api"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counter
TESTS_PASSED=0
TESTS_FAILED=0

# Function to print test result
print_result() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}✓ $2${NC}"
        ((TESTS_PASSED++))
    else
        echo -e "${RED}✗ $2${NC}"
        ((TESTS_FAILED++))
    fi
}

# Function to test endpoint
test_endpoint() {
    local endpoint=$1
    local method=$2
    local data=$3
    local expected_status=$4
    local test_name=$5
    
    echo -e "\n${YELLOW}Testing: $test_name${NC}"
    
    if [ "$method" = "GET" ]; then
        response=$(curl -k -s -o /dev/null -w "%{http_code}" "$endpoint")
    else
        response=$(curl -k -s -o /dev/null -w "%{http_code}" -X "$method" \
            -H "Content-Type: application/json" \
            -d "$data" \
            "$endpoint")
    fi
    
    if [ "$response" = "$expected_status" ]; then
        print_result 0 "Status code: $response (expected: $expected_status)"
        return 0
    else
        print_result 1 "Status code: $response (expected: $expected_status)"
        return 1
    fi
}

echo -e "\n1. Testing Basic Connectivity"
test_endpoint "$BASE_URL" "GET" "" "200" "Home page accessibility"

echo -e "\n2. Testing Chat UI"
test_endpoint "$BASE_URL/Chat" "GET" "" "200" "Chat list page"
test_endpoint "$BASE_URL/Chat/New" "GET" "" "200" "New chat page"

echo -e "\n3. Testing API Endpoints"
test_endpoint "$API_URL/chat/conversations" "GET" "" "200" "Get conversations API"

echo -e "\n4. Testing Ollama Integration"
# Test if Ollama is running
OLLAMA_STATUS=$(curl -k -s -o /dev/null -w "%{http_code}" "http://localhost:11434/api/tags")
if [ "$OLLAMA_STATUS" = "200" ]; then
    print_result 0 "Ollama server is running"
    
    # Test models endpoint
    test_endpoint "$API_URL/models" "GET" "" "200" "Get available models"
else
    print_result 1 "Ollama server is not running (required for chat)"
fi

echo -e "\n5. Testing AI Tools"
test_endpoint "$API_URL/tools" "GET" "" "200" "Get available tools"

# Test tool execution (web search)
TOOL_DATA='{
    "toolId": "web_search",
    "parameters": {
        "query": "test search",
        "maxResults": 3
    }
}'
test_endpoint "$API_URL/tools/execute" "POST" "$TOOL_DATA" "200" "Execute web search tool"

echo -e "\n6. Testing SignalR Hub"
# Check if SignalR hub is accessible
SIGNALR_NEGOTIATE=$(curl -k -s -o /dev/null -w "%{http_code}" "$BASE_URL/chatHub/negotiate")
if [ "$SIGNALR_NEGOTIATE" = "401" ] || [ "$SIGNALR_NEGOTIATE" = "200" ]; then
    print_result 0 "SignalR ChatHub is accessible"
else
    print_result 1 "SignalR ChatHub returned unexpected status: $SIGNALR_NEGOTIATE"
fi

echo -e "\n7. Testing Orchestrator"
ORCHESTRATOR_DATA='{
    "requestId": "test-001",
    "conversationId": "1",
    "message": "test message",
    "modelId": "llama3.2:1b",
    "userId": "test-user",
    "sessionId": "test-session",
    "enableTools": true,
    "stream": false,
    "maxToolCalls": 5,
    "temperature": 0.7,
    "maxTokens": 2000
}'
test_endpoint "$API_URL/orchestrators/conversation/execute" "POST" "$ORCHESTRATOR_DATA" "200" "Execute conversation orchestrator"

# Summary
echo -e "\n=========================================="
echo "Test Summary"
echo "=========================================="
echo -e "${GREEN}Tests Passed: $TESTS_PASSED${NC}"
echo -e "${RED}Tests Failed: $TESTS_FAILED${NC}"

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "\n${GREEN}All tests passed! ✓${NC}"
    exit 0
else
    echo -e "\n${RED}Some tests failed! ✗${NC}"
    exit 1
fi