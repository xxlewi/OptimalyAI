#!/bin/bash

# Test Workflow Execution API

echo "=== Test Workflow Execution ==="

# Set the base URL
BASE_URL="https://localhost:5005/api"

# First, get a project ID
echo "1. Getting projects..."
PROJECTS=$(curl -s -k "$BASE_URL/projects" | jq -r '.data[0].id')
echo "Using project ID: $PROJECTS"

# Execute workflow
echo -e "\n2. Executing workflow for project..."
RESPONSE=$(curl -s -k -X POST "$BASE_URL/workflow/$PROJECTS/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "parameters": {
      "testParam": "testValue",
      "searchQuery": "test search"
    },
    "initiatedBy": "test-script"
  }')

echo "Response: $RESPONSE"
EXECUTION_ID=$(echo "$RESPONSE" | jq -r '.data.executionId')
echo "Execution ID: $EXECUTION_ID"

# Wait a bit
sleep 2

# Get execution status
echo -e "\n3. Getting execution status..."
STATUS_RESPONSE=$(curl -s -k "$BASE_URL/workflow/executions/$EXECUTION_ID/status")
echo "Status: $STATUS_RESPONSE"

# Get stage results
echo -e "\n4. Getting stage results..."
STAGES_RESPONSE=$(curl -s -k "$BASE_URL/workflow/executions/$EXECUTION_ID/stages")
echo "Stages: $STAGES_RESPONSE"

echo -e "\n=== Test completed ==="
echo "You can monitor the execution at: https://localhost:5005/ProjectWorkflows/Monitor?executionId=$EXECUTION_ID"