# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Database Configuration
- **Development**: Uses In-Memory database (no local database needed)
- **Production**: Uses PostgreSQL via Docker
- **NEVER** use LocalDB, SQLite, or any local file-based database
- Migrations are created but not applied in development (In-Memory DB recreates on each run)

### Switching Between Databases
- Set `"UseProductionDatabase": true` in appsettings.json for PostgreSQL
- Set `"UseProductionDatabase": false` in appsettings.json for In-Memory (default)

### PostgreSQL with Docker
```bash
# Start PostgreSQL
./docker-db-start.sh

# Stop PostgreSQL  
./docker-db-stop.sh

# Apply migrations
dotnet ef database update
```

### Connection Details
- Host: localhost
- Port: 5432  
- Database: optimalyai_db
- Username: optimaly
- Password: OptimalyAI2024!

## Key URLs and Pages

- **Dashboard**: `https://localhost:5005/`
- **AI Tools UI**: `https://localhost:5005/Tools` - Interactive testing of AI tools
- **AI Tools API**: `https://localhost:5005/api/tools` - REST API for tools
- **Swagger Docs**: `https://localhost:5005/api/docs` - API documentation
- **Ollama Models**: `https://localhost:5005/Models` - AI model management
- **Chat Interface**: `https://localhost:5005/Chat` - AI chat conversations

## Development Commands

### Build and Run
```bash
# Development mode with hot reload
python run-dev.py

# Direct dotnet commands
dotnet build                                    # Build all projects
dotnet run --project OptimalyAI.csproj         # Run the web application
dotnet watch run --project OptimalyAI.csproj   # Run with hot reload
```

### Build Guidance
- dělej buil výhradně přes pytho (always build exclusively through Python)

### Ports and URLs
- Application runs on `https://localhost:5005`
- Swagger documentation: `https://localhost:5005/api/docs`
- Logs are written to: `logs/optimaly-ai-{date}.log`

## Razor Guidance
- Vždy pečlivě kontroluj syntaxi Razor, aby jsi předešel syntaktickým chybám

## Browser Testing Tools

### Visual Testing and Verification
Pro vizuální ověření změn v prohlížeči používej následující nástroje:

1. **Screenshot capture**:
```bash
# Celá obrazovka
screencapture -x screenshot.png

# Konkrétní okno (interaktivní)
screencapture -w screenshot.png

# S časovým zpožděním
sleep 3 && screencapture -w screenshot.png
```

2. **Otevření URL v prohlížeči**:
```bash
open "https://localhost:5005/Projects/Create"
```

3. **Zobrazení screenshotu**:
```
# Použij Read tool pro zobrazení PNG souborů
Read file_path="/tmp/screenshot.png"
```

### Browser Inspector Tools
V adresáři `Tools/` jsou k dispozici pokročilé nástroje pro inspekci prohlížeče:

- **browser-inspector.js** - JavaScript knihovna pro zachytávání console logů, network requestů, chyb a DOM stavu
- **browser-test.sh** - Kompletní testovací nástroj s reportováním
- **analyze-browser-state.py** - Analyzátor exportovaných dat z prohlížeče

### Příklad použití pro testování
```bash
# 1. Otevřít stránku
open "https://localhost:5005/Projects/Create?customerId=19513ef7-90e5-4c5a-ac58-02684a445b5a"

# 2. Počkat na načtení a udělat screenshot
sleep 3 && screencapture -w /tmp/test.png

# 3. Zobrazit screenshot pomocí Read tool
Read file_path="/tmp/test.png"
```

## Recent Fixes and Features

### Customer Pre-selection in Projects (Fixed 14.6.2025)
- Při vytváření projektu ze stránky detailu zákazníka se nyní zákazník automaticky předvyplní
- Opravený JavaScript kód v `Views/Projects/Create.cshtml` správně nastavuje hodnotu v Select2 dropdownu
- Funguje stejně jako u vytváření požadavků

### Known Issues and Solutions

1. **JavaScript syntax errors v Razor views**:
   - Vždy používej `@Html.Raw(System.Text.Json.JsonSerializer.Serialize())` pro escapování C# hodnot v JavaScriptu
   - Nikdy nevkládej `@Model` properties přímo do JavaScript kódu

2. **Build errors**:
   - Pokud aplikace nechce naběhnout, zkontroluj logy: `tail -50 logs/dev-runner-*.log`
   - Časté chyby: chybějící properties v DTO, syntaktické chyby v Razor

## Testing Workflow
1. Udělej změny v kódu
2. Spusť aplikaci: `python run-dev.py`
3. Otevři URL v prohlížeči: `open "https://localhost:5005/..."`
4. Udělej screenshot: `screencapture -w /tmp/test.png`
5. Zobraz výsledek: `Read file_path="/tmp/test.png"`
6. Commitni změny s popisným commit message

[Rest of the file remains the same...]