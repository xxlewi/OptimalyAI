#!/bin/bash

# Komplexní browser automation script
# Umožňuje různé automatizované akce v prohlížeči

set -e

ACTION="${1:-help}"
URL="${2:-https://localhost:5005}"

# Barvy pro výstup
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

show_help() {
    echo -e "${BLUE}🤖 Browser Automation Tool${NC}"
    echo "=========================="
    echo ""
    echo "Použití: $0 <action> [url]"
    echo ""
    echo "Akce:"
    echo "  open-devtools    - Otevře stránku s Developer Tools"
    echo "  console-log      - Otevře stránku a zobrazí Console"
    echo "  network-tab      - Otevře stránku a zobrazí Network tab"
    echo "  elements         - Otevře stránku a zobrazí Elements/Inspector"
    echo "  screenshot       - Pouze screenshot aktuální stránky"
    echo "  fill-form        - Vyplní formulář (experimentální)"
    echo "  click-button     - Klikne na tlačítko podle textu"
    echo ""
    echo "Příklady:"
    echo "  $0 open-devtools https://localhost:5005/Projects/Create"
    echo "  $0 console-log https://localhost:5005/Customers"
    echo "  $0 network-tab"
}

open_with_devtools() {
    echo -e "${YELLOW}🔧 Otevírám $URL s DevTools...${NC}"
    
    osascript << EOF
tell application "Google Chrome"
    activate
    open location "$URL"
    delay 3
    
    tell application "System Events"
        keystroke "i" using {command down, option down}
    end tell
    delay 2
end tell
EOF
}

open_console() {
    echo -e "${YELLOW}📝 Otevírám Console...${NC}"
    
    osascript << EOF
tell application "Google Chrome"
    activate
    open location "$URL"
    delay 3
    
    tell application "System Events"
        -- Otevřít DevTools
        keystroke "i" using {command down, option down}
        delay 1
        
        -- Přejít na Console (Esc pro console drawer)
        key code 53 -- ESC key
        delay 0.5
    end tell
end tell
EOF
}

open_network() {
    echo -e "${YELLOW}🌐 Otevírám Network tab...${NC}"
    
    osascript << EOF
tell application "Google Chrome"
    activate
    open location "$URL"
    delay 2
    
    tell application "System Events"
        -- Otevřít DevTools
        keystroke "i" using {command down, option down}
        delay 2
        
        -- Kliknout na Network tab
        -- Používáme keyboard navigation
        keystroke tab using {shift down} -- Focus na tab bar
        delay 0.5
        keystroke "n" -- Network
    end tell
end tell
EOF
}

open_elements() {
    echo -e "${YELLOW}🔍 Otevírám Elements inspector...${NC}"
    
    osascript << EOF
tell application "Google Chrome"
    activate
    open location "$URL"
    delay 3
    
    tell application "System Events"
        -- Otevřít DevTools s Elements tab (Cmd+Option+C)
        keystroke "c" using {command down, option down}
    end tell
    delay 2
end tell
EOF
}

take_screenshot() {
    echo -e "${YELLOW}📸 Pořizuji screenshot...${NC}"
    
    TIMESTAMP=$(date +%Y%m%d_%H%M%S)
    SCREENSHOT="/tmp/browser_${ACTION}_${TIMESTAMP}.png"
    
    # Počkat chvíli a udělat screenshot
    sleep 2
    screencapture -x "$SCREENSHOT"
    
    echo -e "${GREEN}✅ Screenshot uložen: $SCREENSHOT${NC}"
    echo ""
    echo "Pro zobrazení:"
    echo -e "${BLUE}Read file_path=\"$SCREENSHOT\"${NC}"
}

fill_form_example() {
    echo -e "${YELLOW}📝 Vyplňuji formulář (experimentální)...${NC}"
    
    osascript << EOF
tell application "Google Chrome"
    activate
    open location "$URL"
    delay 3
    
    -- Příklad vyplnění formuláře pomocí JavaScript
    execute front window's active tab javascript "
        // Najít input podle ID nebo name
        var nameInput = document.getElementById('Name') || document.querySelector('input[name=Name]');
        if (nameInput) {
            nameInput.value = 'Test Project';
            nameInput.dispatchEvent(new Event('input', {bubbles: true}));
        }
        
        // Vybrat hodnotu v select boxu
        var customerSelect = document.getElementById('customerSelect');
        if (customerSelect) {
            customerSelect.value = customerSelect.options[1].value;
            customerSelect.dispatchEvent(new Event('change', {bubbles: true}));
        }
        
        console.log('Form filled!');
    "
end tell
EOF
}

click_button_by_text() {
    BUTTON_TEXT="${2:-Submit}"
    echo -e "${YELLOW}👆 Klikám na tlačítko: $BUTTON_TEXT${NC}"
    
    osascript << EOF
tell application "Google Chrome"
    activate
    
    execute front window's active tab javascript "
        // Najít tlačítko podle textu
        var buttons = Array.from(document.querySelectorAll('button, input[type=submit], a.btn'));
        var targetButton = buttons.find(btn => btn.textContent.includes('$BUTTON_TEXT'));
        
        if (targetButton) {
            targetButton.click();
            console.log('Clicked button: $BUTTON_TEXT');
        } else {
            console.error('Button not found: $BUTTON_TEXT');
        }
    "
end tell
EOF
}

# Hlavní logika
case "$ACTION" in
    "help"|"-h"|"--help")
        show_help
        ;;
    "open-devtools")
        open_with_devtools
        take_screenshot
        ;;
    "console-log"|"console")
        open_console
        take_screenshot
        ;;
    "network-tab"|"network")
        open_network
        take_screenshot
        ;;
    "elements"|"inspector")
        open_elements
        take_screenshot
        ;;
    "screenshot")
        take_screenshot
        ;;
    "fill-form")
        fill_form_example
        take_screenshot
        ;;
    "click-button")
        click_button_by_text "$2"
        ;;
    *)
        echo -e "${YELLOW}Neznámá akce: $ACTION${NC}"
        echo ""
        show_help
        exit 1
        ;;
esac