#!/bin/bash

# Simple ReAct Pattern test

echo "Testing ReAct Pattern - Simple Query"
echo "===================================="

# Test with ReAct enabled
echo -e "\nQuery: 'Vyhledej informace o počasí v Praze'"
echo "ReAct: ENABLED"

curl -k -s -X POST "https://localhost:5005/api/orchestrators/conversation/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Vyhledej informace o počasí v Praze",
    "modelId": "llama3.2",
    "enableTools": true,
    "metadata": {
      "enable_react": true
    }
  }' | jq '{
    success: .success,
    response: .response,
    toolsDetected: .toolsDetected,
    toolsUsed: .toolsConsidered | length,
    reactMetadata: .metadata | {
      reactMode: .react_mode,
      steps: .react_steps,
      thoughts: .react_thoughts,
      actions: .react_actions,
      executionTime: .react_execution_time
    }
  }'