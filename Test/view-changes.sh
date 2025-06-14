#!/bin/bash

# Simple script to view changes after code modifications
# This is what Claude can use to verify changes

# Function to take a screenshot and save it with timestamp
take_screenshot() {
    local url="$1"
    local description="$2"
    local timestamp=$(date +"%Y%m%d_%H%M%S")
    local filename="screenshot_${description}_${timestamp}.png"
    
    echo "Opening $url..."
    open "$url"
    
    echo "Waiting for page to load..."
    sleep 3
    
    echo "Taking screenshot..."
    screencapture -w "$filename"
    
    echo "Screenshot saved as: $filename"
    echo "Full path: $(pwd)/$filename"
    
    # Also save a copy to a fixed location for easy access
    cp "$filename" "/tmp/latest_screenshot.png"
    echo "Latest screenshot also saved to: /tmp/latest_screenshot.png"
}

# Function to capture specific element
capture_element() {
    local url="$1"
    local selector="$2"
    local timestamp=$(date +"%Y%m%d_%H%M%S")
    
    osascript << EOF
tell application "Safari"
    activate
    open location "$url"
    delay 3
    
    # Get element details
    set elementInfo to do JavaScript "
        const el = document.querySelector('$selector');
        if (el) {
            const rect = el.getBoundingClientRect();
            JSON.stringify({
                found: true,
                text: el.textContent,
                html: el.outerHTML.substring(0, 500),
                position: {
                    top: rect.top,
                    left: rect.left,
                    width: rect.width,
                    height: rect.height
                },
                styles: {
                    display: getComputedStyle(el).display,
                    visibility: getComputedStyle(el).visibility,
                    backgroundColor: getComputedStyle(el).backgroundColor
                }
            }, null, 2);
        } else {
            JSON.stringify({found: false, selector: '$selector'});
        }
    " in current tab of window 1
    
    return elementInfo
end tell
EOF
}

# Main functionality
echo "=== View Changes Tool ==="
echo ""
echo "Common usage examples:"
echo ""
echo "1. View Projects Create page:"
echo "   ./view-changes.sh https://localhost:5005/Projects/Create"
echo ""
echo "2. View specific page with description:"
echo "   ./view-changes.sh https://localhost:5005/Projects create_form"
echo ""
echo "3. Check specific element:"
echo "   ./view-changes.sh element https://localhost:5005/Projects/Create '#CustomerName'"
echo ""

# Handle arguments
if [ "$1" == "element" ]; then
    # Capture specific element
    if [ -z "$2" ] || [ -z "$3" ]; then
        echo "Usage: ./view-changes.sh element <url> <css-selector>"
        exit 1
    fi
    
    echo "Capturing element '$3' at $2..."
    result=$(capture_element "$2" "$3")
    echo "Element details:"
    echo "$result"
    
    # Also take a screenshot
    take_screenshot "$2" "element_check"
    
elif [ -n "$1" ]; then
    # Take screenshot of URL
    URL="$1"
    DESC="${2:-page}"
    
    take_screenshot "$URL" "$DESC"
    
    # Also try to get some basic page info
    echo ""
    echo "Getting page info..."
    osascript << EOF
tell application "Safari"
    set pageInfo to do JavaScript "
        JSON.stringify({
            title: document.title,
            forms: document.forms.length,
            inputs: document.querySelectorAll('input').length,
            errors: document.querySelectorAll('.alert-danger, .error').length,
            url: window.location.href
        }, null, 2)
    " in current tab of window 1
    
    return "Page info: " & pageInfo
end tell
EOF
    
else
    # Interactive mode
    echo "What would you like to capture?"
    echo "1) Projects Create page"
    echo "2) Projects Index page"
    echo "3) Customer page"
    echo "4) Custom URL"
    echo "5) Last screenshot taken"
    
    read -p "Choice (1-5): " choice
    
    case $choice in
        1) take_screenshot "https://localhost:5005/Projects/Create" "projects_create" ;;
        2) take_screenshot "https://localhost:5005/Projects" "projects_index" ;;
        3) take_screenshot "https://localhost:5005/Customers" "customers" ;;
        4) 
            read -p "Enter URL: " custom_url
            take_screenshot "$custom_url" "custom"
            ;;
        5) 
            if [ -f "/tmp/latest_screenshot.png" ]; then
                echo "Opening latest screenshot..."
                open "/tmp/latest_screenshot.png"
            else
                echo "No screenshot found"
            fi
            ;;
        *) echo "Invalid choice" ;;
    esac
fi