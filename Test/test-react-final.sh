#!/bin/bash
# Final test to verify ReAct is working

echo "=== Final ReAct Pattern Test ==="
echo ""

# Wait for application to fully start
sleep 3

# Create a new conversation
echo "1. Creating new conversation..."
CONVERSATION_RESPONSE=$(curl -X POST https://localhost:5005/Chat/CreateConversation \
  -H "Content-Type: application/json" \
  -k -s \
  -d '{
    "title": "ReAct Test - '"$(date +%s)"'",
    "model": "llama3.2:1b",
    "systemPrompt": "You are a helpful AI assistant that uses tools when needed."
  }')

CONVERSATION_ID=$(echo $CONVERSATION_RESPONSE | jq -r '.conversationId')
echo "Created conversation: $CONVERSATION_ID"
echo ""

# Send a message that should trigger ReAct
echo "2. Sending search message..."
MESSAGE_RESPONSE=$(curl -X POST https://localhost:5005/Chat/SendMessage \
  -H "Content-Type: application/json" \
  -k -s \
  -d "{
    \"conversationId\": $CONVERSATION_ID,
    \"message\": \"search on web what is OptimalyAI and how does it work\",
    \"model\": \"llama3.2:1b\"
  }")

echo "Response received"
echo ""

# Monitor logs for ReAct activity
echo "3. Checking logs for ReAct activity..."
sleep 2

# Check for ReAct entries
tail -500 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ReAct|react|enable_react|GetReActMode|ExecuteWithReActAsync)" | tail -30

echo ""
echo "4. Checking for tool execution..."
tail -500 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(web_search|Tool execution|ToolExecutor)" | tail -20

echo ""
echo "5. Checking ConversationOrchestrator logs..."
tail -500 logs/optimaly-ai-$(date +%Y%m%d).log | grep "ConversationOrchestrator" | grep -E "(Executing|metadata|configuration)" | tail -20

echo ""
echo "=== Test completed ==="
echo ""
echo "To manually test:"
echo "1. Open https://localhost:5005/Chat"
echo "2. Create a new conversation"
echo "3. Type: 'search what is OptimalyAI'"
echo "4. Watch the logs with: tail -f logs/optimaly-ai-$(date +%Y%m%d).log | grep -i react"