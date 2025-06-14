#!/bin/bash

# Browser Interaction Tools Available on macOS
# This script demonstrates various ways to interact with browsers

echo "=== Browser Interaction Tools for macOS ==="
echo ""

# 1. Open URL in default browser
echo "1. Opening URL in browser:"
echo "   Command: open https://localhost:5005"
echo ""

# 2. Take screenshots
echo "2. Screenshot capabilities:"
echo "   - Full screen: screencapture screenshot.png"
echo "   - Interactive mode: screencapture -i screenshot.png"
echo "   - Window capture: screencapture -w screenshot.png"
echo "   - With delay: screencapture -T 5 screenshot.png"
echo ""

# 3. AppleScript for browser control
echo "3. AppleScript browser automation:"
cat << 'EOF'
   Example AppleScript to interact with Safari:
   
   osascript -e 'tell application "Safari"
       activate
       open location "https://localhost:5005"
       delay 2
       do JavaScript "document.title" in current tab of window 1
   end tell'
EOF
echo ""

# 4. Using JavaScript in browser via AppleScript
echo "4. Execute JavaScript in browser:"
cat << 'EOF'
   # Get page title
   osascript -e 'tell application "Safari" to do JavaScript "document.title" in current tab of window 1'
   
   # Click a button
   osascript -e 'tell application "Safari" to do JavaScript "document.querySelector(\"#myButton\").click()" in current tab of window 1'
   
   # Get element text
   osascript -e 'tell application "Safari" to do JavaScript "document.querySelector(\".class-name\").textContent" in current tab of window 1'
EOF
echo ""

# 5. Chrome automation via AppleScript
echo "5. Chrome automation:"
cat << 'EOF'
   osascript -e 'tell application "Google Chrome"
       activate
       open location "https://localhost:5005"
       delay 2
       execute active tab of window 1 javascript "document.title"
   end tell'
EOF
echo ""

# 6. Capture specific window
echo "6. Window-specific capture:"
echo "   # List all windows"
echo "   osascript -e 'tell application \"System Events\" to get name of every window of every process'"
echo ""

# 7. Accessibility Inspector
echo "7. Accessibility tools:"
echo "   - UI Browser (if installed): /Applications/UI Browser.app"
echo "   - Accessibility Inspector: /Applications/Xcode.app/Contents/Developer/Applications/Accessibility Inspector.app"
echo ""

# 8. Using curl to interact with the application
echo "8. API testing with curl:"
echo "   curl -k https://localhost:5005/api/tools"
echo ""

echo "=== Practical Examples ==="
echo ""

# Example 1: Open browser and take screenshot
echo "Example 1: Open page and capture screenshot"
cat << 'EOF'
#!/bin/bash
# Open the page
open https://localhost:5005/Projects/Create
# Wait for page to load
sleep 3
# Take screenshot
screencapture -w project-create-screenshot.png
EOF
echo ""

# Example 2: Fill form using AppleScript
echo "Example 2: Fill form with AppleScript (Safari)"
cat << 'EOF'
#!/bin/bash
osascript << 'END'
tell application "Safari"
    activate
    open location "https://localhost:5005/Projects/Create"
    delay 3
    
    # Fill form fields
    do JavaScript "document.getElementById('Name').value = 'Test Project'" in current tab of window 1
    do JavaScript "document.getElementById('Description').value = 'Test Description'" in current tab of window 1
    
    # Take screenshot after filling
    delay 1
end tell
END

screencapture -w filled-form.png
EOF
echo ""

# Example 3: Get page state
echo "Example 3: Get current page state"
cat << 'EOF'
#!/bin/bash
# Get page title, URL, and specific element values
osascript << 'END'
tell application "Safari"
    set pageTitle to do JavaScript "document.title" in current tab of window 1
    set pageURL to URL of current tab of window 1
    set formValues to do JavaScript "
        JSON.stringify({
            name: document.getElementById('Name')?.value || '',
            description: document.getElementById('Description')?.value || '',
            customerName: document.getElementById('CustomerName')?.value || ''
        })
    " in current tab of window 1
    
    return "Title: " & pageTitle & "
URL: " & pageURL & "
Form Values: " & formValues
end tell
END
EOF
echo ""

echo "=== Usage Tips ==="
echo "1. Always add delays after navigation to ensure page loads"
echo "2. Use -k flag with curl for self-signed certificates"
echo "3. Screenshots can be taken in PNG, JPG, PDF formats"
echo "4. AppleScript requires accessibility permissions"
echo "5. Different browsers have slightly different AppleScript syntax"