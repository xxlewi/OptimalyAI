#!/bin/bash
# Jednoduchý test ReAct patternu

echo "=== Test ReAct Pattern - Simple Check ==="
echo ""

# 1. Test přímého volání Ollama
echo "1. Test Ollama API..."
OLLAMA_RESPONSE=$(curl -s -X POST http://localhost:11434/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "model": "llama3.2:1b",
    "messages": [{"role": "user", "content": "Reply with: Hello from Ollama"}],
    "stream": false
  }')

echo "Ollama response: $(echo $OLLAMA_RESPONSE | jq -r '.message.content' | head -c 50)..."
echo ""

# 2. Vytvoření nové konverzace
echo "2. Vytváření konverzace..."
CONV_RESPONSE=$(curl -s -X POST https://localhost:5005/Chat/CreateConversation \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "title": "ReAct Test",
    "model": "llama3.2:1b"
  }')

CONV_ID=$(echo $CONV_RESPONSE | jq -r '.conversationId')
echo "Conversation ID: $CONV_ID"
echo ""

# 3. Poslání jednoduché zprávy
echo "3. Posílám jednoduchou zprávu..."
MSG_RESPONSE=$(curl -s -X POST https://localhost:5005/Chat/SendMessage \
  -H "Content-Type: application/json" \
  -k \
  -d "{
    \"conversationId\": $CONV_ID,
    \"message\": \"Just say hello\",
    \"model\": \"llama3.2:1b\"
  }")

echo "Response status: $(echo $MSG_RESPONSE | jq -r '.success' 2>/dev/null || echo 'ERROR')"
echo ""

# 4. Kontrola logů
echo "4. Poslední logy s ReAct..."
tail -100 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ReAct|enable_react|Executing with)" | tail -10

echo ""
echo "5. Poslední chyby..."
tail -100 logs/optimaly-ai-$(date +%Y%m%d).log | grep -E "(ERROR|404)" | tail -5

echo ""
echo "=== Test dokončen ==="