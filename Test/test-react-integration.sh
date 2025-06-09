#!/bin/bash

# Integration tests for ReAct Pattern

echo "========================================"
echo "Running ReAct Pattern Integration Tests"
echo "========================================"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Ensure the app is running
echo -e "${YELLOW}Checking if application is running...${NC}"
if ! curl -k -s https://localhost:5005/api/orchestrators/conversation/health > /dev/null; then
    echo -e "${RED}Application is not running! Please start it with: python run-dev.py${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Application is running${NC}"

# Test 1: ReAct with tool detection
echo -e "\n${YELLOW}Test 1: ReAct Pattern with Tool Detection${NC}"
echo "Query: 'Vyhledej aktuální kurz EUR/CZK'"

RESPONSE=$(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Vyhledej aktuální kurz EUR/CZK",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": true
    }
  }')

echo "$RESPONSE" | jq '.'

# Validate ReAct metadata
if echo "$RESPONSE" | jq -e '.metadata.react_mode' > /dev/null; then
    REACT_STEPS=$(echo "$RESPONSE" | jq -r '.metadata.react_steps')
    REACT_THOUGHTS=$(echo "$RESPONSE" | jq -r '.metadata.react_thoughts')
    echo -e "${GREEN}✓ ReAct executed: $REACT_STEPS steps, $REACT_THOUGHTS thoughts${NC}"
else
    echo -e "${RED}✗ ReAct metadata not found${NC}"
fi

# Test 2: Multi-tool ReAct execution
echo -e "\n${YELLOW}Test 2: Multi-Tool ReAct Execution${NC}"
echo "Query: 'Porovnej počasí v Praze, Brně a Ostravě'"

RESPONSE=$(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Porovnej počasí v Praze, Brně a Ostravě",
    "modelId": "llama3.2",
    "enableTools": true,
    "maxToolCalls": 5,
    "metadata": {
      "enable_react": true
    }
  }')

TOOLS_USED=$(echo "$RESPONSE" | jq -r '.toolsConsidered | length')
echo -e "Tools considered: $TOOLS_USED"

if [ "$TOOLS_USED" -gt 0 ]; then
    echo -e "${GREEN}✓ Multiple tools considered${NC}"
else
    echo -e "${RED}✗ No tools were considered${NC}"
fi

# Test 3: ReAct with context persistence
echo -e "\n${YELLOW}Test 3: ReAct with Context Persistence${NC}"
echo "Creating conversation and testing context"

# First message
CONV_ID="test-conv-$(date +%s)"
echo "Conversation ID: $CONV_ID"

curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d "{
    \"conversationId\": \"$CONV_ID\",
    \"message\": \"Zapamatuj si, že moje oblíbené město je Praha\",
    \"modelId\": \"llama3.2\",
    \"enableTools\": false
  }" | jq '.response'

# Second message with ReAct
echo -e "\nAsking about weather in favorite city with ReAct..."

RESPONSE=$(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d "{
    \"conversationId\": \"$CONV_ID\",
    \"message\": \"Jaké je počasí v mém oblíbeném městě?\",
    \"modelId\": \"llama3.2\",
    \"enableTools\": true,
    \"metadata\": {
      \"enable_react\": true
    }
  }")

echo "$RESPONSE" | jq '.'

if echo "$RESPONSE" | jq -r '.response' | grep -i "Praha" > /dev/null; then
    echo -e "${GREEN}✓ Context preserved: Found reference to Praha${NC}"
else
    echo -e "${YELLOW}⚠ Context might not be preserved${NC}"
fi

# Test 4: ReAct error handling
echo -e "\n${YELLOW}Test 4: ReAct Error Handling${NC}"
echo "Testing with very low max iterations"

RESPONSE=$(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Analyzuj všechny aspekty umělé inteligence a její dopady na společnost",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": true,
      "react_max_iterations": 1
    }
  }')

FINISH_REASON=$(echo "$RESPONSE" | jq -r '.finishReason')
if [ "$FINISH_REASON" = "max_iterations" ]; then
    echo -e "${GREEN}✓ Correctly handled max iterations limit${NC}"
else
    echo -e "${YELLOW}Finish reason: $FINISH_REASON${NC}"
fi

# Test 5: Performance comparison
echo -e "\n${YELLOW}Test 5: Performance Comparison${NC}"
echo "Comparing execution time with and without ReAct"

# Without ReAct
START_TIME=$(date +%s%N)
curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Co je hlavní město Německa?",
    "modelId": "llama3.2",
    "enableTools": false
  }' > /dev/null
END_TIME=$(date +%s%N)
TIME_WITHOUT_REACT=$((($END_TIME - $START_TIME) / 1000000))

# With ReAct
START_TIME=$(date +%s%N)
curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Co je hlavní město Německa?",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": true
    }
  }' > /dev/null
END_TIME=$(date +%s%N)
TIME_WITH_REACT=$((($END_TIME - $START_TIME) / 1000000))

echo "Without ReAct: ${TIME_WITHOUT_REACT}ms"
echo "With ReAct: ${TIME_WITH_REACT}ms"
echo "Overhead: $((TIME_WITH_REACT - TIME_WITHOUT_REACT))ms"

# Test 6: Concurrent ReAct requests
echo -e "\n${YELLOW}Test 6: Concurrent ReAct Requests${NC}"
echo "Sending 3 concurrent requests..."

# Send concurrent requests
(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaké je počasí v Londýně?",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {"enable_react": true}
  }' > /tmp/react-test-1.json) &

(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaké je počasí v Paříži?",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {"enable_react": true}
  }' > /tmp/react-test-2.json) &

(curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaké je počasí v Berlíně?",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {"enable_react": true}
  }' > /tmp/react-test-3.json) &

# Wait for all requests to complete
wait

# Check results
SUCCESS_COUNT=0
for i in 1 2 3; do
    if [ -f "/tmp/react-test-$i.json" ] && jq -e '.success' "/tmp/react-test-$i.json" > /dev/null 2>&1; then
        ((SUCCESS_COUNT++))
    fi
done

echo -e "Successful concurrent requests: $SUCCESS_COUNT/3"
if [ $SUCCESS_COUNT -eq 3 ]; then
    echo -e "${GREEN}✓ All concurrent requests succeeded${NC}"
else
    echo -e "${RED}✗ Some concurrent requests failed${NC}"
fi

# Cleanup
rm -f /tmp/react-test-*.json

echo -e "\n${GREEN}========================================"
echo "ReAct Integration Tests Complete!"
echo "========================================${NC}"