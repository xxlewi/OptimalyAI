#!/bin/bash

# Test script for new Workflow Designer UI
# Tests template application, drag & drop, and API endpoints

API_URL="https://localhost:5005/api"
PROJECT_ID="32bd9b96-bd66-4607-b07c-ec2774d30ee2"

echo "=== Testing Workflow Designer API ==="
echo

# 1. Get workflow design
echo "1. Getting workflow design for project..."
curl -k -X GET "$API_URL/workflow/$PROJECT_ID/design" \
     -H "Content-Type: application/json" | jq '.'

echo -e "\n2. Getting available components..."
curl -k -X GET "$API_URL/workflow/components" \
     -H "Content-Type: application/json" | jq '.'

# 3. Create a new stage from template
echo -e "\n3. Creating new stage..."
STAGE_DATA='{
  "projectId": "'$PROJECT_ID'",
  "name": "Analýza vstupních fotek",
  "description": "Extrakce vizuálních vlastností produktů z fotografií",
  "type": "Analysis",
  "orchestratorType": "ConversationOrchestrator",
  "reactAgentType": "ConversationReActAgent",
  "executionStrategy": "Sequential",
  "orchestratorConfiguration": "{}",
  "reactAgentConfiguration": "{}",
  "tools": []
}'

STAGE_RESPONSE=$(curl -k -X POST "$API_URL/workflow/stages" \
     -H "Content-Type: application/json" \
     -d "$STAGE_DATA")

echo "$STAGE_RESPONSE" | jq '.'

# Extract stage ID from response
STAGE_ID=$(echo "$STAGE_RESPONSE" | jq -r '.data.id')

if [ "$STAGE_ID" != "null" ]; then
    echo -e "\n4. Adding tool to stage..."
    TOOL_DATA='{
      "toolName": "image_analyzer",
      "configuration": {},
      "order": 1
    }'
    
    curl -k -X POST "$API_URL/workflow/stages/$STAGE_ID/tools" \
         -H "Content-Type: application/json" \
         -d "$TOOL_DATA" | jq '.'
fi

# 5. Save workflow design
echo -e "\n5. Saving workflow design..."
WORKFLOW_DATA='{
  "projectId": "'$PROJECT_ID'",
  "triggerType": "Manual",
  "schedule": null,
  "stages": []
}'

curl -k -X POST "$API_URL/workflow/design" \
     -H "Content-Type: application/json" \
     -d "$WORKFLOW_DATA" | jq '.'

# 6. Validate workflow
echo -e "\n6. Validating workflow..."
curl -k -X GET "$API_URL/workflow/$PROJECT_ID/validate" \
     -H "Content-Type: application/json" | jq '.'

# 7. Test workflow execution
echo -e "\n7. Testing workflow execution..."
EXECUTE_DATA='{
  "parameters": {
    "testParam": "test value"
  },
  "initiatedBy": "test-script"
}'

curl -k -X POST "$API_URL/workflow/$PROJECT_ID/execute" \
     -H "Content-Type: application/json" \
     -d "$EXECUTE_DATA" | jq '.'

echo -e "\n=== UI Test Instructions ==="
echo "1. Open https://localhost:5005/Projects/WorkflowDesigner/$PROJECT_ID"
echo "2. Test template selection - click on 'Vyhledávání produktů' template"
echo "3. Test drag & drop - drag tools from sidebar to stages"
echo "4. Test stage reordering - use drag handles to reorder stages"
echo "5. Test save workflow - click 'Uložit Workflow' button"
echo "6. Test validation - click 'Validovat' button"
echo -e "\n=== End of test ==="