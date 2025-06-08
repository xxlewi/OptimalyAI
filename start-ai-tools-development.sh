#!/bin/bash

# AI Tools Development - Terminal Launcher Script
# This script opens 4 terminal windows for parallel development

PROJECT_DIR="/Users/lewi/Documents/Vyvoj/OptimalyAI"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Starting AI Tools Development Environment${NC}"
echo -e "${YELLOW}Opening 4 terminal windows for parallel development...${NC}\n"

# Terminal 1 - Core Infrastructure (CRITICAL - Must start first!)
osascript <<EOF
tell application "Terminal"
    activate
    do script "cd $PROJECT_DIR && echo -e '${GREEN}🏗️  Terminal 1: Core Infrastructure${NC}' && echo -e '${YELLOW}Branch: feature/ai-tools-infrastructure${NC}' && echo '' && echo 'Creating feature branch...' && git checkout -b feature/ai-tools-infrastructure && echo '' && echo -e '${GREEN}✓ Ready for development${NC}' && echo '' && echo 'Instructions:' && echo '1. Open VS Code: code .' && echo '2. Tell Claude: I am working on Terminal 1 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md' && echo '3. Start with Phase 1: Core Interfaces' && echo '' && echo -e '${RED}IMPORTANT: Other terminals depend on this work!${NC}'"
    set currentTab to current tab of window 1
    set custom title of currentTab to "T1: Core Infrastructure"
end tell
EOF

sleep 2

# Terminal 2 - Basic Tools (Waits for T1 Phase 2)
osascript <<EOF
tell application "Terminal"
    do script "cd $PROJECT_DIR && echo -e '${GREEN}🛠️  Terminal 2: Basic Tools${NC}' && echo -e '${YELLOW}Branch: feature/ai-tools-basic${NC}' && echo '' && echo -e '${RED}⏳ WAITING: Terminal 1 must complete Phase 1 & 2 first!${NC}' && echo '' && echo 'When ready, run:' && echo '  git checkout -b feature/ai-tools-basic' && echo '  git pull origin feature/ai-tools-infrastructure' && echo '  code .' && echo '' && echo 'Then tell Claude: I am working on Terminal 2 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md'"
    set currentTab to current tab of window 1
    set custom title of currentTab to "T2: Basic Tools"
end tell
EOF

sleep 2

# Terminal 3 - Advanced Tools (Waits for T1 Phase 3)
osascript <<EOF
tell application "Terminal"
    do script "cd $PROJECT_DIR && echo -e '${GREEN}🚀 Terminal 3: Advanced Tools${NC}' && echo -e '${YELLOW}Branch: feature/ai-tools-advanced${NC}' && echo '' && echo -e '${RED}⏳ WAITING: Terminal 1 must complete Phase 1, 2 & 3 first!${NC}' && echo '' && echo 'When ready, run:' && echo '  git checkout -b feature/ai-tools-advanced' && echo '  git pull origin feature/ai-tools-infrastructure' && echo '  code .' && echo '' && echo 'Then tell Claude: I am working on Terminal 3 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md'"
    set currentTab to current tab of window 1
    set custom title of currentTab to "T3: Advanced Tools"
end tell
EOF

sleep 2

# Terminal 4 - API & UI (Waits for T1 Phase 3 and coordination with T2)
osascript <<EOF
tell application "Terminal"
    do script "cd $PROJECT_DIR && echo -e '${GREEN}🎨 Terminal 4: API & UI${NC}' && echo -e '${YELLOW}Branch: feature/ai-tools-api-ui${NC}' && echo '' && echo -e '${RED}⏳ WAITING: Terminal 1 must complete Phase 1-3 first!${NC}' && echo '' && echo 'When ready, run:' && echo '  git checkout -b feature/ai-tools-api-ui' && echo '  git pull origin feature/ai-tools-infrastructure' && echo '  code .' && echo '' && echo 'Then tell Claude: I am working on Terminal 4 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md'"
    set currentTab to current tab of window 1
    set custom title of currentTab to "T4: API & UI"
end tell
EOF

echo -e "\n${GREEN}✅ All terminals opened successfully!${NC}"
echo -e "\n${BLUE}📋 Development Order:${NC}"
echo "1. Start with Terminal 1 immediately"
echo "2. Terminal 2 can start after T1 completes Phase 2"
echo "3. Terminals 3 & 4 can start after T1 completes Phase 3"
echo -e "\n${YELLOW}Check AI_TOOLS_PARALLEL_IMPLEMENTATION.md for detailed instructions${NC}"