#!/bin/bash
# Comprehensive ReAct debugging test

echo "=== ReAct Pattern Debug Test ==="
echo ""

# First, let's check if the ReAct configuration is loaded
echo "1. Checking application configuration..."
echo ""

# Test through the Chat controller endpoint
echo "2. Testing through Chat controller with ReAct enabled..."
CONVERSATION_ID="react-debug-$(date +%s)"

curl -X POST https://localhost:5005/Chat/SendMessage \
  -H "Content-Type: application/json" \
  -k -s \
  -d "{
    \"conversationId\": \"$CONVERSATION_ID\",
    \"message\": \"search on web what is OptimalyAI\",
    \"modelId\": \"llama3.2:1b\"
  }" 2>&1 | head -50

echo ""
echo "3. Checking application logs for the last 5 minutes..."
echo ""

# Get logs from the last 5 minutes
FIVE_MIN_AGO=$(date -v-5M "+%Y-%m-%d %H:%M")
echo "Looking for logs since: $FIVE_MIN_AGO"
echo ""

# Search for ReAct-related entries
tail -1000 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ReAct|react|enable_react|ReActAgent|ReActSettings)" | tail -30

echo ""
echo "4. Checking for ConversationOrchestrator activity..."
echo ""

tail -1000 logs/optimaly-ai-$(date +%Y%m%d).log | grep "ConversationOrchestrator" | grep -E "(Executing|configuration|metadata|ReAct)" | tail -20

echo ""
echo "5. Checking for any errors..."
echo ""

tail -500 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ERROR|WARN)" | grep -E "(ReAct|Orchestrator)" | tail -10

echo ""
echo "=== Debug test completed ==="