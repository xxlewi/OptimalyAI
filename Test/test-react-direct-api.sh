#!/bin/bash
# Direct API test for ReAct pattern

echo "=== Testing ReAct Pattern via Direct API ==="
echo ""

# Test 1: Check available tools first
echo "1. Checking available tools..."
curl -X GET https://localhost:5005/api/tools \
  -H "Content-Type: application/json" \
  -k -s | jq '.data[] | {id, name, category, isEnabled}' | head -20

echo ""
echo "2. Testing web search tool directly..."
curl -X POST https://localhost:5005/api/tools/execute \
  -H "Content-Type: application/json" \
  -k -s \
  -d '{
    "toolId": "web_search",
    "parameters": {
      "query": "OptimalyAI ASP.NET",
      "maxResults": 3
    }
  }' | jq '.'

echo ""
echo "3. Monitoring logs during execution..."
echo ""

# Start monitoring logs in background
tail -f logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ReAct|react|ConversationOrchestrator|Tool|enable_react)" &
LOG_PID=$!

# Wait a moment
sleep 2

# Now send a chat message
echo "4. Sending chat message to trigger ReAct..."
CONVERSATION_ID="react-api-test-$(date +%s)"

# Try using the chat API endpoint
curl -X POST https://localhost:5005/api/chat/send \
  -H "Content-Type: application/json" \
  -k -s \
  -d "{
    \"conversationId\": \"$CONVERSATION_ID\",
    \"message\": \"search what is OptimalyAI\",
    \"modelId\": \"llama3.2:1b\",
    \"userId\": \"test-user\",
    \"enableTools\": true,
    \"metadata\": {
      \"enable_react\": true
    }
  }" 2>&1

echo ""
echo ""

# Wait for processing
sleep 5

# Kill the log monitoring
kill $LOG_PID 2>/dev/null

echo ""
echo "5. Checking final log state..."
tail -50 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ReAct|react|enable_react|GetReActMode)" | tail -20

echo ""
echo "=== Test completed ==="