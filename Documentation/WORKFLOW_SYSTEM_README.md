# Project Workflow System - Complete Guide

## 🎯 Přehled systému

Project Workflow System je pokročilá platforma pro vytváření, správu a spouštění AI-powered workflow s vizuálním designerem, real-time monitoringem a integrací s orchestrátory.

## 🏗️ Architektura

### Základní komponenty
```
PROJECT
  └── STAGE (workflow krok)
       ├── Orchestrator (ConversationOrchestrator / ToolChainOrchestrator)
       ├── ReAct Agent (volitelný AI reasoning)
       └── Tools[] (konkrétní akce/nástroje)
```

### Technologie
- **Backend**: ASP.NET Core, Entity Framework, SignalR
- **Frontend**: Razor Views, AdminLTE, JavaScript/jQuery
- **Database**: PostgreSQL (production) / In-Memory (development)
- **AI Integration**: Ollama models, LlmTornado toolkit

## 🚀 Hlavní funkce

### 1. Vizuální Workflow Designer
- **URL**: `https://localhost:5005/WorkflowDesigner?projectId={id}`
- Drag & drop interface pro vytváření workflow
- Real-time validace a náhled
- Konfigurace orchestrátorů a tools
- Canvas s vizuálními spojeními mezi stages

### 2. Workflow Templates
- **URL**: `https://localhost:5005/ProjectWorkflows/Templates`
- Galerie workflow šablon
- Import/export šablon (JSON)
- Vytvoření projektu ze šablony
- Správa a verzování šablon

### 3. Workflow Execution
- **API**: `POST /api/workflow/{projectId}/execute`
- Spouštění workflow přes orchestrátory
- Real-time monitoring s SignalR
- Podpora pro AI modely a tools
- Error handling a retry mechanismy

### 4. Real-time Monitoring
- **URL**: `https://localhost:5005/ProjectWorkflows/Monitor`
- Live sledování běžících workflow
- SignalR notifikace o progress
- Detailed logs a stage results
- Možnost zrušení běhu

## 📋 Rychlý start

### 1. Vytvoření nového workflow
```bash
# 1. Vytvořte nový projekt
curl -X POST https://localhost:5005/api/projects \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Workflow",
    "description": "Test workflow",
    "type": "Development"
  }'

# 2. Otevřete workflow designer
# https://localhost:5005/WorkflowDesigner?projectId={project-id}

# 3. Přidejte stages a tools pomocí UI
```

### 2. Spuštění workflow
```bash
# Spuštění workflow
curl -X POST https://localhost:5005/api/workflow/{project-id}/execute \
  -H "Content-Type: application/json" \
  -d '{
    "parameters": {
      "input": "test data"
    },
    "initiatedBy": "user"
  }'

# Monitoring běhu
curl https://localhost:5005/api/workflow/executions/{execution-id}/status
```

### 3. Použití šablon
```bash
# Seznam šablon
curl https://localhost:5005/api/workflow/templates

# Vytvoření projektu ze šablony
curl -X POST https://localhost:5005/api/workflow/templates/{template-id}/create-project \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Project from Template",
    "description": "New project"
  }'
```

## 🔧 API Reference

### Workflow Management
- `GET /api/workflow/{projectId}/design` - Získat workflow design
- `POST /api/workflow/design` - Uložit workflow design  
- `GET /api/workflow/{projectId}/validate` - Validovat workflow
- `GET /api/workflow/components` - Dostupné komponenty

### Stage Management
- `GET /api/workflow/{projectId}/stages` - Seznam stages
- `POST /api/workflow/stages` - Vytvořit stage
- `PUT /api/workflow/stages/{stageId}` - Upravit stage
- `DELETE /api/workflow/stages/{stageId}` - Smazat stage
- `POST /api/workflow/stages/{stageId}/duplicate` - Duplikovat stage

### Workflow Execution
- `POST /api/workflow/{projectId}/execute` - Spustit workflow
- `GET /api/workflow/executions/{executionId}/status` - Status execution
- `POST /api/workflow/executions/{executionId}/cancel` - Zrušit execution
- `GET /api/workflow/executions/{executionId}/stages` - Výsledky stages

### Template Management
- `GET /api/workflow/templates` - Seznam šablon
- `POST /api/workflow/{projectId}/convert-to-template` - Převést na šablonu
- `POST /api/workflow/templates/{templateId}/create-project` - Vytvořit ze šablony

## 🎨 UI Komponenty

### Workflow Designer
```javascript
// Inicializace workflow designer
const designer = new WorkflowDesigner({
    projectId: 'project-guid',
    container: '#workflow-canvas',
    apiBaseUrl: '/api/workflow'
});

// Event handling
designer.on('stageAdded', function(stage) {
    console.log('New stage added:', stage);
});

designer.on('validationError', function(errors) {
    console.log('Validation errors:', errors);
});
```

### SignalR Monitoring
```javascript
// Připojení k workflow monitoring
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/workflowHub")
    .build();

// Event handlers
connection.on("WorkflowStarted", function(data) {
    updateProgress(data);
});

connection.on("StageCompleted", function(data) {
    markStageComplete(data);
});

// Start monitoring
connection.start().then(function() {
    connection.invoke("JoinExecution", executionId);
});
```

## 🔌 Orchestrátory

### ConversationOrchestrator
- Integrace s Ollama AI modely
- Automatická detekce potřeby tools
- Support pro streaming responses
- ReAct pattern pro reasoning

### ToolChainOrchestrator  
- Sekvenční/paralelní execution tools
- Parameter mapping mezi tools
- Error handling strategies
- Conditional execution

### ProjectStageOrchestrator
- High-level orchestrace stages
- Delegace na sub-orchestrátory
- Context management
- Progress tracking

## 🛠️ Tools Integration

### Registrace tools
```csharp
public class MyCustomTool : ITool
{
    public string Id => "my_custom_tool";
    public string Name => "My Custom Tool";
    public string Category => "Custom";
    
    public IReadOnlyList<IToolParameter> Parameters => new[]
    {
        new SimpleToolParameter
        {
            Name = "input",
            Type = ToolParameterType.String,
            IsRequired = true
        }
    };
    
    public async Task<IToolResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        // Tool implementation
        return new ToolResult 
        { 
            IsSuccess = true, 
            Data = result 
        };
    }
}
```

### Použití v stages
```json
{
  "stageTools": [
    {
      "toolId": "my_custom_tool",
      "order": 1,
      "configuration": {
        "timeout": 30,
        "retries": 3
      },
      "inputMapping": {
        "input": "previousStage.output"
      }
    }
  ]
}
```

## 📊 Monitoring a Logs

### Execution Tracking
- Real-time status updates
- Stage-by-stage progress
- Tool execution results
- Error details a stack traces
- Performance metrics

### SignalR Events
- `WorkflowStarted` - Workflow začal
- `StageStarted` - Stage zahájen
- `StageCompleted` - Stage dokončen
- `ToolExecuted` - Tool vykonán
- `WorkflowCompleted` - Workflow dokončen

## 🧪 Testing

### Unit Tests
```bash
# Spuštění unit testů
dotnet test OptimalyAI.Tests

# Specifické testy
dotnet test --filter "WorkflowExecutionService"
```

### Integration Tests
```bash
# E2E test celého systému
./Test/test-workflow-e2e.sh

# Test workflow execution
./Test/test-workflow-execution.sh
```

### Manual Testing
1. Otevřete workflow designer
2. Vytvořte test workflow s stages
3. Přidejte tools do stages
4. Spusťte workflow
5. Sledujte monitoring

## 🔒 Security

### API Security
- Rate limiting (100 req/min)
- CORS konfigurace
- Validation na všech endpoints
- Error handling bez stack traces

### Tool Security
```csharp
// Tool security validation
public class ToolSecurityService : IToolSecurity
{
    public async Task<bool> ValidateToolExecution(
        string toolId, 
        Dictionary<string, object> parameters)
    {
        // Security validation logic
        return true;
    }
}
```

## 🚀 Deployment

### Development
```bash
# Start aplikace
python run-dev.py

# Nebo přímo
dotnet watch run --project OptimalyAI.csproj
```

### Production
```bash
# PostgreSQL database
./docker-db-start.sh

# Apply migrations
dotnet ef database update

# Build a spuštění
dotnet build --configuration Release
dotnet run --configuration Release
```

### Environment Configuration
```json
{
  "UseProductionDatabase": true,
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=optimalyai_db;Username=optimaly;Password=OptimalyAI2024!"
  }
}
```

## 📈 Performance

### Optimalizace
- Async/await patterns všude
- Entity Framework optimalizace
- SignalR connection pooling
- Client-side caching

### Monitoring
- Application Insights integration
- Custom metrics collection
- Performance counters
- Real-time dashboard

## 🐛 Troubleshooting

### Časté problémy

1. **Workflow se nespustí**
   ```bash
   # Zkontrolujte validaci
   curl https://localhost:5005/api/workflow/{projectId}/validate
   ```

2. **SignalR nefunguje**
   ```javascript
   // Zkontrolujte connection status
   console.log(connection.state);
   ```

3. **Tools selže**
   ```bash
   # Zkontrolujte tool registry
   curl https://localhost:5005/api/tools
   ```

### Debug módy
```bash
# Enable detailed logging
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

## 📚 Další zdroje

- **API dokumentace**: `https://localhost:5005/api/docs`
- **Implementační plán**: `Documentation/Implementation/PROJECT_WORKFLOW_REDESIGN_PLAN.md`
- **CLAUDE.md**: Pokyny pro Claude Code assistant
- **Test scripts**: `Test/` složka

## 🎉 Závěr

Project Workflow System poskytuje kompletní platformu pro vytváření a spouštění AI-powered workflow s pokročilými funkcemi:

- ✅ Vizuální workflow designer
- ✅ Real-time execution monitoring  
- ✅ AI orchestrátory integrace
- ✅ Template management
- ✅ SignalR real-time updates
- ✅ Comprehensive API
- ✅ Testing tools

Systém je připraven k produkčnímu nasazení a dalšímu rozvoji!