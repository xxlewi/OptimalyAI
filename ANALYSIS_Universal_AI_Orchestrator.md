# Universal AI Orchestrator - Implementační checklist

## Koncept: AutoOrchestrator
Autonomní AI orchestrační platforma, která automaticky rozhoduje, jaké AI modely použít, v jakém pořadí a jak jejich výstupy zkombinovat. Na vstupu dostane pouze textový požadavek, zbytek vyřeší autonomně.

### Klíčové komponenty:
- **Planner Agent**: AI model vytvářející exekuční plány jako DAG
- **Model Registry**: Centrální registr dostupných modelů a jejich schopností  
- **Execution Engine**: Paralelní spouštění modelů podle závislostí
- **Self-Healing**: Automatická validace a doplnění chybějících dat

## Infrastruktura
- **Lokální development**: MacBook M4 128GB RAM
- **Runtime**: Ollama pro správu open-source modelů
- **Základní modely**:
  - mixtral:8x7b (hlavní workhorse)
  - llama3.2:latest (rychlejší alternativa)
  - phi3:latest (super rychlý na jednoduché úlohy)

## UI Struktura (jednoduché a funkční)

### 1. Dashboard (`/`)
- Rychlé spuštění běžných úloh
- Přehled posledních běhů
- Status modelů

### 2. Projekty (`/ai/projects`)
- Seznam projektových šablon
- Vytvoření/editace projektu
- Spuštění úlohy z projektu

### 3. Modely (`/ai/models`)
- Seznam lokálních modelů (Ollama)
- Test dostupnosti
- Stažení nových modelů

## Implementační fáze

### Fáze 1: Základní entity a infrastruktura

#### Entity
- [ ] Vytvořit `OAI.Core/Entities/AIProject.cs` dědící z `BaseEntity`
  ```csharp
  public string Name { get; set; }
  public string Description { get; set; }
  public string SystemPrompt { get; set; }
  public string PrimaryModel { get; set; }
  public string FallbackModel { get; set; }
  ```

- [ ] Vytvořit `OAI.Core/Entities/AITaskRun.cs` dědící z `BaseEntity`
  ```csharp
  public int ProjectId { get; set; }
  public string Input { get; set; }
  public string Output { get; set; }
  public string Status { get; set; } // Running, Completed, Failed
  public int DurationMs { get; set; }
  public DateTime StartedAt { get; set; }
  public DateTime? CompletedAt { get; set; }
  ```

- [ ] Vytvořit `OAI.Core/Entities/AIModelRegistration.cs` dědící z `BaseEntity`
  ```csharp
  public string ModelName { get; set; }
  public string Provider { get; set; } // "ollama"
  public bool IsAvailable { get; set; }
  public string Capabilities { get; set; } // JSON
  public DateTime LastHealthCheck { get; set; }
  ```

#### DTOs
- [ ] Vytvořit `OAI.Core/DTOs/AIProjectDto.cs` dědící z `BaseDto`
- [ ] Vytvořit `OAI.Core/DTOs/CreateAIProjectDto.cs` dědící z `CreateDtoBase`
- [ ] Vytvořit `OAI.Core/DTOs/RunTaskDto.cs`
- [ ] Vytvořit `OAI.Core/DTOs/TaskRunResultDto.cs`

### Fáze 2: Ollama integrace

- [ ] Vytvořit složku `AI/Services/`
- [ ] Vytvořit `AI/Services/OllamaService.cs`
  ```csharp
  public interface IOllamaService
  {
      Task<string> GenerateAsync(string model, string prompt);
      Task<List<OllamaModel>> ListModelsAsync();
      Task<bool> IsModelAvailableAsync(string model);
  }
  ```

- [ ] Vytvořit `AI/Models/OllamaModel.cs`
  ```csharp
  public class OllamaModel
  {
      public string Name { get; set; }
      public string Size { get; set; }
      public DateTime ModifiedAt { get; set; }
  }
  ```

- [ ] Vytvořit `AI/Models/OllamaGenerateRequest.cs`
- [ ] Vytvořit `AI/Models/OllamaGenerateResponse.cs`

### Fáze 3: Service Layer

- [ ] Vytvořit `OAI.ServiceLayer/Interfaces/IAIProjectService.cs`
  ```csharp
  public interface IAIProjectService : IBaseService<AIProject>
  {
      Task<AIProjectDto> GetProjectAsync(int id);
      Task<List<AIProjectDto>> GetAllProjectsAsync();
      Task<AIProjectDto> CreateProjectAsync(CreateAIProjectDto dto);
  }
  ```

- [ ] Vytvořit `OAI.ServiceLayer/Services/AIProjectService.cs`

- [ ] Vytvořit `OAI.ServiceLayer/Interfaces/IAITaskRunService.cs`
  ```csharp
  public interface IAITaskRunService : IBaseService<AITaskRun>
  {
      Task<TaskRunResultDto> RunTaskAsync(int projectId, RunTaskDto dto);
      Task<List<AITaskRun>> GetProjectRunsAsync(int projectId);
  }
  ```

- [ ] Vytvořit `OAI.ServiceLayer/Services/AITaskRunService.cs`

### Fáze 4: Základní UI Controllers

- [ ] Vytvořit `Controllers/AIOrchestatorController.cs`
  ```csharp
  public class AIOrchestatorController : Controller
  {
      public IActionResult Index() // Dashboard
      public IActionResult Projects() // Seznam projektů
      public IActionResult ProjectDetail(int id)
      public IActionResult Models() // Seznam modelů
  }
  ```

- [ ] Vytvořit API controller `Controllers/AIApiController.cs` dědící z `BaseApiController`
  ```csharp
  [HttpPost("projects/{id}/run")]
  public async Task<ActionResult<ApiResponse<TaskRunResultDto>>> RunTask(int id, RunTaskDto dto)
  
  [HttpGet("models")]
  public async Task<ActionResult<ApiResponse<List<OllamaModel>>>> GetModels()
  ```

### Fáze 5: Views

- [ ] Vytvořit `Views/AIOrchestator/Index.cshtml` (Dashboard)
- [ ] Vytvořit `Views/AIOrchestator/Projects.cshtml` (Seznam projektů)
- [ ] Vytvořit `Views/AIOrchestator/ProjectDetail.cshtml` (Detail + spuštění)
- [ ] Vytvořit `Views/AIOrchestator/Models.cshtml` (Seznam modelů)
- [ ] Vytvořit `Views/AIOrchestator/_RunTaskForm.cshtml` (Partial view pro form)

### Fáze 6: JavaScript pro real-time updates

- [ ] Vytvořit `wwwroot/js/ai-orchestrator.js`
  - Polling pro status běžících úloh
  - AJAX pro spuštění úlohy
  - Update UI s výsledky

### Fáze 7: Mappers a Validators

- [ ] Vytvořit `OAI.ServiceLayer/Mapping/AIProjectMapper.cs`
- [ ] Vytvořit `OAI.ServiceLayer/Mapping/AITaskRunMapper.cs`
- [ ] Vytvořit `Validation/CreateAIProjectDtoValidator.cs`
- [ ] Vytvořit `Validation/RunTaskDtoValidator.cs`

### Fáze 8: Pokročilé features (Fáze 2)

- [ ] Implementovat Planner Agent
- [ ] Vytvořit ExecutionPlan entitu pro DAG
- [ ] Implementovat paralelní execution engine
- [ ] Self-healing mechanismus
- [ ] Visual DAG designer

### Fáze 9: Konfigurace

- [ ] Upravit `appsettings.json`
  ```json
  {
    "OllamaSettings": {
      "BaseUrl": "http://localhost:11434",
      "DefaultTimeout": 300
    }
  }
  ```

- [ ] Registrace služeb v `ServiceCollectionExtensions.cs`

### Fáze 10: Seed data a testování

- [ ] Vytvořit základní projekty (Business Analyst, Code Assistant)
- [ ] Otestovat Ollama integraci
- [ ] Vytvořit sample prompty

## První kroky

1. **Instalace Ollama**: `brew install ollama`
2. **Stažení modelů**: 
   - `ollama pull mixtral:8x7b`
   - `ollama pull llama3.2`
   - `ollama pull phi3`
3. **Spuštění Ollama**: `ollama serve`

## API Endpoints (pro pozdější použití)

```
POST   /api/ai/projects/{id}/run     - Spustit úlohu
GET    /api/ai/projects/{id}/runs    - Historie běhů
GET    /api/ai/models                - Seznam modelů
GET    /api/ai/runs/{id}/status      - Status běhu
DELETE /api/ai/runs/{id}             - Zrušit běh
```

## Poznámky

- Začínáme jednoduše - základní UI, Ollama integrace, spouštění úloh
- Později přidáme autonomní plánování a pokročilé features
- UI bude jednoduché a funkční, ne "letadlo"
- Vše poběží lokálně na M4 Macbooku s 128GB RAM