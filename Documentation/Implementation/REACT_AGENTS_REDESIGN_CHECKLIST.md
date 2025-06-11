# 📋 REACT AGENTS REDESIGN CHECKLIST

## ✅ **FÁZE 1: ReAct Usage Simplifikace (1.5 dne)**

### **1.1 ReActDecisionEngine Implementation**

#### ☐ Core Decision Engine
- [ ] Vytvořit `OAI.ServiceLayer/Services/Orchestration/ReActDecisionEngine.cs`
  ```csharp
  public class ReActDecisionEngine
  {
      public bool ShouldUseReAct(string userMessage, WorkflowStage stage, WorkflowContext context);
      private bool IsComplexWorkflow(string workflowType);
      private bool RequiresComplexAnalysis(string userMessage, WorkflowStage stage);
      private bool RequiresReasoning(string userMessage, WorkflowStage stage);
  }
  ```

#### ☐ Workflow-Level ReAct Logic
- [ ] Implementovat `IsComplexWorkflow(string workflowType)`:
  ```csharp
  return workflowType switch
  {
      "ecommerce_product_search" => true,  // Multi-step reasoning needed
      "content_creation" => true,          // Planning and creativity
      "chatbot_conversation" => true,      // Interactive reasoning
      "image_generation" => false,         // Mostly deterministic
      "data_analysis" => false,           // Mostly computational
      _ => false
  };
  ```

#### ☐ Stage-Level ReAct Logic
- [ ] Implementovat `RequiresComplexAnalysis()` pro Analysis stages:
  - Keywords: "compare", "analyze", "evaluate", "assess"
  - Multiple data sources required
  - Ambiguous user input

- [ ] Implementovat `RequiresReasoning()` pro Decision stages:
  - Multiple options to choose from
  - Conditional logic required
  - User preferences need interpretation

#### ☐ Message Complexity Analysis
- [ ] Vylepšit complexity detection logic:
  ```csharp
  private bool IsComplexQuery(string message)
  {
      var complexityIndicators = new[]
      {
          "compare", "analyze", "find and", "step by step", "how to",
          "which is better", "recommend", "suggest", "explain why"
      };
      
      var multiStepIndicators = new[]
      {
          "then", "after that", "next", "finally", "also"
      };
      
      var ambiguityIndicators = new[]
      {
          "maybe", "possibly", "might", "could", "similar"
      };
      
      // Score based complexity assessment
      return CalculateComplexityScore(message, complexityIndicators, multiStepIndicators, ambiguityIndicators) > threshold;
  }
  ```

### **1.2 ConversationReActAgent Optimization**

#### ☐ Caching Implementation
- [ ] Přidat `IMemoryCache` dependency do `ConversationReActAgent`
- [ ] Implementovat reasoning pattern caching:
  ```csharp
  private readonly IMemoryCache _reasoningCache;
  
  protected override async Task<AgentThought> GenerateThoughtAsync(string observation)
  {
      var cacheKey = GenerateCacheKey(observation);
      if (_reasoningCache.TryGetValue(cacheKey, out AgentThought cachedThought))
      {
          _logger.LogDebug("Using cached reasoning for key: {CacheKey}", cacheKey);
          return cachedThought;
      }
      
      var thought = await base.GenerateThoughtAsync(observation);
      
      // Cache successful reasoning patterns
      if (thought.IsSuccessful)
      {
          _reasoningCache.Set(cacheKey, thought, TimeSpan.FromHours(1));
      }
      
      return thought;
  }
  ```

#### ☐ Early Termination Logic
- [ ] Implementovat early termination pro obvious cases:
  ```csharp
  protected override async Task<bool> ShouldContinueReasoningAsync(AgentThought thought, string observation)
  {
      // Terminate early for simple factual answers
      if (IsSimpleFactualAnswer(thought.Content))
      {
          return false;
      }
      
      // Terminate if confidence is very high
      if (thought.Confidence > 0.95)
      {
          return false;
      }
      
      return await base.ShouldContinueReasoningAsync(thought, observation);
  }
  ```

#### ☐ Parallel Tool Execution
- [ ] Implementovat parallel tool execution kde možné:
  ```csharp
  protected override async Task<AgentAction[]> GenerateActionsAsync(AgentThought thought)
  {
      var actions = await base.GenerateActionsAsync(thought);
      
      // Group independent actions for parallel execution
      var parallelGroups = GroupActionsForParallelExecution(actions);
      
      return parallelGroups.SelectMany(group => group).ToArray();
  }
  ```

#### ☐ Timeout Optimization
- [ ] Přidat adaptive timeout based on query complexity:
  ```csharp
  protected override TimeSpan GetExecutionTimeout(string userMessage)
  {
      var complexityScore = CalculateComplexityScore(userMessage);
      
      return complexityScore switch
      {
          < 0.3 => TimeSpan.FromSeconds(30),  // Simple queries
          < 0.7 => TimeSpan.FromSeconds(60),  // Medium complexity
          _ => TimeSpan.FromSeconds(120)      // Complex queries
      };
  }
  ```

### **1.3 Integration s Orchestrátory**

#### ☐ ConversationOrchestrator Integration
- [ ] Přidat `ReActDecisionEngine` dependency do `ConversationOrchestrator`
- [ ] Upravit ReAct triggering logic:
  ```csharp
  public override async Task<IOrchestratorResult> ExecuteAsync(ConversationOrchestratorRequestDto request, IOrchestratorContext context)
  {
      var shouldUseReAct = _reActDecisionEngine.ShouldUseReAct(
          request.Message, 
          context.CurrentStage, 
          context.WorkflowContext
      );
      
      if (shouldUseReAct)
      {
          return await ExecuteWithReActAsync(request, context);
      }
      else
      {
          return await ExecuteDirectToolCallAsync(request, context);
      }
  }
  ```

#### ☐ ProjectStageOrchestrator Integration
- [ ] Upravit `ProjectStageOrchestrator` pro workflow-level ReAct decisions
- [ ] Remove hardcoded ReAct enabling z stage configuration:
  ```csharp
  private async Task<StageExecutionResult> ExecuteStageAsync(ProjectStage stage, ProjectStageRequest request)
  {
      var useReAct = _reActDecisionEngine.ShouldUseReAct(
          request.UserMessage, 
          stage, 
          request.WorkflowContext
      );
      
      if (useReAct)
      {
          return await ExecuteStageWithReActAsync(stage, request);
      }
      else
      {
          return await ExecuteStageDirectlyAsync(stage, request);
      }
  }
  ```

#### ☐ Service Registration
- [ ] Registrovat `ReActDecisionEngine` do DI container jako Singleton
- [ ] Update service registrations pro modified orchestrátory

## ✅ **FÁZE 2: UI Simplifikace (0.5 dne)**

### **2.1 CreateStage Formulář Update**

#### ☐ Remove ReAct Agent Selector
- [ ] Odstranit ReAct agent dropdown z `Views/Projects/WorkflowDesigner/_CreateStage.cshtml`
- [ ] Přidat simple checkbox místo dropdown:
  ```html
  <div class="form-group">
      <label>
          Vyžaduje reasoning/rozhodování?
          <i class="fas fa-question-circle" data-toggle="tooltip" 
             title="Zapněte pouze pro kroky, které potřebují analyzovat data nebo se rozhodovat"></i>
      </label>
      <div class="form-check">
          <input type="checkbox" id="useReActMode" class="form-check-input">
          <label for="useReActMode" class="form-check-label">
              Povolit AI reasoning pro tento krok
          </label>
      </div>
      <small class="form-text text-muted">
          <strong>Zapněte pro:</strong> Analýzu dat, rozhodování, hodnocení kvality<br>
          <strong>Nechte vypnuté pro:</strong> Získávání dat, transformace, jednoduché API volání
      </small>
  </div>
  ```

#### ☐ Update Help Text
- [ ] Přidat detailed examples kdy zapnout/nezapnout ReAct:
  ```html
  <div class="alert alert-info mt-2">
      <h6><i class="fas fa-lightbulb"></i> Kdy použít reasoning:</h6>
      <div class="row">
          <div class="col-md-6">
              <strong class="text-success">✅ Zapněte pro:</strong>
              <ul class="small mb-0">
                  <li>Analýzu a porovnání dat</li>
                  <li>Rozhodování mezi možnostmi</li>
                  <li>Hodnocení kvality výsledků</li>
                  <li>Interpretaci nejednoznačných dat</li>
              </ul>
          </div>
          <div class="col-md-6">
              <strong class="text-danger">❌ Nechte vypnuté pro:</strong>
              <ul class="small mb-0">
                  <li>Jednoduché API volání</li>
                  <li>Transformace dat</li>
                  <li>Export/import souborů</li>
                  <li>Deterministické operace</li>
              </ul>
          </div>
      </div>
  </div>
  ```

#### ☐ JavaScript Updates
- [ ] Update JavaScript pro handling checkbox místo dropdown
- [ ] Remove `updateReactAgentHelp()` function
- [ ] Update form submission logic pro boolean useReAct field
- [ ] Update template application logic

### **2.2 Workflow Templates Update**

#### ☐ Template Structure Changes
- [ ] Remove `reactAgent` field z JavaScript templates
- [ ] Replace s `useReAct` boolean field:
  ```javascript
  const workflowTemplates = {
      'ecommerce_product_search': {
          stages: [
              {
                  name: 'Analýza vstupních fotek',
                  type: 'Analysis',
                  useReAct: true,  // Analysis requires reasoning
                  tools: ['image_analyzer']
              },
              {
                  name: 'Vyhledávání produktů',
                  type: 'Search', 
                  useReAct: false, // Simple search execution
                  tools: ['aliexpress_search', 'web_search']
              }
          ]
      }
  };
  ```

#### ☐ Template Application Logic
- [ ] Update `fillStageTemplate()` function:
  ```javascript
  function fillStageTemplate(templateType) {
      const template = templates[templateType];
      if (template) {
          $('#stageName').val(template.name);
          $('#stageType').val(template.type);
          $('#useReActMode').prop('checked', template.useReAct || false);
          // ... rest of template application
      }
  }
  ```

### **2.3 Project Configuration UI**

#### ☐ Project-Level ReAct Settings
- [ ] Přidat workflow-level ReAct configuration do project settings
- [ ] Add default ReAct behavior setting per workflow type
- [ ] Možnost override automatic ReAct decisions

## ✅ **FÁZE 3: Advanced Optimizations (1 den)**

### **3.1 ReAct Performance Monitoring**

#### ☐ Metrics Collection
- [ ] Implementovat ReAct performance metrics:
  ```csharp
  public class ReActMetrics
  {
      public TimeSpan ExecutionTime { get; set; }
      public int IterationCount { get; set; }
      public int ToolCallsCount { get; set; }
      public double SuccessRate { get; set; }
      public string ComplexityLevel { get; set; }
  }
  ```

#### ☐ Analytics Dashboard
- [ ] Přidat ReAct usage analytics do monitoring dashboard
- [ ] Track kdy ReAct adds value vs overhead
- [ ] Monitor cache hit rates

### **3.2 Smart ReAct Triggering**

#### ☐ Machine Learning Enhancement
- [ ] Implement ML model pro better complexity detection (future)
- [ ] Training data collection z user interactions
- [ ] A/B testing framework pro ReAct decisions

#### ☐ User Preference Learning
- [ ] Track user satisfaction s ReAct vs direct tool execution
- [ ] Personalized ReAct triggering based on user behavior
- [ ] Feedback loop pro improving decision engine

## ✅ **TESTING & VALIDATION**

### **Unit Tests**
- [ ] Test `ReActDecisionEngine` logic pro různé scenarios:
  - [ ] Complex vs simple queries
  - [ ] Different workflow types
  - [ ] Various stage types
  - [ ] Edge cases a boundary conditions

- [ ] Test `ConversationReActAgent` optimizations:
  - [ ] Caching functionality
  - [ ] Early termination logic
  - [ ] Timeout handling
  - [ ] Parallel tool execution

### **Integration Tests**
- [ ] End-to-end test ReAct enabling/disabling napříč different workflow types
- [ ] Test ReAct decision engine integration s orchestrátory
- [ ] Test UI ReAct checkbox functionality
- [ ] Performance tests pro cached vs non-cached reasoning

### **User Acceptance Tests**
- [ ] Test user experience s simplified ReAct interface
- [ ] Verify ReAct decisions match user expectations
- [ ] Test tooltip and help text clarity
- [ ] Measure task completion time improvement

### **Performance Tests**
- [ ] Benchmark ReAct execution time s optimizations
- [ ] Test caching effectiveness (hit rate, memory usage)
- [ ] Monitor overall workflow performance impact
- [ ] Compare reasoning quality with/without optimizations

## ✅ **MIGRATION & DEPLOYMENT**

### **Database Migration**
- [ ] Update ProjectStage entity - replace `ReActAgentType` string s `UseReAct` boolean
- [ ] Create migration script pro converting existing ReAct configurations
- [ ] Preserve existing ReAct settings kde meaningful

### **Configuration Migration**
- [ ] Convert existing ReAct agent configurations
- [ ] Set default `UseReAct` values based on stage types:
  - Analysis stages: true
  - Decision stages: true  
  - Search/Transform stages: false
  - Export stages: false

### **Backward Compatibility**
- [ ] Ensure API endpoints remain compatible
- [ ] Support legacy ReAct agent specifications (deprecated)
- [ ] Graceful fallback pro missing ReAct configurations

## 📝 **IMPLEMENTAČNÍ POZNÁMKY**

### **Dependencies**
- Fáze 2 vyžaduje completion of Fáze 1.1
- Fáze 3 je optional a může být implemented později
- Testing může běžet paralelně s implementation

### **Performance Impact**
- ReAct decision engine adds minimal overhead (< 10ms)
- Caching reduces repeated reasoning time by 70-90%
- Early termination reduces average reasoning time by 30-50%
- Overall workflow performance improvement: 20-40%

### **Breaking Changes**
- UI changes are non-breaking (legacy support maintained)
- API changes are additive only
- Database migration is backward compatible

### **Monitoring Requirements**
- Track ReAct usage patterns
- Monitor cache performance
- Measure user satisfaction
- Alert on ReAct failures

### **Definition of Done**
- [ ] All checklist items completed
- [ ] Performance metrics improved
- [ ] User experience simplified
- [ ] No regression in ReAct quality
- [ ] Documentation updated
- [ ] Monitoring dashboards configured