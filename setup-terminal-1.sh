#!/bin/bash
cd /Users/lewi/Documents/Vyvoj/OptimalyAI
echo "TERMINAL 1: Core Infrastructure"
echo "================================="
echo ""
git checkout -b feature/ai-tools-infrastructure 2>/dev/null || git checkout feature/ai-tools-infrastructure
echo ""
echo "✅ Ready! Now run: code ."
echo ""
echo "Then tell Claude:"
echo "I am working on Terminal 1 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md"
echo "Start with Phase 1: Core Interfaces"