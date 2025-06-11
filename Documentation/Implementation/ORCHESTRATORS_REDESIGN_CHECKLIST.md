# 📋 ORCHESTRATORS REDESIGN CHECKLIST

## ✅ **FÁZE 1: Workflow-Level Orchestrátory (3 dny)**

### **1.1 Base Infrastruktura**

#### ☐ Interface a Base Classes
- [ ] Vytvořit `OAI.Core/Interfaces/Orchestration/IWorkflowOrchestrator.cs`
  ```csharp
  public interface IWorkflowOrchestrator<TRequest, TResponse> : IOrchestrator<TRequest, TResponse>
  {
      string WorkflowType { get; }
      Task<WorkflowValidationResult> ValidateWorkflowAsync(TRequest request);
      Task<WorkflowContext> InitializeContextAsync(TRequest request);
      Task<List<WorkflowStage>> GetStagesAsync(TRequest request);
  }
  ```

- [ ] Vytvořit `OAI.ServiceLayer/Services/Orchestration/Base/BaseWorkflowOrchestrator.cs`
  ```csharp
  public abstract class BaseWorkflowOrchestrator<TRequest, TResponse> : BaseOrchestrator, IWorkflowOrchestrator<TRequest, TResponse>
  {
      protected abstract Task<List<WorkflowStage>> DefineStagesAsync(TRequest request);
      protected abstract Task<TResponse> ExecuteWorkflowAsync(List<WorkflowStage> stages, WorkflowContext context);
  }
  ```

#### ☐ Supporting Classes
- [ ] Vytvořit `OAI.Core/Entities/Orchestration/WorkflowContext.cs`
  - Properties: WorkflowId, ProjectId, UserId, SharedData, ExecutionMetrics
  - Methods: SetData<T>, GetData<T>, AddMetric

- [ ] Vytvořit `OAI.Core/Entities/Orchestration/WorkflowStage.cs`
  - Properties: Name, Description, Tools, ExecutionStrategy, UseReAct, Order, Configuration
  - Methods: AddTool, RemoveTool, ValidateConfiguration

- [ ] Vytvořit `OAI.Core/DTOs/Orchestration/WorkflowValidationResult.cs`
  - Properties: IsValid, Errors, Warnings, Suggestions

#### ☐ Service Registration
- [ ] Přidat workflow orchestrátory do automatic service discovery
- [ ] Vytvořit `IWorkflowOrchestratorFactory` interface
- [ ] Implementovat `WorkflowOrchestratorFactory` class

### **1.2 EcommerceWorkflowOrchestrator**

#### ☐ DTOs
- [ ] Vytvořit `OAI.Core/DTOs/Orchestration/EcommerceWorkflowRequest.cs`
  - Properties: ProjectId, CustomerPhotos, SearchParameters, ExportFormat
  
- [ ] Vytvořit `OAI.Core/DTOs/Orchestration/EcommerceWorkflowResponse.cs`
  - Properties: AnalysisResults, ProductMatches, ExportedFiles, ExecutionSummary

#### ☐ Implementation
- [ ] Vytvořit `OAI.ServiceLayer/Services/Orchestration/Implementations/EcommerceWorkflowOrchestrator.cs`
- [ ] Implementovat `DefineStagesAsync()`:
  ```csharp
  return new List<WorkflowStage>
  {
      new("image_analysis", "Analýza vstupních fotek", 
          tools: ["image_analyzer"], 
          strategy: ExecutionStrategy.Sequential),
      new("product_search", "Vyhledávání produktů", 
          tools: ["aliexpress_search", "web_search"], 
          strategy: ExecutionStrategy.Parallel),
      new("result_processing", "Zpracování výsledků", 
          tools: ["product_filter", "similarity_scorer"], 
          strategy: ExecutionStrategy.Sequential),
      new("data_export", "Export dat", 
          tools: ["excel_exporter"], 
          strategy: ExecutionStrategy.Sequential)
  };
  ```

- [ ] Implementovat `ExecuteWorkflowAsync()` s context passing
- [ ] Přidat error handling a retry logic
- [ ] Přidat structured logging pro každou stage

#### ☐ Testing
- [ ] Unit tests pro EcommerceWorkflowOrchestrator
- [ ] Integration test s mock tools
- [ ] Validation test pro request/response DTOs

### **1.3 Další Workflow Orchestrátory**

#### ☐ ImageGenerationWorkflowOrchestrator
- [ ] Vytvořit DTOs: `ImageGenerationRequest`, `ImageGenerationResponse`
- [ ] Implementovat stages:
  - `prompt_refinement` (useReAct: true, tools: ["prompt_enhancer"])
  - `image_generation` (strategy: Conditional, tools: ["dalle_generator", "midjourney_api"])
  - `post_processing` (strategy: Sequential, tools: ["image_upscaler", "background_remover"])

#### ☐ ContentCreationWorkflowOrchestrator
- [ ] Vytvořit DTOs: `ContentCreationRequest`, `ContentCreationResponse`
- [ ] Implementovat stages:
  - `research` (strategy: Parallel, tools: ["web_search", "academic_search"])
  - `planning` (useReAct: true, tools: ["outline_generator"])
  - `writing` (strategy: Sequential, tools: ["content_writer", "grammar_checker"])
  - `review` (useReAct: true, tools: ["quality_assessor"])

#### ☐ DataAnalysisWorkflowOrchestrator
- [ ] Vytvořit DTOs: `DataAnalysisRequest`, `DataAnalysisResponse`
- [ ] Implementovat stages:
  - `data_ingestion` (strategy: Parallel, tools: ["csv_reader", "api_fetcher"])
  - `processing` (strategy: Sequential, tools: ["data_cleaner", "transformer"])
  - `visualization` (strategy: Sequential, tools: ["chart_generator", "dashboard_creator"])
  - `reporting` (strategy: Sequential, tools: ["report_generator"])

#### ☐ ChatbotWorkflowOrchestrator
- [ ] Vytvořit DTOs: `ChatbotRequest`, `ChatbotResponse`
- [ ] Implementovat single stage s ConversationOrchestrator a useReAct: true

## ✅ **FÁZE 2: ProjectStageOrchestrator Refaktoring (1 den)**

### **2.1 Delegation Layer**

#### ☐ Core Refaktoring
- [ ] Přidat `IWorkflowOrchestratorFactory` dependency do `ProjectStageOrchestrator`
- [ ] Implementovat `DetermineWorkflowType(Project project)` method:
  ```csharp
  private string DetermineWorkflowType(Project project)
  {
      // Analyze project description, tools, or explicit configuration
      if (project.WorkflowType != null) return project.WorkflowType;
      
      // Auto-detection logic based on project characteristics
      var keywords = project.Description?.ToLower() ?? "";
      if (keywords.Contains("product") && keywords.Contains("search")) return "ecommerce_product_search";
      if (keywords.Contains("image") && keywords.Contains("generat")) return "image_generation";
      // ... more detection logic
      
      return "custom"; // fallback
  }
  ```

- [ ] Implementovat `GetWorkflowOrchestrator(string workflowType)` method
- [ ] Upravit `ExecuteAsync()` pro delegaci nebo fallback na legacy logic
- [ ] Zachovat zpětnou kompatibilitu s existujícími projekty

#### ☐ Database Changes
- [ ] Přidat `WorkflowType` property do `Project` entity
- [ ] Vytvořit migration pro přidání WorkflowType column
- [ ] Update `ProjectDto` s WorkflowType property
- [ ] Update mapping mezi Project entity a DTO

### **2.2 Migration Strategy**
- [ ] Implementovat auto-detection workflow type pro existující projekty
- [ ] Vytvořit migration script pro populaci WorkflowType z project descriptions
- [ ] Add validation pro workflow type values

## ✅ **FÁZE 3: UI Redesign (2 dny)**

### **3.1 Workflow Designer Hlavní Stránka**

#### ☐ Workflow Type Selector
- [ ] Přidat workflow type selector na top of `Views/Projects/WorkflowDesigner/Index.cshtml`:
  ```html
  <div class="form-group">
      <label>Typ workflow</label>
      <select id="workflowType" class="form-control form-control-lg">
          <option value="ecommerce_product_search">🛒 E-commerce vyhledávání produktů</option>
          <option value="image_generation">🎨 Generování a úprava obrázků</option>
          <option value="content_creation">📝 Tvorba obsahu</option>
          <option value="data_analysis">📊 Analýza dat</option>
          <option value="chatbot_conversation">💬 Chatbot konverzace</option>
          <option value="custom">⚙️ Vlastní workflow</option>
      </select>
  </div>
  ```

#### ☐ Auto-populate Logic
- [ ] Implementovat JavaScript `onWorkflowTypeChange()` function
- [ ] Auto-populate stages podle vybraného workflow orchestrátoru
- [ ] Update `applyTemplate()` function pro workflow-level approach
- [ ] Skrýt orchestrator selector z individual stages

### **3.2 CreateStage Formulář Update**

#### ☐ Simplifikace Formuláře
- [ ] Odstranit "Orchestrátor" field z `Views/Projects/WorkflowDesigner/_CreateStage.cshtml`
- [ ] Zachovat pouze: název, popis, typ, execution strategy, nástroje
- [ ] Update tooltips a help text pro novou architekturu
- [ ] Remove orchestrator-related JavaScript functions

#### ☐ Enhanced Stage Configuration
- [ ] Vylepšit tools selector s drag & drop functionality
- [ ] Přidat preview selected tools před created
- [ ] Add validation pro required tools based on stage type

### **3.3 Workflow Templates Update**

#### ☐ JavaScript Templates
- [ ] Update workflow templates v `Views/Projects/WorkflowDesigner/Index.cshtml`
- [ ] Remove orchestrator selection z stage templates
- [ ] Update template structure pro workflow-level orchestrátory:
  ```javascript
  const workflowTemplates = {
      'ecommerce_product_search': {
          workflowType: 'ecommerce_product_search',
          name: 'E-commerce vyhledávání produktů',
          stages: [
              { name: 'Analýza fotek', type: 'Analysis', strategy: 'Sequential', tools: ['image_analyzer'] },
              // ... další stages
          ]
      }
  };
  ```

#### ☐ Template Application Logic
- [ ] Update `applyTemplate()` function pro nový structure
- [ ] Add workflow type setting při application template
- [ ] Remove orchestrator setting z template logic

## ✅ **TESTING & VALIDATION**

### **Unit Tests**
- [ ] Test každý workflow orchestrátor independently
- [ ] Test WorkflowOrchestratorFactory selection logic
- [ ] Test backward compatibility s legacy ProjectStageOrchestrator
- [ ] Test workflow type auto-detection algorithm

### **Integration Tests**
- [ ] End-to-end test pro EcommerceWorkflow s real/mock tools
- [ ] Test workflow orchestrator switching (legacy vs new)
- [ ] Test UI workflow creation s novými orchestrátory
- [ ] Test migration script pro existing projects

### **Performance Tests**
- [ ] Benchmark workflow execution speed (new vs old architecture)
- [ ] Monitor memory usage s workflow context sharing
- [ ] Test parallel tool execution performance
- [ ] Measure UI responsiveness s novým design

## 📝 **IMPLEMENTAČNÍ POZNÁMKY**

### **Dependencies**
- Fáze 2 vyžaduje dokončení Fáze 1.1 a 1.2
- Fáze 3 vyžaduje dokončení Fáze 2.1
- Testing může běžet paralelně s implementací

### **Breaking Changes**
- Žádné breaking changes pro existing API endpoints
- UI changes jsou opt-in (legacy mode zůstává funkční)
- Database migration je backward compatible

### **Performance Considerations**
- Workflow context sharing reduces redundant data passing
- Parallel tool execution v stages
- Caching workflow orchestrator instances
- Lazy loading workflow configurations

### **Definition of Done**
- [ ] Všechny checklist items completed
- [ ] Code compiles bez warnings  
- [ ] Basic functionality tested manually
- [ ] No breaking changes pro existing workflows
- [ ] Documentation updated
- [ ] Git commit s descriptive message