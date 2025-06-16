#!/bin/bash

# Browser Inspector Tool
# Usage: ./browser-inspect.sh [url] [output_file]

URL="${1:-https://localhost:5005}"
OUTPUT="${2:-browser-state.json}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

echo "🔍 Browser Inspector Tool"
echo "========================"
echo "URL: $URL"
echo ""

# Create temporary HTML file that will inject our inspector
TEMP_HTML="/tmp/browser_inspector_${TIMESTAMP}.html"

cat > "$TEMP_HTML" << EOF
<!DOCTYPE html>
<html>
<head>
    <title>Browser Inspector Loader</title>
    <script>
        // Inject the inspector script into the target page
        function injectInspector() {
            const script = document.createElement('script');
            script.src = 'file:///Users/lewi/Documents/Vyvoj/OptimalyAI/Tools/browser-inspector.js';
            document.head.appendChild(script);
            
            setTimeout(() => {
                if (window.BrowserInspector) {
                    console.log('✅ Browser Inspector loaded successfully');
                } else {
                    console.error('❌ Failed to load Browser Inspector');
                }
            }, 1000);
        }
        
        // Auto-redirect to target URL with injection
        window.onload = function() {
            window.location.href = '${URL}';
        };
    </script>
</head>
<body>
    <h1>Loading Browser Inspector...</h1>
    <p>Redirecting to: ${URL}</p>
</body>
</html>
EOF

# Option 1: Using Chrome with debugging enabled
echo "Option 1: Chrome with Remote Debugging"
echo "1. Run this command to start Chrome with debugging:"
echo "   /Applications/Google\\ Chrome.app/Contents/MacOS/Google\\ Chrome --remote-debugging-port=9222 --user-data-dir=/tmp/chrome-debug"
echo ""

# Option 2: Create bookmarklet
BOOKMARKLET=$(cat /Users/lewi/Documents/Vyvoj/OptimalyAI/Tools/browser-inspector.js | sed 's/"/\\"/g' | tr -d '\n')
echo "Option 2: Bookmarklet (drag this to bookmarks bar):"
echo "javascript:(function(){${BOOKMARKLET:0:100}...})();"
echo ""

# Option 3: Copy to clipboard for console injection
echo "Option 3: Paste into browser console"
echo "The inspector script has been copied to clipboard. Just paste it into the console!"
cat /Users/lewi/Documents/Vyvoj/OptimalyAI/Tools/browser-inspector.js | pbcopy

# Option 4: Using AppleScript to inject (Safari only)
echo ""
echo "Option 4: Automated injection (Safari)"
read -p "Press Enter to open $URL and inject inspector (Safari only)..."

osascript << APPLESCRIPT
tell application "Safari"
    activate
    open location "${URL}"
    delay 2
    
    do JavaScript "
        ${BOOKMARKLET}
    " in current tab of window 1
end tell
APPLESCRIPT

echo ""
echo "📋 To export browser state:"
echo "1. Click the 🔍 button in bottom-right corner"
echo "2. Or run in console: BrowserInspector.getPageState()"
echo "3. Or run in console: BrowserInspector.exportState()"