#!/bin/bash

# Integrated Browser Testing Tool
# This tool combines screenshot, state capture, and analysis

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
URL="${1:-https://localhost:5005}"
TEST_NAME="${2:-test}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
OUTPUT_DIR="./Test/browser-tests/${TEST_NAME}_${TIMESTAMP}"

# Create output directory
mkdir -p "$OUTPUT_DIR"

echo -e "${BLUE}🔍 Browser Testing Tool${NC}"
echo "========================"
echo "URL: $URL"
echo "Test name: $TEST_NAME"
echo "Output: $OUTPUT_DIR"
echo ""

# Function to inject inspector via clipboard
inject_inspector() {
    echo -e "${YELLOW}📋 Copying Browser Inspector to clipboard...${NC}"
    cat /Users/lewi/Documents/Vyvoj/OptimalyAI/Tools/browser-inspector.js | pbcopy
    echo "1. Open Developer Tools (F12)"
    echo "2. Go to Console tab"
    echo "3. Paste (Cmd+V) and press Enter"
    echo "4. You should see: '🔍 Browser Inspector initialized'"
    echo ""
    read -p "Press Enter after you've injected the script..."
}

# Function to capture screenshot
capture_screenshot() {
    local filename="$OUTPUT_DIR/screenshot_${1}.png"
    echo -e "${YELLOW}📸 Capturing screenshot...${NC}"
    screencapture -w "$filename"
    echo -e "${GREEN}✓ Screenshot saved to: $filename${NC}"
}

# Function to capture browser state
capture_state() {
    echo -e "${YELLOW}📊 Capturing browser state...${NC}"
    echo "In the browser console, run:"
    echo -e "${BLUE}copy(JSON.stringify(BrowserInspector.getPageState(), null, 2))${NC}"
    echo ""
    echo "This will copy the state to clipboard."
    read -p "Press Enter after copying the state..."
    
    # Save from clipboard
    pbpaste > "$OUTPUT_DIR/browser-state.json"
    echo -e "${GREEN}✓ State saved to: $OUTPUT_DIR/browser-state.json${NC}"
}

# Function to analyze state
analyze_state() {
    echo -e "${YELLOW}🔍 Analyzing browser state...${NC}"
    python3 /Users/lewi/Documents/Vyvoj/OptimalyAI/Tools/analyze-browser-state.py "$OUTPUT_DIR/browser-state.json" > "$OUTPUT_DIR/analysis.txt"
    echo -e "${GREEN}✓ Analysis saved to: $OUTPUT_DIR/analysis.txt${NC}"
    echo ""
    echo "=== ANALYSIS PREVIEW ==="
    head -n 30 "$OUTPUT_DIR/analysis.txt"
    echo "..."
}

# Function to create test report
create_report() {
    echo -e "${YELLOW}📝 Creating test report...${NC}"
    
    cat > "$OUTPUT_DIR/report.md" << EOF
# Browser Test Report

**Date:** $(date)  
**URL:** $URL  
**Test Name:** $TEST_NAME  

## Screenshots

![Initial State](screenshot_initial.png)
![After Action](screenshot_after.png)

## Browser State Analysis

\`\`\`
$(cat "$OUTPUT_DIR/analysis.txt")
\`\`\`

## Console Logs

\`\`\`json
$(jq '.consoleLogs' "$OUTPUT_DIR/browser-state.json" 2>/dev/null || echo "No console logs")
\`\`\`

## Network Requests

\`\`\`json
$(jq '.networkRequests' "$OUTPUT_DIR/browser-state.json" 2>/dev/null || echo "No network requests")
\`\`\`

## Errors

\`\`\`json
$(jq '.errors' "$OUTPUT_DIR/browser-state.json" 2>/dev/null || echo "No errors")
\`\`\`
EOF

    echo -e "${GREEN}✓ Report saved to: $OUTPUT_DIR/report.md${NC}"
}

# Main flow
echo -e "${BLUE}Starting browser test...${NC}"
echo ""

# Step 1: Open URL
echo -e "${YELLOW}1. Opening URL...${NC}"
open "$URL"
sleep 2

# Step 2: Initial screenshot
capture_screenshot "initial"

# Step 3: Inject inspector
inject_inspector

# Step 4: Let user perform actions
echo -e "${YELLOW}4. Perform your test actions in the browser${NC}"
echo "   (fill forms, click buttons, etc.)"
read -p "Press Enter when ready to capture final state..."

# Step 5: Final screenshot
capture_screenshot "after"

# Step 6: Capture state
capture_state

# Step 7: Analyze
if [ -f "$OUTPUT_DIR/browser-state.json" ]; then
    analyze_state
fi

# Step 8: Create report
create_report

# Summary
echo ""
echo -e "${GREEN}✅ Test completed!${NC}"
echo "Results saved in: $OUTPUT_DIR"
echo ""
echo "Files created:"
ls -la "$OUTPUT_DIR"
echo ""
echo -e "${BLUE}To view the report:${NC}"
echo "cat $OUTPUT_DIR/report.md"
echo ""
echo -e "${BLUE}To view screenshots:${NC}"
echo "open $OUTPUT_DIR"