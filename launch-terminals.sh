#!/bin/bash

PROJECT_DIR="/Users/lewi/Documents/Vyvoj/OptimalyAI"

echo "🚀 Starting AI Tools Development Environment"
echo "Opening 4 terminal windows for parallel development..."

# Terminal 1 - Core Infrastructure
osascript -e 'tell application "Terminal" to activate'
osascript -e 'tell application "Terminal" to do script "cd '"$PROJECT_DIR"' && echo \"🏗️  Terminal 1: Core Infrastructure\" && echo \"Branch: feature/ai-tools-infrastructure\" && echo \"\" && echo \"Creating feature branch...\" && git checkout -b feature/ai-tools-infrastructure && echo \"\" && echo \"✓ Ready! Now run: code .\" && echo \"\" && echo \"Tell Claude: I am working on Terminal 1 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md\""'

sleep 2

# Terminal 2 - Basic Tools
osascript -e 'tell application "Terminal" to do script "cd '"$PROJECT_DIR"' && echo \"🛠️  Terminal 2: Basic Tools\" && echo \"Branch: feature/ai-tools-basic\" && echo \"\" && echo \"⏳ WAIT for Terminal 1 to complete Phase 2\" && echo \"\" && echo \"When ready, run:\" && echo \"  git checkout -b feature/ai-tools-basic\" && echo \"  git pull origin feature/ai-tools-infrastructure\" && echo \"  code .\""'

sleep 2

# Terminal 3 - Advanced Tools
osascript -e 'tell application "Terminal" to do script "cd '"$PROJECT_DIR"' && echo \"🚀 Terminal 3: Advanced Tools\" && echo \"Branch: feature/ai-tools-advanced\" && echo \"\" && echo \"⏳ WAIT for Terminal 1 to complete Phase 3\" && echo \"\" && echo \"When ready, run:\" && echo \"  git checkout -b feature/ai-tools-advanced\" && echo \"  git pull origin feature/ai-tools-infrastructure\" && echo \"  code .\""'

sleep 2

# Terminal 4 - API & UI
osascript -e 'tell application "Terminal" to do script "cd '"$PROJECT_DIR"' && echo \"🎨 Terminal 4: API & UI\" && echo \"Branch: feature/ai-tools-api-ui\" && echo \"\" && echo \"⏳ WAIT for Terminal 1 to complete Phase 3\" && echo \"\" && echo \"When ready, run:\" && echo \"  git checkout -b feature/ai-tools-api-ui\" && echo \"  git pull origin feature/ai-tools-infrastructure\" && echo \"  code .\""'

echo ""
echo "✅ All terminals opened!"
echo ""
echo "📋 Start with Terminal 1 immediately!"
echo "📋 Other terminals have instructions when to start"