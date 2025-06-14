#!/bin/bash

# Script pro automatické otevření stránky s Developer Tools
# Použití: ./open-with-devtools.sh <URL>

URL="${1:-https://localhost:5005}"
BROWSER="${2:-chrome}" # chrome nebo safari

echo "🔧 Otevírám $URL s Developer Tools..."

if [ "$BROWSER" = "chrome" ]; then
    osascript << EOF
tell application "Google Chrome"
    activate
    
    -- Otevřít novou záložku s URL
    tell window 1
        set newTab to make new tab with properties {URL:"$URL"}
        set active tab index to (count tabs)
    end tell
    
    -- Počkat na načtení
    delay 3
    
    -- Otevřít Developer Tools (Cmd+Option+I)
    tell application "System Events"
        keystroke "i" using {command down, option down}
        delay 1
        
        -- Přepnout na Console tab (Cmd+[)
        keystroke "]" using {command down}
    end tell
    
    delay 2
end tell
EOF

elif [ "$BROWSER" = "safari" ]; then
    osascript << EOF
tell application "Safari"
    activate
    
    -- Otevřít URL
    open location "$URL"
    delay 3
    
    -- Otevřít Developer Tools
    tell application "System Events"
        keystroke "i" using {command down, option down}
    end tell
    
    delay 2
end tell
EOF
fi

echo "✅ Hotovo! Počkejte 3 vteřiny na screenshot..."
sleep 3

# Automaticky udělat screenshot
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SCREENSHOT_PATH="/tmp/devtools_${TIMESTAMP}.png"
screencapture -x "$SCREENSHOT_PATH"

echo "📸 Screenshot uložen: $SCREENSHOT_PATH"
echo ""
echo "Pro zobrazení v Claude Code:"
echo "Read file_path=\"$SCREENSHOT_PATH\""