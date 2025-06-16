#\!/bin/bash
# Příklad, jak testuji změny v prohlížeči

# 1. Otevřu stránku
open "https://localhost:5005/Projects/Create?customerId=19513ef7-90e5-4c5a-ac58-02684a445b5a"

# 2. Počkám, až se načte
sleep 3

# 3. Udělám screenshot aktivního okna
screencapture -w /tmp/test_screenshot.png

# 4. Pak můžu screenshot zobrazit pomocí Read nástroje
echo "Screenshot uložen do /tmp/test_screenshot.png"
EOF < /dev/null