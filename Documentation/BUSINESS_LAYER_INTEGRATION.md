# Business Layer Integration - OptimalyAI

## Přehled současného stavu

Máme plně funkční technickou vrstvu:
- **Tools** - AI nástroje (WebSearch, LlmTornado, atd.)
- **Orchestrators** - Koordinace nástrojů (ConversationOrchestrator, ToolChainOrchestrator)
- **Conversations & Messages** - Chat rozhraní s AI
- **ToolExecutions** - Logy spuštění nástrojů

Chybí business vrstva pro:
- Správu zákaznických požadavků
- Definici business workflow
- Sledování průběhu projektů
- Propojení s technickou vrstvou

## Navrhované rozšíření

### 1. Nové Business Entity

#### BusinessRequest (Obchodní požadavek)
```csharp
public class BusinessRequest : BaseEntity
{
    public string RequestNumber { get; set; } // REQ-2024-001
    public string RequestType { get; set; } // ProductPhoto, DocumentAnalysis, etc.
    public string Title { get; set; }
    public string Description { get; set; }
    public string ClientId { get; set; }
    public string ClientName { get; set; }
    public RequestStatus Status { get; set; }
    public RequestPriority Priority { get; set; }
    public DateTime? Deadline { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    
    // Relationships
    public Guid? WorkflowTemplateId { get; set; }
    public WorkflowTemplate WorkflowTemplate { get; set; }
    public List<RequestExecution> Executions { get; set; }
    public List<RequestFile> Files { get; set; }
    public string Metadata { get; set; } // JSON
}

public enum RequestStatus
{
    Draft, Submitted, Queued, Processing, Review, Completed, Failed, Cancelled
}

public enum RequestPriority
{
    Low, Normal, High, Urgent
}
```

#### WorkflowTemplate (Šablona workflow)
```csharp
public class WorkflowTemplate : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string RequestType { get; set; } // Pro který typ požadavku
    public bool IsActive { get; set; }
    public int Version { get; set; }
    
    // Definice kroků workflow
    public List<WorkflowStep> Steps { get; set; }
    public string Configuration { get; set; } // JSON s konfigurací orchestrátoru
}
```

#### WorkflowStep (Krok workflow)
```csharp
public class WorkflowStep : BaseEntity
{
    public Guid WorkflowTemplateId { get; set; }
    public WorkflowTemplate WorkflowTemplate { get; set; }
    
    public string Name { get; set; }
    public int Order { get; set; }
    public string StepType { get; set; } // Tool, Orchestrator, Manual, Condition
    public string ExecutorId { get; set; } // ID nástroje nebo orchestrátoru
    public bool IsParallel { get; set; }
    public string InputMapping { get; set; } // JSON - mapování vstupů
    public string OutputMapping { get; set; } // JSON - mapování výstupů
    public string Conditions { get; set; } // JSON - podmínky spuštění
}
```

#### RequestExecution (Spuštění požadavku)
```csharp
public class RequestExecution : BaseEntity
{
    public Guid BusinessRequestId { get; set; }
    public BusinessRequest BusinessRequest { get; set; }
    
    public ExecutionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int DurationMs { get; set; }
    
    // Propojení s technickou vrstvou
    public Guid? ConversationId { get; set; } // Pokud bylo použito chat rozhraní
    public string OrchestratorInstanceId { get; set; }
    
    public List<StepExecution> StepExecutions { get; set; }
    public string Results { get; set; } // JSON s výsledky
    public string Errors { get; set; } // JSON s chybami
}

public enum ExecutionStatus
{
    Pending, Running, Paused, Completed, Failed, Cancelled
}
```

#### StepExecution (Spuštění kroku)
```csharp
public class StepExecution : BaseEntity
{
    public Guid RequestExecutionId { get; set; }
    public RequestExecution RequestExecution { get; set; }
    
    public Guid WorkflowStepId { get; set; }
    public WorkflowStep WorkflowStep { get; set; }
    
    public ExecutionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Propojení s ToolExecution pokud byl použit nástroj
    public Guid? ToolExecutionId { get; set; }
    public ToolExecution ToolExecution { get; set; }
    
    public string Input { get; set; } // JSON
    public string Output { get; set; } // JSON
    public string Logs { get; set; }
}
```

### 2. Integrace s existujícím systémem

#### Propojení Business -> Technical
```
BusinessRequest 
  → WorkflowTemplate 
    → WorkflowSteps
      → Tools/Orchestrators (pomocí ExecutorId)
        → ToolExecutions (automaticky logované)
```

#### Příklad flow pro produktové foto:

1. **Vytvoření BusinessRequest**
   - Typ: "ProductPhoto"
   - Přiložené soubory
   - Vybraná WorkflowTemplate: "E-shop Product Processing v2"

2. **Spuštění RequestExecution**
   - Vytvoří se instance ToolChainOrchestrator
   - Nakonfiguruje se podle WorkflowTemplate

3. **Zpracování WorkflowSteps**
   ```
   Step 1: ObjectDetectionTool (parallel)
   Step 2: ClassificationTool (parallel) 
   Step 3: OCRTool (parallel)
   Step 4: SegmentationTool (sequential)
   Step 5: InpaintingTool (sequential)
   Step 6: MetadataGeneratorOrchestrator
   Step 7: EshopUploadTool
   ```

4. **Sledování průběhu**
   - Každý krok vytvoří StepExecution
   - Tools automaticky vytvoří ToolExecution
   - Vše propojené přes ID

### 3. Využití existujících komponent

#### Tools zůstávají beze změny
- Implementují ITool
- Automatická registrace
- Žádné změny potřeba

#### Orchestrators - malé rozšíření
```csharp
public class BusinessOrchestrator<TRequest, TResponse> : BaseOrchestrator<TRequest, TResponse>
{
    private readonly Guid _businessRequestId;
    private readonly Guid _requestExecutionId;
    
    // Override pro logování do business vrstvy
    protected override async Task OnStepCompleted(string stepName, object result)
    {
        await base.OnStepCompleted(stepName, result);
        
        // Uložit StepExecution
        await _stepExecutionService.LogStepCompletion(
            _requestExecutionId, 
            stepName, 
            result
        );
    }
}
```

#### Conversations - propojení s požadavky
```csharp
public class Conversation : BaseEntity 
{
    // Existing properties...
    
    // Nové propojení
    public Guid? BusinessRequestId { get; set; }
    public BusinessRequest BusinessRequest { get; set; }
}
```

### 4. UI Workflow

#### Dashboard požadavků
```
┌─────────────────────────────────────────┐
│         📊 PŘEHLED POŽADAVKŮ            │
├─────────────────────────────────────────┤
│                                         │
│ 🔄 Aktivní: 12  ⏳ Ve frontě: 45      │
│ ✅ Dokončené dnes: 127                 │
│                                         │
│ Nejnovější požadavky:                  │
│ ┌─────────────────────────────────┐    │
│ │ REQ-2024-089 | Produktové foto  │    │
│ │ Klient: Fashion Store           │    │
│ │ Status: ████████░░ 73%          │    │
│ │ [Zobrazit] [Pause] [Cancel]     │    │
│ └─────────────────────────────────┘    │
│                                         │
│ [➕ Nový požadavek] [📋 Šablony]       │
└─────────────────────────────────────────┘
```

#### Detail požadavku - využívá existující komponenty
```
┌─────────────────────────────────────────┐
│    📋 REQ-2024-089 - Produktové foto   │
├─────────────────────────────────────────┤
│                                         │
│ 📊 Workflow: E-shop Product v2          │
│                                         │
│ Aktuální krok: Inpainting (5/7)        │
│                                         │
│ ┌─── TECHNICKÉ DETAILY ──────────┐     │
│ │ Orchestrator: ToolChain #4521   │     │
│ │ Conversation: #1234 (pokud chat)│     │
│ │                                 │     │
│ │ Tool Executions:                │     │
│ │ ✅ ObjectDetection - 1.2s       │     │
│ │ ✅ Classification - 0.8s        │     │
│ │ ✅ OCR - 0.5s                   │     │
│ │ ✅ Segmentation - 2.1s          │     │
│ │ 🔄 Inpainting - běží...         │     │
│ └─────────────────────────────────┘     │
│                                         │
│ [Zobrazit orchestrator] [Logy] [Chat]  │
└─────────────────────────────────────────┘
```

### 5. Implementační plán

#### Fáze 1: Business Entity (1 týden)
1. Vytvořit entity v OAI.Core/Entities/Business/
2. Přidat DbSets do AppDbContext
3. Vytvořit migrace
4. DTOs a mappery

#### Fáze 2: Services (1 týden)
1. BusinessRequestService
2. WorkflowTemplateService
3. RequestExecutionService
4. Integrace s existujícími orchestrátory

#### Fáze 3: Controllers & API (3-4 dny)
1. RequestsController
2. WorkflowsController
3. API endpoints pro CRUD operace

#### Fáze 4: UI (1 týden)
1. Views pro Requests (využít existující komponenty)
2. Workflow designer (může využít Projects UI)
3. Real-time monitoring (rozšířit MonitoringHub)

### 6. Výhody tohoto přístupu

1. **Žádné změny v Tools** - fungují jak jsou
2. **Minimální změny v Orchestrators** - jen přidáme logování
3. **Využití existujícího UI** - Chat, Monitoring, Tools
4. **Propojení business a technical** - průhledné sledování
5. **Škálovatelnost** - snadno přidat nové typy požadavků

### 7. Příklad konfigurace WorkflowTemplate

```json
{
  "name": "E-shop Product Processing v2",
  "requestType": "ProductPhoto",
  "configuration": {
    "orchestratorType": "ToolChain",
    "steps": [
      {
        "group": 1,
        "parallel": true,
        "tools": [
          {
            "id": "object_detection",
            "parameters": {
              "image": "@request.files[0]"
            }
          },
          {
            "id": "image_classifier",
            "parameters": {
              "image": "@request.files[0]"
            }
          }
        ]
      },
      {
        "group": 2,
        "parallel": false,
        "tools": [
          {
            "id": "segmentation",
            "parameters": {
              "image": "@request.files[0]",
              "boxes": "@step[1].object_detection.output.boxes"
            }
          }
        ]
      }
    ]
  }
}
```

## Závěr

Tento přístup maximálně využívá existující funkční komponenty a přidává pouze business vrstvu pro správu požadavků. Všechny Tools, Orchestrators a UI komponenty zůstávají funkční a jsou pouze propojené s novou business vrstvou.