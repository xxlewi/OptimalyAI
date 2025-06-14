#!/bin/bash

# Practical script to capture browser state after changes
# Usage: ./capture-browser-state.sh [url] [output_name]

URL="${1:-https://localhost:5005}"
OUTPUT_NAME="${2:-browser-capture}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")

echo "=== Browser State Capture Tool ==="
echo "URL: $URL"
echo "Output: ${OUTPUT_NAME}_${TIMESTAMP}"
echo ""

# Function to capture Safari state
capture_safari() {
    echo "Capturing Safari state..."
    
    osascript << EOF
tell application "Safari"
    activate
    
    # Open URL if not already there
    if not (exists (tabs of window 1 whose URL contains "$URL")) then
        open location "$URL"
        delay 3
    end if
    
    # Get page information
    set pageTitle to do JavaScript "document.title" in current tab of window 1
    set pageURL to URL of current tab of window 1
    
    # Get page HTML (truncated for display)
    set pageHTML to do JavaScript "document.documentElement.outerHTML.substring(0, 1000) + '...'" in current tab of window 1
    
    # Get any error messages
    set errorMessages to do JavaScript "
        Array.from(document.querySelectorAll('.alert-danger, .error, .validation-summary-errors'))
            .map(el => el.textContent.trim())
            .join('\\n')
    " in current tab of window 1
    
    # Get form values if any forms exist
    set formData to do JavaScript "
        const forms = document.querySelectorAll('form');
        if (forms.length > 0) {
            const data = {};
            forms[0].querySelectorAll('input, select, textarea').forEach(el => {
                if (el.name && el.type !== 'hidden') {
                    data[el.name] = el.value;
                }
            });
            JSON.stringify(data, null, 2);
        } else {
            'No forms found';
        }
    " in current tab of window 1
    
    # Save to file
    set outputFile to "${OUTPUT_NAME}_${TIMESTAMP}_state.txt"
    set fileContent to "=== Browser State Capture ===" & "
Date: $(date)" & "
URL: " & pageURL & "
Title: " & pageTitle & "

=== Error Messages ===" & "
" & errorMessages & "

=== Form Data ===" & "
" & formData & "

=== Page HTML (truncated) ===" & "
" & pageHTML
    
    do shell script "echo " & quoted form of fileContent & " > " & quoted form of outputFile
    
    return "State saved to: " & outputFile
end tell
EOF
    
    # Take screenshot
    echo "Taking screenshot..."
    screencapture -w "${OUTPUT_NAME}_${TIMESTAMP}_screenshot.png"
    
    echo "Capture complete!"
}

# Function to capture Chrome state
capture_chrome() {
    echo "Capturing Chrome state..."
    
    osascript << EOF
tell application "Google Chrome"
    activate
    
    # Open URL if not already there
    if not (exists (tabs of window 1 whose URL contains "$URL")) then
        open location "$URL"
        delay 3
    end if
    
    # Get page information
    set pageTitle to execute active tab of window 1 javascript "document.title"
    set pageURL to URL of active tab of window 1
    
    # Similar JavaScript execution as Safari
    set formData to execute active tab of window 1 javascript "
        const forms = document.querySelectorAll('form');
        if (forms.length > 0) {
            const data = {};
            forms[0].querySelectorAll('input, select, textarea').forEach(el => {
                if (el.name && el.type !== 'hidden') {
                    data[el.name] = el.value;
                }
            });
            JSON.stringify(data, null, 2);
        } else {
            'No forms found';
        }
    "
    
    return "URL: " & pageURL & ", Form Data: " & formData
end tell
EOF
    
    # Take screenshot
    screencapture -w "${OUTPUT_NAME}_${TIMESTAMP}_screenshot.png"
}

# Function for quick screenshot only
quick_screenshot() {
    echo "Taking quick screenshot..."
    open "$URL"
    sleep 3
    screencapture -w "${OUTPUT_NAME}_${TIMESTAMP}_screenshot.png"
    echo "Screenshot saved to: ${OUTPUT_NAME}_${TIMESTAMP}_screenshot.png"
}

# Main menu
echo "Select browser:"
echo "1) Safari (with detailed state capture)"
echo "2) Chrome (with state capture)"
echo "3) Quick screenshot only"
echo "4) Interactive screenshot (you select the area)"

read -p "Choice (1-4): " choice

case $choice in
    1) capture_safari ;;
    2) capture_chrome ;;
    3) quick_screenshot ;;
    4) 
        open "$URL"
        sleep 2
        screencapture -i "${OUTPUT_NAME}_${TIMESTAMP}_screenshot.png"
        ;;
    *) echo "Invalid choice" ;;
esac

echo ""
echo "Files created:"
ls -la | grep "${OUTPUT_NAME}_${TIMESTAMP}"