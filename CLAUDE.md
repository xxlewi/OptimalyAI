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

[Rest of the file remains the same...]