#!/bin/bash

echo "Testing ReAct pattern with logging..."
echo "==================================="

# Create a unique conversation ID
CONV_ID=$((RANDOM % 1000))
echo "Conversation ID: $CONV_ID"

# First, create a conversation
echo "1. Creating conversation..."
CONV_RESPONSE=$(curl -s -k -X POST https://localhost:5005/api/conversations \
  -H "Content-Type: application/json" \
  -d "{
    \"title\": \"ReAct Test $CONV_ID\",
    \"model\": \"llama3.2\",
    \"systemPrompt\": \"You are a helpful AI assistant.\"
  }")

echo "2. Sending message with tools enabled..."
# Send a message that should trigger tool usage
MSG_RESPONSE=$(curl -s -k -X POST https://localhost:5005/api/conversations/$CONV_ID/messages \
  -H "Content-Type: application/json" \
  -d '{
    "content": "What is the weather in Prague?",
    "role": "user"
  }')

echo "3. Checking last 50 log entries for ReAct..."
tail -50 logs/optimaly-ai-$(date +%Y%m%d).log | grep -i "react"

echo ""
echo "4. Checking for tool execution in logs..."
tail -50 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(Tool analysis|tool_analysis|ToolExecutor)"

echo ""
echo "Test completed. Check logs above for ReAct activity."