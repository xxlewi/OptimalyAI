#!/bin/bash

# Test for Customer Project Creation via Workflow Designer

echo "=== CUSTOMER PROJECT WORKFLOW TEST ==="

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

# Test 1: Create a customer first
echo -e "\n${BLUE}=== Test 1: Creating Test Customer ===${NC}"
CUSTOMER_RESPONSE=$(curl -s -k -X POST "$API_URL/customers" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Zákazník",
    "companyName": "Test Company s.r.o.",
    "type": 1,
    "ico": "12345678",
    "dic": "CZ12345678"
  }')

CUSTOMER_ID=$(echo "$CUSTOMER_RESPONSE" | jq -r '.data.id // .id // empty')
if [ -n "$CUSTOMER_ID" ] && [ "$CUSTOMER_ID" != "null" ]; then
    log_test "Create test customer" "PASS" "Customer ID: $CUSTOMER_ID"
else
    log_test "Create test customer" "FAIL" "Response: $CUSTOMER_RESPONSE"
    exit 1
fi

# Test 2: Create project via WorkflowDesigner for customer
echo -e "\n${BLUE}=== Test 2: Create Project via Workflow Designer ===${NC}"

# Test access to WorkflowDesignerMvc with customerId
WORKFLOW_RESPONSE=$(curl -s -k "$BASE_URL/WorkflowDesignerMvc?customerId=$CUSTOMER_ID" \
  -w "%{http_code}" -o /tmp/workflow_response.html)

HTTP_CODE="${WORKFLOW_RESPONSE: -3}"
if [ "$HTTP_CODE" = "200" ]; then
    log_test "Access WorkflowDesigner with customerId" "PASS" "HTTP 200 OK"
else
    log_test "Access WorkflowDesigner with customerId" "FAIL" "HTTP Code: $HTTP_CODE"
fi

# Test 3: Check if project was created for the customer
echo -e "\n${BLUE}=== Test 3: Verify Project Creation ===${NC}"

# Get customer's projects
CUSTOMER_PROJECTS_RESPONSE=$(curl -s -k "$API_URL/customers/$CUSTOMER_ID")
PROJECTS_COUNT=$(echo "$CUSTOMER_PROJECTS_RESPONSE" | jq -r '.data.projects | length // 0' 2>/dev/null || echo "0")

if [ "$PROJECTS_COUNT" -gt 0 ]; then
    log_test "Project created for customer" "PASS" "Found $PROJECTS_COUNT project(s)"
    
    # Get the project details
    PROJECT_ID=$(echo "$CUSTOMER_PROJECTS_RESPONSE" | jq -r '.data.projects[0].id // empty' 2>/dev/null)
    if [ -n "$PROJECT_ID" ] && [ "$PROJECT_ID" != "null" ]; then
        echo "   Project ID: $PROJECT_ID"
    fi
else
    # Try alternative approach - get all projects and check for our customer
    ALL_PROJECTS_RESPONSE=$(curl -s -k "$API_URL/projects")
    CUSTOMER_PROJECT=$(echo "$ALL_PROJECTS_RESPONSE" | jq -r --arg cid "$CUSTOMER_ID" '.data[] | select(.customerId == $cid) | .id' 2>/dev/null | head -1)
    
    if [ -n "$CUSTOMER_PROJECT" ] && [ "$CUSTOMER_PROJECT" != "null" ]; then
        log_test "Project created for customer" "PASS" "Found project: $CUSTOMER_PROJECT"
    else
        log_test "Project created for customer" "FAIL" "No projects found for customer"
    fi
fi

# Test 4: Test project workflow functionality 
echo -e "\n${BLUE}=== Test 4: Test Project Workflow Functionality ===${NC}"

if [ -n "$PROJECT_ID" ] && [ "$PROJECT_ID" != "null" ]; then
    # Test workflow design endpoint
    DESIGN_RESPONSE=$(curl -s -k "$API_URL/workflow/$PROJECT_ID/design")
    if echo "$DESIGN_RESPONSE" | jq -e '.data // .isSuccess' > /dev/null 2>&1; then
        log_test "Workflow design endpoint" "PASS" "Design accessible"
    else
        log_test "Workflow design endpoint" "FAIL" "Design not accessible"
    fi
else
    log_test "Workflow design endpoint" "FAIL" "No project ID available"
fi

# Cleanup
echo -e "\n${BLUE}=== Cleanup ===${NC}"

# Delete test customer (will cascade delete projects)
if [ -n "$CUSTOMER_ID" ]; then
    curl -s -k -X DELETE "$API_URL/customers/$CUSTOMER_ID" > /dev/null
    echo "Deleted test customer: $CUSTOMER_ID"
fi

# Final results
echo -e "\n${BLUE}=== TEST RESULTS ===${NC}"
echo -e "${GREEN}Tests Passed: $TESTS_PASSED${NC}"
echo -e "${RED}Tests Failed: $TESTS_FAILED${NC}"

TOTAL_TESTS=$((TESTS_PASSED + TESTS_FAILED))
if [ $TOTAL_TESTS -gt 0 ]; then
    SUCCESS_RATE=$((TESTS_PASSED * 100 / TOTAL_TESTS))
    echo -e "${BLUE}Success Rate: $SUCCESS_RATE%${NC}"
fi

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "\n${GREEN}🎉 ALL TESTS PASSED! Customer project creation via Workflow Designer works!${NC}"
    exit 0
else
    echo -e "\n${YELLOW}⚠️  Some tests failed. Please check the implementation.${NC}"
    exit 1
fi