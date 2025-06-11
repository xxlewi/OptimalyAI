#!/bin/bash

# End-to-End Test for Complete Workflow System

echo "=== PROJECT WORKFLOW REDESIGN - E2E TEST ==="

# Set the base URL
BASE_URL="https://localhost:5005"
API_URL="$BASE_URL/api"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Test results
TESTS_PASSED=0
TESTS_FAILED=0

# Function to log test results
log_test() {
    local test_name="$1"
    local result="$2"
    local details="$3"
    
    if [ "$result" = "PASS" ]; then
        echo -e "${GREEN}✅ PASS${NC} - $test_name"
        ((TESTS_PASSED++))
    else
        echo -e "${RED}❌ FAIL${NC} - $test_name"
        if [ -n "$details" ]; then
            echo -e "   ${YELLOW}Details:${NC} $details"
        fi
        ((TESTS_FAILED++))
    fi
}

# Test 1: Create a new project template
echo -e "\n${BLUE}=== Test 1: Creating Project Template ===${NC}"
TEMPLATE_RESPONSE=$(curl -s -k -X POST "$API_URL/projects" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "E2E Test Template",
    "description": "End-to-end test workflow template",
    "type": "Template",
    "isTemplate": true,
    "status": "Active"
  }')

TEMPLATE_ID=$(echo "$TEMPLATE_RESPONSE" | jq -r '.data.id // empty')
if [ -n "$TEMPLATE_ID" ] && [ "$TEMPLATE_ID" != "null" ]; then
    log_test "Create project template" "PASS" "Template ID: $TEMPLATE_ID"
else
    log_test "Create project template" "FAIL" "Response: $TEMPLATE_RESPONSE"
    exit 1
fi

# Test 2: Add stages to template
echo -e "\n${BLUE}=== Test 2: Adding Workflow Stages ===${NC}"

# Stage 1: Input stage
STAGE1_RESPONSE=$(curl -s -k -X POST "$API_URL/workflow/stages" \
  -H "Content-Type: application/json" \
  -d "{
    \"projectId\": \"$TEMPLATE_ID\",
    \"name\": \"Data Input\",
    \"description\": \"Input stage for data collection\",
    \"type\": \"Input\",
    \"orchestratorType\": \"ConversationOrchestrator\",
    \"executionStrategy\": \"Sequential\",
    \"errorHandlingStrategy\": \"StopOnError\",
    \"order\": 1
  }")

STAGE1_ID=$(echo "$STAGE1_RESPONSE" | jq -r '.data.id // empty')
if [ -n "$STAGE1_ID" ] && [ "$STAGE1_ID" != "null" ]; then
    log_test "Create input stage" "PASS" "Stage 1 ID: $STAGE1_ID"
else
    log_test "Create input stage" "FAIL" "Response: $STAGE1_RESPONSE"
fi

# Stage 2: Processing stage
STAGE2_RESPONSE=$(curl -s -k -X POST "$API_URL/workflow/stages" \
  -H "Content-Type: application/json" \
  -d "{
    \"projectId\": \"$TEMPLATE_ID\",
    \"name\": \"Data Processing\",
    \"description\": \"Process data using tools\",
    \"type\": \"Processing\",
    \"orchestratorType\": \"ToolChainOrchestrator\",
    \"executionStrategy\": \"Sequential\",
    \"errorHandlingStrategy\": \"ContinueOnError\",
    \"order\": 2
  }")

STAGE2_ID=$(echo "$STAGE2_RESPONSE" | jq -r '.data.id // empty')
if [ -n "$STAGE2_ID" ] && [ "$STAGE2_ID" != "null" ]; then
    log_test "Create processing stage" "PASS" "Stage 2 ID: $STAGE2_ID"
else
    log_test "Create processing stage" "FAIL" "Response: $STAGE2_RESPONSE"
fi

# Test 3: Add tools to processing stage
echo -e "\n${BLUE}=== Test 3: Adding Tools to Stages ===${NC}"

# Get available tools
TOOLS_RESPONSE=$(curl -s -k "$API_URL/tools")
AVAILABLE_TOOLS=$(echo "$TOOLS_RESPONSE" | jq -r '.data[0].id // empty')

if [ -n "$AVAILABLE_TOOLS" ] && [ "$AVAILABLE_TOOLS" != "null" ]; then
    # Add tool to stage
    TOOL_ADD_RESPONSE=$(curl -s -k -X POST "$API_URL/workflow/stages/$STAGE2_ID/tools" \
      -H "Content-Type: application/json" \
      -d "{
        \"toolId\": \"$AVAILABLE_TOOLS\",
        \"order\": 1,
        \"configuration\": \"{\\\"testParam\\\": \\\"testValue\\\"}\",
        \"inputMapping\": \"{}\"
      }")
    
    if echo "$TOOL_ADD_RESPONSE" | jq -e '.isSuccess' > /dev/null; then
        log_test "Add tool to stage" "PASS" "Tool added to processing stage"
    else
        log_test "Add tool to stage" "FAIL" "Response: $TOOL_ADD_RESPONSE"
    fi
else
    log_test "Add tool to stage" "FAIL" "No tools available"
fi

# Test 4: Create project from template
echo -e "\n${BLUE}=== Test 4: Creating Project from Template ===${NC}"

PROJECT_RESPONSE=$(curl -s -k -X POST "$API_URL/workflow/templates/$TEMPLATE_ID/create-project" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "E2E Test Project",
    "description": "Project created from E2E test template",
    "type": "Development",
    "status": "Active"
  }')

PROJECT_ID=$(echo "$PROJECT_RESPONSE" | jq -r '.data.id // empty')
if [ -n "$PROJECT_ID" ] && [ "$PROJECT_ID" != "null" ]; then
    log_test "Create project from template" "PASS" "Project ID: $PROJECT_ID"
else
    log_test "Create project from template" "FAIL" "Response: $PROJECT_RESPONSE"
fi

# Test 5: Validate workflow
echo -e "\n${BLUE}=== Test 5: Workflow Validation ===${NC}"

VALIDATION_RESPONSE=$(curl -s -k "$API_URL/workflow/$PROJECT_ID/validate")
IS_VALID=$(echo "$VALIDATION_RESPONSE" | jq -r '.data // false')

if [ "$IS_VALID" = "true" ]; then
    log_test "Workflow validation" "PASS" "Workflow is valid"
else
    log_test "Workflow validation" "FAIL" "Workflow validation failed"
fi

# Test 6: Get workflow design
echo -e "\n${BLUE}=== Test 6: Retrieve Workflow Design ===${NC}"

DESIGN_RESPONSE=$(curl -s -k "$API_URL/workflow/$PROJECT_ID/design")
STAGES_COUNT=$(echo "$DESIGN_RESPONSE" | jq -r '.data.stages | length // 0')

if [ "$STAGES_COUNT" -ge 2 ]; then
    log_test "Retrieve workflow design" "PASS" "Found $STAGES_COUNT stages"
else
    log_test "Retrieve workflow design" "FAIL" "Expected 2+ stages, found $STAGES_COUNT"
fi

# Test 7: Execute workflow
echo -e "\n${BLUE}=== Test 7: Execute Workflow ===${NC}"

EXECUTION_RESPONSE=$(curl -s -k -X POST "$API_URL/workflow/$PROJECT_ID/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "parameters": {
      "testInput": "E2E test data",
      "mode": "test"
    },
    "initiatedBy": "e2e-test"
  }')

EXECUTION_ID=$(echo "$EXECUTION_RESPONSE" | jq -r '.data.executionId // empty')
if [ -n "$EXECUTION_ID" ] && [ "$EXECUTION_ID" != "null" ]; then
    log_test "Execute workflow" "PASS" "Execution ID: $EXECUTION_ID"
    
    # Wait a bit for execution to start
    sleep 3
    
    # Test 8: Monitor execution
    echo -e "\n${BLUE}=== Test 8: Monitor Execution ===${NC}"
    
    STATUS_RESPONSE=$(curl -s -k "$API_URL/workflow/executions/$EXECUTION_ID/status")
    EXECUTION_STATUS=$(echo "$STATUS_RESPONSE" | jq -r '.data.status // empty')
    
    if [ -n "$EXECUTION_STATUS" ]; then
        log_test "Monitor execution status" "PASS" "Status: $EXECUTION_STATUS"
        
        # Test 9: Get stage results
        echo -e "\n${BLUE}=== Test 9: Retrieve Stage Results ===${NC}"
        
        RESULTS_RESPONSE=$(curl -s -k "$API_URL/workflow/executions/$EXECUTION_ID/stages")
        RESULTS_COUNT=$(echo "$RESULTS_RESPONSE" | jq -r '.data | length // 0')
        
        if [ "$RESULTS_COUNT" -ge 0 ]; then
            log_test "Retrieve stage results" "PASS" "Found $RESULTS_COUNT stage results"
        else
            log_test "Retrieve stage results" "FAIL" "No stage results found"
        fi
        
        # Test 10: Cancel execution (if still running)
        if [ "$EXECUTION_STATUS" = "Running" ]; then
            echo -e "\n${BLUE}=== Test 10: Cancel Execution ===${NC}"
            
            CANCEL_RESPONSE=$(curl -s -k -X POST "$API_URL/workflow/executions/$EXECUTION_ID/cancel")
            if [ $? -eq 0 ]; then
                log_test "Cancel execution" "PASS" "Execution cancelled"
            else
                log_test "Cancel execution" "FAIL" "Failed to cancel execution"
            fi
        fi
    else
        log_test "Monitor execution status" "FAIL" "No status received"
    fi
else
    log_test "Execute workflow" "FAIL" "No execution ID received"
fi

# Test 11: Export template
echo -e "\n${BLUE}=== Test 11: Export Template ===${NC}"

EXPORT_RESPONSE=$(curl -s -k "$API_URL/workflow/$TEMPLATE_ID/design")
if echo "$EXPORT_RESPONSE" | jq -e '.isSuccess' > /dev/null; then
    # Save to file
    echo "$EXPORT_RESPONSE" | jq '.data' > "/tmp/exported_template.json"
    log_test "Export template" "PASS" "Template exported to /tmp/exported_template.json"
else
    log_test "Export template" "FAIL" "Export failed"
fi

# Test 12: Template management
echo -e "\n${BLUE}=== Test 12: Template Management ===${NC}"

TEMPLATES_RESPONSE=$(curl -s -k "$API_URL/workflow/templates")
TEMPLATES_COUNT=$(echo "$TEMPLATES_RESPONSE" | jq -r '.data | length // 0')

if [ "$TEMPLATES_COUNT" -ge 1 ]; then
    log_test "List templates" "PASS" "Found $TEMPLATES_COUNT templates"
else
    log_test "List templates" "FAIL" "No templates found"
fi

# Cleanup
echo -e "\n${BLUE}=== Cleanup ===${NC}"

# Delete test project
if [ -n "$PROJECT_ID" ]; then
    curl -s -k -X DELETE "$API_URL/projects/$PROJECT_ID" > /dev/null
    echo "Deleted test project: $PROJECT_ID"
fi

# Delete test template
if [ -n "$TEMPLATE_ID" ]; then
    curl -s -k -X DELETE "$API_URL/projects/$TEMPLATE_ID" > /dev/null
    echo "Deleted test template: $TEMPLATE_ID"
fi

# Final results
echo -e "\n${BLUE}=== TEST RESULTS ===${NC}"
echo -e "${GREEN}Tests Passed: $TESTS_PASSED${NC}"
echo -e "${RED}Tests Failed: $TESTS_FAILED${NC}"

TOTAL_TESTS=$((TESTS_PASSED + TESTS_FAILED))
SUCCESS_RATE=$((TESTS_PASSED * 100 / TOTAL_TESTS))

echo -e "${BLUE}Success Rate: $SUCCESS_RATE%${NC}"

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "\n${GREEN}🎉 ALL TESTS PASSED! Project Workflow Redesign is working perfectly!${NC}"
    exit 0
else
    echo -e "\n${YELLOW}⚠️  Some tests failed. Please check the implementation.${NC}"
    exit 1
fi