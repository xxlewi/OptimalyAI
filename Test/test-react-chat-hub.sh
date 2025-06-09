#!/bin/bash
# Test ReAct through ChatHub (SignalR)

echo "=== Testing ReAct through Chat Interface ==="
echo ""

# Generate a unique connection ID
CONNECTION_ID=$(uuidgen)
CONVERSATION_ID="react-test-$(date +%s)"

echo "Connection ID: $CONNECTION_ID"
echo "Conversation ID: $CONVERSATION_ID"
echo ""

# Test message that should trigger ReAct
MESSAGE="search on web what is OptimalyAI and how does it work"

echo "Sending message: $MESSAGE"
echo ""

# Use curl to simulate SignalR negotiation and send message
# Note: This is a simplified test - in reality SignalR uses WebSockets
curl -X POST https://localhost:5005/Chat/Send \
  -H "Content-Type: application/json" \
  -k -s \
  -d "{
    \"conversationId\": \"$CONVERSATION_ID\",
    \"message\": \"$MESSAGE\",
    \"modelId\": \"llama3.2:1b\"
  }" | jq '.'

echo ""
echo "=== Checking logs for ReAct activity ==="
echo ""

# Wait a moment for processing
sleep 2

# Check logs for ReAct activity
tail -100 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ReAct|react|React|enable_react)" | tail -20