# Terminal Instructions for AI Tools Implementation

## 🖥️ Terminal Setup Instructions

### Prerequisites Check (All Terminals)
```bash
# Verify .NET SDK
dotnet --version

# Verify Git
git --version

# Verify Docker (for sandbox features)
docker --version

# Check current branch
git status
```

---

## Terminal 1: Core Infrastructure
```bash
# Create feature branch
git checkout -b feature/ai-tools-core-infrastructure

# Work on Phase 1 items from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Focus on: Base Interfaces, Models, and DTOs

# Instructions for Claude:
# "I'm working on Phase 1: Core Infrastructure from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Please implement all items in sections 1.1, 1.2, and 1.3
# Follow the existing patterns in OAI.Core for entities and DTOs"

# Regular commits
git add .
git commit -m "Add AI Tools core interfaces and models"
```

---

## Terminal 2: Core Services
```bash
# Create feature branch
git checkout -b feature/ai-tools-core-services

# Work on Phase 2 items
# Focus on: Tool Registry and Executor Services

# Instructions for Claude:
# "I'm working on Phase 2: Core Services from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Please implement all items in sections 2.1, 2.2, and 2.3
# Ensure services follow the BaseService pattern and auto-registration naming"

# Sync with main periodically
git fetch origin
git rebase origin/main
```

---

## Terminal 3: Security & Validation
```bash
# Create feature branch
git checkout -b feature/ai-tools-security

# Work on Phase 3 items
# Focus on: Security, Validation, and Sandboxing

# Instructions for Claude:
# "I'm working on Phase 3: Security & Validation from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Implement all security measures in sections 3.1, 3.2, and 3.3
# Use FluentValidation for validators and follow existing validation patterns"
```

---

## Terminal 4: Basic Tools
```bash
# Create feature branch
git checkout -b feature/ai-tools-basic

# Work on Phase 4 items
# Focus on: File Operations, Text Processing, Data Extraction

# Instructions for Claude:
# "I'm working on Phase 4: Basic Tools Implementation from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Create the basic tools in sections 4.1, 4.2, and 4.3
# Each tool should inherit from BaseTool created in Phase 1"
```

---

## Terminal 5: Web Search Tool
```bash
# Create feature branch
git checkout -b feature/ai-tools-websearch

# Work on Phase 5 items
# Focus on: Web Search implementation

# Instructions for Claude:
# "I'm working on Phase 5: Web Search Tool from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Implement complete web search functionality from sections 5.1, 5.2, and 5.3
# Add necessary NuGet packages for web scraping and search APIs"

# Add search API keys to appsettings.Development.json
# {
#   "SearchProviders": {
#     "Google": {
#       "ApiKey": "YOUR_API_KEY",
#       "SearchEngineId": "YOUR_ENGINE_ID"
#     }
#   }
# }
```

---

## Terminal 6: Code Generation Tool
```bash
# Create feature branch
git checkout -b feature/ai-tools-codegen

# Work on Phase 6 items
# Focus on: Code Generation and Execution

# Instructions for Claude:
# "I'm working on Phase 6: Code Generation Tool from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Implement code generation features from sections 6.1, 6.2, and 6.3
# Ensure Docker is used for secure code execution"
```

---

## Terminal 7: API & Controllers
```bash
# Create feature branch
git checkout -b feature/ai-tools-api

# Work on Phase 7 items
# Focus on: REST API endpoints

# Instructions for Claude:
# "I'm working on Phase 7: API & Controllers from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Create all controllers and endpoints from sections 7.1, 7.2, and 7.3
# Follow the BaseApiController pattern and add Swagger documentation"
```

---

## Terminal 8: UI & Integration
```bash
# Create feature branch
git checkout -b feature/ai-tools-ui

# Work on Phase 8 items
# Focus on: User Interface and SignalR

# Instructions for Claude:
# "I'm working on Phase 8: UI & Integration from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Create UI components from sections 8.1, 8.2, and 8.3
# Use AdminLTE components and follow existing UI patterns"
```

---

## Terminal 9: Advanced Tools
```bash
# Create feature branch
git checkout -b feature/ai-tools-advanced

# Work on Phase 9 items
# Focus on: Database, API, and Workflow tools

# Instructions for Claude:
# "I'm working on Phase 9: Advanced Tools from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Implement advanced tools from sections 9.1, 9.2, and 9.3
# These should build upon the basic tools infrastructure"
```

---

## Terminal 10: Testing & Documentation
```bash
# Create feature branch
git checkout -b feature/ai-tools-tests

# Work on Phase 10 items
# Focus on: Testing and Documentation

# Instructions for Claude:
# "I'm working on Phase 10: Testing & Documentation from AI_TOOLS_IMPLEMENTATION_PLAN.md
# Create comprehensive tests and documentation from sections 10.1, 10.2, and 10.3
# Use xUnit for testing and follow existing test patterns"
```

---

## 🔄 Synchronization Commands (All Terminals)

### Before Starting Work
```bash
# Fetch latest changes
git fetch origin

# Ensure you're on your feature branch
git checkout feature/ai-tools-[your-phase]

# Rebase on latest main
git rebase origin/main
```

### During Development
```bash
# Check your changes
git status

# Stage and commit regularly
git add .
git commit -m "Descriptive commit message"

# Push to remote
git push origin feature/ai-tools-[your-phase]
```

### Handling Conflicts
```bash
# If conflicts occur during rebase
git status  # See conflicted files
# Resolve conflicts in editor
git add [resolved-files]
git rebase --continue
```

---

## 📋 Daily Workflow

1. **Morning Sync (All Terminals)**
   ```bash
   git fetch origin
   git rebase origin/main
   ```

2. **Check TODO Progress**
   - Open AI_TOOLS_IMPLEMENTATION_PLAN.md
   - Mark completed items with [x]
   - Commit the updated plan

3. **Development**
   - Work on assigned phase items
   - Commit frequently with descriptive messages
   - Push at least every 2 hours

4. **End of Day**
   ```bash
   # Push all changes
   git push origin feature/ai-tools-[your-phase]
   
   # Create PR if phase is complete
   # Use GitHub CLI or web interface
   ```

---

## 🚨 Important Notes

1. **Communication**
   - Use git commits to communicate progress
   - Update checkboxes in AI_TOOLS_IMPLEMENTATION_PLAN.md
   - Create issues for blockers

2. **Code Quality**
   - Follow existing patterns in codebase
   - Run `dotnet build` before committing
   - Ensure no compiler warnings

3. **Dependencies**
   - Coordinate NuGet package additions
   - Document any new dependencies
   - Update CLAUDE.md with new patterns

4. **Testing**
   - Test your phase independently
   - Ensure backwards compatibility
   - Document breaking changes

---

## 🎯 Quick Reference

| Terminal | Phase | Branch Name | Focus Area |
|----------|-------|-------------|------------|
| 1 | Core Infrastructure | feature/ai-tools-core-infrastructure | Interfaces, Models |
| 2 | Core Services | feature/ai-tools-core-services | Registry, Executor |
| 3 | Security | feature/ai-tools-security | Validation, Sandbox |
| 4 | Basic Tools | feature/ai-tools-basic | File, Text, Data |
| 5 | Web Search | feature/ai-tools-websearch | Search, Scraping |
| 6 | Code Gen | feature/ai-tools-codegen | Code Generation |
| 7 | API | feature/ai-tools-api | Controllers, Endpoints |
| 8 | UI | feature/ai-tools-ui | Views, SignalR |
| 9 | Advanced | feature/ai-tools-advanced | DB, API, Workflow |
| 10 | Testing | feature/ai-tools-tests | Tests, Docs |

---

## 🔧 Troubleshooting

### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Git Issues
```bash
# Reset to clean state (careful!)
git reset --hard origin/feature/ai-tools-[your-phase]

# Stash changes temporarily
git stash
git stash pop
```

### Port Conflicts
```bash
# Check if port 5005 is in use
lsof -i :5005  # macOS/Linux
netstat -ano | findstr :5005  # Windows

# Kill the process or use different port
```

---

Last Updated: {{ current_date }}