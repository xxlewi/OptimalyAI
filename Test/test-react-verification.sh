#!/bin/bash
# Test ReAct pattern verification with detailed logging

echo "=== Testing ReAct Pattern Verification ==="
echo "Checking if ReAct is properly enabled and working"
echo ""

# Test 1: Simple search query that should trigger ReAct
echo "Test 1: Simple search query with ReAct enabled"
curl -X POST https://localhost:5005/api/orchestrators/conversation/execute \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "message": "search on web what is OptimalyAI",
    "modelId": "llama3.2:1b",
    "conversationId": "test-react-verify-'$(date +%s)'",
    "userId": "test-user",
    "enableTools": true,
    "maxToolCalls": 3,
    "metadata": {
      "enable_react": true
    }
  }' | jq '.'

echo ""
echo "----------------------------------------"
echo ""

# Test 2: Complex query that should auto-enable ReAct
echo "Test 2: Complex query (should auto-enable ReAct)"
curl -X POST https://localhost:5005/api/orchestrators/conversation/execute \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "message": "Compare the weather in Prague and Brno, then tell me which city has better conditions for outdoor activities",
    "modelId": "llama3.2:1b",
    "conversationId": "test-react-complex-'$(date +%s)'",
    "userId": "test-user",
    "enableTools": true,
    "maxToolCalls": 5
  }' | jq '.'

echo ""
echo "----------------------------------------"
echo ""

# Test 3: Direct ReAct health check
echo "Test 3: Checking ReAct Agent availability"
curl -X GET https://localhost:5005/api/tools \
  -H "Content-Type: application/json" \
  -k | jq '.data[] | select(.category == "AI") | {id, name, isEnabled}'

echo ""
echo "=== Checking recent logs for ReAct activity ==="
echo ""

# Show recent logs filtered for ReAct
python run-dev.py logs 100 | grep -i "react\|ReAct" | tail -20

echo ""
echo "=== Test completed ==="