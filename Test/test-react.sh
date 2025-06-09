#!/bin/bash

# Test ReAct Pattern implementation

echo "=================================================="
echo "Testing ReAct Pattern - Conversation Orchestrator"
echo "=================================================="

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# API base URL
API_URL="https://localhost:5005/api"

# Test 1: Simple ReAct test with search
echo -e "\n${YELLOW}Test 1: Simple search with ReAct${NC}"
echo "Testing: 'Jaké je počasí v Praze?'"

curl -k -X POST "$API_URL/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaké je počasí v Praze?",
    "modelId": "llama3.2",
    "enableTools": true,
    "maxToolCalls": 3,
    "metadata": {
      "enable_react": true
    }
  }' | jq '.'

echo -e "\n${GREEN}✓ Test 1 complete${NC}"

# Test 2: Complex query that should auto-enable ReAct
echo -e "\n${YELLOW}Test 2: Complex query - auto ReAct activation${NC}"
echo "Testing: 'Porovnej počasí v Praze a New Yorku'"

curl -k -X POST "$API_URL/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Porovnej počasí v Praze a New Yorku",
    "modelId": "llama3.2",
    "enableTools": true,
    "maxToolCalls": 5
  }' | jq '.'

echo -e "\n${GREEN}✓ Test 2 complete${NC}"

# Test 3: Multi-step reasoning
echo -e "\n${YELLOW}Test 3: Multi-step reasoning${NC}"
echo "Testing: 'Najdi informace o Pythonu a poté mi vysvětli, proč je populární pro AI'"

curl -k -X POST "$API_URL/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Najdi informace o programovacím jazyce Python a poté mi vysvětli, proč je populární pro AI",
    "modelId": "llama3.2",
    "enableTools": true,
    "maxToolCalls": 3,
    "metadata": {
      "enable_react": true,
      "react_max_iterations": 5
    }
  }' | jq '.'

echo -e "\n${GREEN}✓ Test 3 complete${NC}"

# Test 4: Check ReAct metadata in response
echo -e "\n${YELLOW}Test 4: Check ReAct metadata${NC}"
echo "Testing ReAct metadata in response"

RESPONSE=$(curl -k -s -X POST "$API_URL/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Co je hlavní město České republiky?",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": true
    }
  }')

echo "$RESPONSE" | jq '.'

# Check if response contains ReAct metadata
if echo "$RESPONSE" | jq -e '.metadata.react_mode' > /dev/null; then
    echo -e "\n${GREEN}✓ ReAct metadata found in response${NC}"
    echo "ReAct steps: $(echo "$RESPONSE" | jq '.metadata.react_steps')"
    echo "ReAct thoughts: $(echo "$RESPONSE" | jq '.metadata.react_thoughts')"
    echo "ReAct actions: $(echo "$RESPONSE" | jq '.metadata.react_actions')"
else
    echo -e "\n${RED}✗ ReAct metadata not found in response${NC}"
fi

echo -e "\n${GREEN}✓ Test 4 complete${NC}"

# Test 5: Test without ReAct (comparison)
echo -e "\n${YELLOW}Test 5: Same query WITHOUT ReAct (for comparison)${NC}"
echo "Testing: 'Jaké je počasí v Praze?' without ReAct"

curl -k -X POST "$API_URL/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaké je počasí v Praze?",
    "modelId": "llama3.2",
    "enableTools": true,
    "maxToolCalls": 3,
    "metadata": {
      "enable_react": false
    }
  }' | jq '.'

echo -e "\n${GREEN}✓ Test 5 complete${NC}"

echo -e "\n${GREEN}=================================================="
echo "All ReAct Pattern tests completed!"
echo "==================================================${NC}"