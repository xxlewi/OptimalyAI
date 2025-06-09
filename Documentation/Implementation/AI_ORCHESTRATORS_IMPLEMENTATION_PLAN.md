# AI Orchestrators Implementation Plan

## 🎯 Cíl
Vytvořit modulární systém AI orchestrátorů, který umožní inteligentní koordinaci mezi AI modely (Ollama) a AI Tools, včetně automatického rozhodování kdy použít jaký nástroj.

## 📊 Aktuální Stav *(09.06.2025)*

### ✅ Dokončeno:
- **Base Infrastructure** - Kompletní orchestrátor infrastruktura
- **Conversation Orchestrator** - Inteligentní chat s automatickou tool detekcí
- **UI Integration** - Tool usage indikátory v chatu, real-time SignalR events
- **Tool Chain Orchestrator** - Pokročilé řetězení nástrojů se 3 execution strategiemi
- **ReAct Infrastructure** - Připravená infrastruktura pro ReAct pattern

### 🎯 Aktuální Priorita:
**ReAct Agent Implementation** - Využití připravené infrastruktury pro reasoning

### 📈 Statistiky:
- **3 aktivní orchestrátory** v systému
- **2 AI nástroje** registrované (Web Search, LLM Tornado)
- **100% build úspěšnost**
- **UI plně funkční** s real-time monitoring

## 📋 Implementation Checklist

### 1. Base Infrastructure (Základní infrastruktura)

#### Interfaces (OAI.Core/Interfaces/Orchestration/)
- [x] `IOrchestrator<TRequest, TResponse>` - Základní interface pro všechny orchestrátory
- [x] `IOrchestratorContext` - Kontext pro předávání informací mezi orchestrátory
- [x] `IOrchestratorStrategy` - Interface pro různé strategie vykonávání
- [x] `IOrchestratorMetrics` - Interface pro metriky a monitoring
- [x] `IOrchestratorResult` - Standardizovaný výsledek orchestrace

#### Base Classes (OAI.ServiceLayer/Services/Orchestration/Base/)
- [x] `BaseOrchestrator<TRequest, TResponse>` - Základní třída s common funkcionalitou
- [x] `OrchestratorContext` - Implementace kontextu
- [x] `OrchestratorResult` - Implementace výsledku
- [x] `OrchestratorException` - Specializované výjimky

#### DTOs (OAI.Core/DTOs/Orchestration/)
- [x] `OrchestratorRequestDto` - Základní request DTO
- [x] `OrchestratorResponseDto` - Základní response DTO
- [x] `ConversationOrchestratorRequestDto` - Pro konverzační orchestrátor
- [x] `ConversationOrchestratorResponseDto` - Response pro konverzační orchestrátor
- [x] `ToolChainOrchestratorRequestDto` - Pro řetězení nástrojů
- [x] `OrchestratorMetricsDto` - Pro metriky

### 2. Conversation Orchestrator (Priorita 1)

#### Implementation (OAI.ServiceLayer/Services/Orchestration/Implementations/)
- [x] `ConversationOrchestrator` - Hlavní implementace
  - [x] Integrace s `IConversationManager`
  - [x] Integrace s `IOllamaService`
  - [x] Integrace s `IToolExecutor`
  - [x] Tool detection logic (rozpoznání kdy použít nástroj)
  - [x] Context management mezi model calls a tool calls
  - [x] Streaming support pro real-time odpovědi

#### Tool Detection & Selection
- [x] `ToolSelectionStrategy` - Strategie pro výběr nástrojů ✅ IMPLEMENTOVÁNO
  - [x] Keyword-based detection (např. "vyhledej", "najdi", "search") ✅
  - [x] Intent classification ✅ Regex patterns pro různé nástroje
  - [ ] Model-guided selection (nechat model rozhodnout)
- [x] `ToolParameterExtractor` - Extrakce parametrů z textu ✅ IMPLEMENTOVÁNO

#### Integration
- [x] Upravit `ChatController` pro použití `ConversationOrchestrator` ✅ **DOKONČENO**
- [x] Upravit `ChatHub` pro streaming výsledků z orchestrátoru ✅ **DOKONČENO**
  - [x] Integrace orchestrátoru do SendMessage metody ✅
  - [x] Vytvoření orchestrator request/context ✅
  - [x] Zpracování orchestrator response ✅
  - [x] Simulace streamingu pro odpověď ✅
- [x] Přidat orchestrator status do SignalR messages ✅ **DOKONČENO**
  - [x] ToolsUsed event při použití nástrojů ✅

### 3. UI Integration (Priorita 2) ✅ **DOKONČENO**

#### Chat UI Updates
- [x] **Tool Usage Indicators** - zobrazení když AI používá nástroje ✅
  - [x] Přidat handler pro ToolsUsed event v chat JavaScript ✅
  - [x] Vizuální indikátor během tool execution ✅
  - [x] Zobrazení tool výsledků inline v konverzaci ✅
- [x] **Enhanced Message Display** ✅
  - [x] Ikona nástroje u zpráv které použily tools ✅
  - [x] Collapsible sekce pro tool metadata ✅
  - [x] Progress indikátor během tool execution ✅
  - [x] Real-time SignalR events (ToolExecutionStarted, ToolExecutionCompleted) ✅
  - [x] Tool confidence badges ✅
  - [x] Tool execution progress animation ✅

### 4. Tool Chain Orchestrator ✅ **DOKONČENO** *(09.06.2025)*

#### Implementation ✅ **KOMPLETNÍ**
- [x] `ToolChainOrchestrator` - Řetězení nástrojů ✅
  - [x] Sequential execution strategy ✅
  - [x] Parallel execution strategy ✅
  - [x] Conditional execution strategy ✅
  - [x] Error handling a retry logic ✅
  - [x] ReAct pattern preparation ✅
  - [x] **DI registrace a UI integrace** ✅
  - [x] **Validace a health checks** ✅
  - [x] **Zobrazení v /Orchestrators stránce** ✅

#### Strategies (OAI.ServiceLayer/Services/Orchestration/Strategies/) ✅ **KOMPLETNÍ**
- [x] `IExecutionStrategy` - Interface pro strategie ✅
- [x] `SequentialExecutionStrategy` - Postupné vykonávání ✅
- [x] `ParallelExecutionStrategy` - Paralelní vykonávání ✅
- [x] `ConditionalExecutionStrategy` - Podmíněné vykonávání ✅
- [x] Parameter mapping between steps ✅
- [x] Dependency resolution ✅
- [x] Reasoning step tracking (for ReAct) ✅
- [x] **Oprava IToolResult.IsSuccess property usage** ✅
- [x] **Správná ToolExecutionContext integrace** ✅

#### Controller & UI Integration ✅ **KOMPLETNÍ**
- [x] **OrchestratorsController oprava** - resolving orchestrátorů přes interfaces ✅
- [x] **GetOrchestratorInstance metoda** - správné DI resolution ✅
- [x] **UI zobrazení Tool Chain Orchestrátoru** v management stránce ✅
- [x] **Metrics a status zobrazení** ✅

### 5. ReAct Pattern Implementation 🎯 **NEXT PRIORITY**

#### Core Infrastructure ✅ **DOKONČENO**
- [x] `IReActAgent` - Main interface for ReAct agents ✅
- [x] `IReActRequest` - Request for ReAct execution ✅
- [x] `IReActResult` - Result with reasoning steps ✅
- [x] `IReActStep` - Single reasoning step (Thought → Action → Observation) ✅
- [x] `IReActAction` - Actions that agent can take ✅
- [x] Integration points in ToolChainOrchestrator ✅
- [x] **ConditionalExecutionStrategy** připravena pro ReAct ✅
- [x] **ReasoningStep tracking** v execution context ✅

#### Implementation (🚧 AKTUÁLNÍ ÚKOL)
- [ ] `ReActAgent` - Main implementation **← IMPLEMENTOVAT**
  - [ ] LLM-based reasoning (Thought generation) pomocí Ollama
  - [ ] Action selection and execution přes existing tools
  - [ ] Observation processing z tool results
  - [ ] Multi-step reasoning loops s max limit
- [ ] `ReActToolAdapter` - Adapter pro existing tools
- [ ] `ReActPromptTemplates` - Strukturované prompty pro reasoning
- [ ] `ReActOrchestrator` - Specializovaný orchestrátor pro ReAct pattern

#### Integration with Orchestrators
- [x] ToolChainOrchestrator prepared for ReAct ✅
- [ ] ConversationOrchestrator integration s ReAct
- [ ] ReAct-specific orchestrator pro pokročilé use cases

### 6. Project-Based Orchestrators 🎯 **HIGH PRIORITY**

#### Core Concept
**Klientské projekty** jako "Analyzátor poptávek na internetu" - každý projekt má vlastní orchestrátor

#### Implementation *(PŘIPRAVENO k implementaci)*
- [x] **Projects UI** - Management stránka `/Projects` ✅
- [x] **ProjectViewModel** - View modely pro projekty ✅
- [x] **Example projekt** - "Analyzátor poptávek na internetu" ✅
- [ ] `ProjectOrchestrator` - Base class pro projektové orchestrátory **← IMPLEMENTOVAT**
- [ ] `ProjectDefinition` - Model pro definici projektu
- [ ] `DemandAnalyzerOrchestrator` - Konkrétní implementace pro analyzátor poptávek
  - [ ] Automatic web scraping (Bazos, Facebook Marketplace)
  - [ ] AI analysis a learning
  - [ ] CRM integration

#### Project Examples
- [ ] **Analyzátor poptávek** - Auto scraping + AI analysis + CRM
- [ ] **Content Generator** - AI chain pro generování obsahu
- [ ] **Data Pipeline** - ETL processes s AI validation

### 7. Workflow Orchestrator

#### Implementation
- [ ] `WorkflowOrchestrator` - Komplexní workflows
  - [ ] Workflow definition DSL/model
  - [ ] Step execution engine
  - [ ] State management
  - [ ] Checkpointing a recovery

#### Models (OAI.Core/Entities/Orchestration/)
- [ ] `WorkflowDefinition` - Entity pro definici workflow
- [ ] `WorkflowStep` - Jednotlivé kroky
- [ ] `WorkflowExecution` - Historie vykonávání

### 5. Supporting Services

#### Monitoring & Metrics
- [x] `OrchestratorMetricsService` - Sbírání metrik ✅ KOMPLETNĚ IMPLEMENTOVÁNO
  - [x] Execution time tracking ✅
  - [x] Success/failure rates ✅
  - [x] Tool usage statistics ✅
  - [x] Real-time dashboard metrics ✅
  - [x] Orchestrator health monitoring ✅

#### Caching
- [ ] `OrchestratorCacheService` - Cache pro výsledky
  - [ ] Cache orchestration results
  - [ ] Cache tool results
  - [ ] Configurable TTL

#### Security
- [ ] `OrchestratorSecurityService` - Bezpečnost
  - [ ] User authorization pro orchestrátory
  - [ ] Tool execution permissions
  - [ ] Rate limiting per user

### 6. API & Controllers

#### API Controllers
- [x] `OrchestratorsController` - API pro orchestrátory ✅ KOMPLETNĚ IMPLEMENTOVÁNO
  - [x] GET /api/orchestrators - Seznam orchestrátorů ✅
  - [x] POST /api/orchestrators/conversation/execute - Spuštění konverzace ✅
  - [x] GET /api/orchestrators/{id} - Detail orchestrátoru ✅
  - [x] GET /api/orchestrators/metrics - Metriky ✅
  - [x] GET /api/orchestrators/dashboard - Dashboard data ✅

#### ViewModels
- [ ] `OrchestratorViewModel` - Pro UI
- [ ] `OrchestratorExecutionViewModel` - Pro zobrazení výsledků

### 7. UI Integration

#### Chat UI Updates (Views/Chat/)
- [ ] **Conversation.cshtml** - Rozšířit pro zobrazení orchestrace
  - [ ] Vizuální indikátor když AI používá nástroj
  - [ ] Zobrazení názvu a ikony použitého nástroje
  - [ ] Progress bar během vykonávání nástroje
  - [ ] Collapsible sekce pro tool výsledky
  - [ ] "AI is searching..." animace

#### Orchestrator Dashboard (Views/Orchestration/)
- [ ] **Dashboard.cshtml** - Hlavní přehled orchestrátorů
  - [ ] Seznam dostupných orchestrátorů
  - [ ] Real-time statistiky (počet běžících, úspěšnost)
  - [ ] Graf využití za posledních 24h
  - [ ] Top používané nástroje
  
- [ ] **History.cshtml** - Historie orchestrací
  - [ ] Tabulka všech orchestrací s filtry
  - [ ] Detail každé orchestrace (timeline view)
  - [ ] Export do CSV/JSON
  
- [ ] **Playground.cshtml** - Testování orchestrátorů
  - [ ] Formulář pro manuální spuštění
  - [ ] Live preview výsledků
  - [ ] JSON editor pro pokročilé uživatele

#### JavaScript Components (wwwroot/js/orchestration/)
- [ ] **orchestration-monitor.js**
  - [ ] SignalR connection pro real-time updates
  - [ ] Update UI když orchestrátor běží
  - [ ] Error handling a retry logic
  
- [ ] **tool-execution-visualizer.js**
  - [ ] Animovaný flow diagram pro tool chain
  - [ ] Zobrazení vstupu/výstupu každého nástroje
  - [ ] Execution timeline
  
- [ ] **chat-orchestration.js**
  - [ ] Integrace s existujícím chat.js
  - [ ] Rozpoznání orchestrator messages
  - [ ] Rendering tool results inline
  - [ ] Tlačítko "Show details" pro tool execution

#### UI Components & Styling
- [ ] **CSS (wwwroot/css/orchestration.css)**
  - [ ] Tool execution cards
  - [ ] Progress indicators
  - [ ] Status badges (running, success, failed)
  - [ ] Animated transitions
  
- [ ] **Partial Views**
  - [ ] `_ToolExecutionCard.cshtml` - Karta pro zobrazení tool execution
  - [ ] `_OrchestratorStatus.cshtml` - Status badge komponenta
  - [ ] `_ToolResultRenderer.cshtml` - Různé rendery pro různé typy výsledků

#### AdminLTE Integration
- [ ] **Sidebar Menu Update**
  - [ ] Přidat "AI Orchestration" sekci
  - [ ] Sub-menu: Dashboard, History, Playground
  - [ ] Badge s počtem běžících orchestrací
  
- [ ] **Widgets**
  - [ ] Info box pro orchestrator statistiky
  - [ ] Small box pro quick stats
  - [ ] Timeline widget pro execution history

#### User Experience Features
- [ ] **Real-time Notifications**
  - [ ] Toast notifikace když orchestrace začne/skončí
  - [ ] Sound notification option
  - [ ] Browser notifications (optional)
  
- [ ] **Interactive Elements**
  - [ ] Drag & drop pro workflow builder (budoucnost)
  - [ ] Click to expand tool results
  - [ ] Copy button pro výsledky
  - [ ] Re-run button pro opakování orchestrace
  
- [ ] **Responsive Design**
  - [ ] Mobile-friendly orchestrator dashboard
  - [ ] Touch-friendly controls
  - [ ] Optimalizace pro různé velikosti obrazovek

### 8. Configuration

#### appsettings.json
- [ ] Orchestrator configuration section
  ```json
  "Orchestration": {
    "ConversationOrchestrator": {
      "EnableToolDetection": true,
      "ToolDetectionKeywords": ["search", "find", "lookup", "vyhledej", "najdi"],
      "MaxToolsPerConversation": 10,
      "ToolExecutionTimeout": 30
    },
    "CacheSettings": {
      "Enabled": true,
      "TTLMinutes": 15
    }
  }
  ```

### 9. Testing

#### Unit Tests
- [ ] BaseOrchestrator tests
- [ ] ConversationOrchestrator tests
- [ ] ToolChainOrchestrator tests
- [ ] Strategy tests

#### Integration Tests
- [ ] End-to-end orchestration tests
- [ ] Tool integration tests
- [ ] Performance tests

### 10. Documentation

#### Code Documentation
- [ ] XML documentation pro všechny public členy
- [ ] README.md update s orchestrator sekcí
- [ ] CLAUDE.md update s orchestrator patterns

#### Examples
- [ ] Example: Chat with web search
- [ ] Example: Multi-tool workflow
- [ ] Example: Custom orchestrator

## 🏗️ Architektura

```
Orchestration System
├── Core Layer (OAI.Core)
│   ├── Interfaces/Orchestration/
│   ├── DTOs/Orchestration/
│   └── Entities/Orchestration/
├── Service Layer (OAI.ServiceLayer)
│   ├── Services/Orchestration/
│   │   ├── Base/
│   │   ├── Implementations/
│   │   └── Strategies/
│   └── Mapping/Orchestration/
└── Presentation Layer (OptimalyAI)
    ├── Controllers/
    ├── Views/Orchestration/
    └── wwwroot/js/orchestration/
```

## 🚀 Další kroky a Priority *(09.06.2025)*

### 🔥 IMMEDIATE NEXT (Vysoká priorita)
1. **ReAct Agent Implementation** 
   - Využít připravenou infrastrukturu v ToolChainOrchestrator
   - LLM-based reasoning pomocí Ollama
   - Integration s existing tools
   
2. **Project-Based Orchestrators**
   - ProjectOrchestrator base class
   - DemandAnalyzerOrchestrator implementace
   - Klientské projekty jako orchestrátory

### 📋 MEDIUM PRIORITY  
3. **Orchestrator Persistence** - Ukládání/načítání konfigurací
4. **Orchestrator Scheduling** - Plánované spouštění orchestrátorů
5. **Enhanced UI** - Dashboard pro orchestrátory, workflow visualizer

### 📊 LONG TERM
6. **Security Features** - Permissions, rate limiting
7. **Advanced Workflows** - Komplexní workflow engine
8. **Documentation** - Kompletní dokumentace a examples

## 🔄 Implementační postup

1. **Fáze 1**: Base Infrastructure + Conversation Orchestrator ✅ **DOKONČENO**
2. **Fáze 2**: Compilation fixes + DI setup + App startup ✅ **DOKONČENO**
3. **Fáze 3**: ChatController + ChatHub integration ✅ **DOKONČENO**
4. **Fáze 4**: UI Integration - Tool usage visualization ✅ **DOKONČENO**
5. **Fáze 5**: Tool Chain Orchestrator + Advanced Strategies ✅ **DOKONČENO** 
6. **Fáze 6**: ReAct Pattern + Project Orchestrators 🎯 **AKTUÁLNÍ**
7. **Fáze 7**: Advanced Features + Documentation

## 📈 Aktuální Progress

### ✅ Dokončeno (Fáze 1)
- **Base Infrastructure**: Všechny interfaces, base classes, DTOs, exceptions
- **ConversationOrchestrator**: Plná implementace s tool detection a selection
- **Metrics Service**: Kompletní monitoring a statistiky 
- **API Controllers**: REST endpoints pro orchestrátory
- **DI Registration**: Automatická registrace všech služeb
- **SimpleOllamaService**: Zjednodušený Ollama adapter pro orchestrátory

### ✅ Dokončeno (Fáze 5) - KOMPLETNÍ! 🚀 *(09.06.2025)*
- **ToolChainOrchestrator**: Kompletní implementace řetězení nástrojů ✅
- **Execution Strategies**: Sequential, Parallel, Conditional ✅
- **Parameter Mapping**: Předávání výstupů mezi kroky ✅
- **Dependency Resolution**: Automatické řešení závislostí ✅
- **ReAct Pattern Infrastructure**: Interfaces a základy pro ReAct ✅
- **Error Handling**: Robustní zpracování chyb a retry logic ✅
- **UI Integration**: Orchestrátor se zobrazuje v /Orchestrators ✅
- **DI Resolution**: Opraveno resolving orchestrátorů přes interfaces ✅
- **Controller Fixes**: GetOrchestratorInstance metoda ✅
- **Build Success**: 100% úspěšný build bez chyb ✅

### 🎯 Aktuální Fáze 6 - PRIORITIES
- **ReAct Agent**: Implementace reasoning engine 🚧
- **Project Orchestrators**: Klientské projekty jako orchestrátory 🚧
- **Persistence**: Ukládání orchestrátor konfigurací
- **Scheduling**: Automatické spouštění orchestrátorů

### ✅ Dokončeno (Fáze 2)
- **Compilation Fixes**: Všechny compilation errors opraveny ✅
- **Entity Updates**: Message entity rozšířena o UserId property ✅
- **Collection Types**: ReadOnly vs Mutable collections opraveny ✅
- **API Errors**: Dictionary conversion errors v OrchestratorsController opraveny ✅

### ✅ Dokončeno (Fáze 2) - HOTOVO! 🎉
- **Orchestrator Compilation**: Všechny compilation errors opraveny ✅
- **Application Startup**: Aplikace se úspěšně spouští ✅  
- **Tool Registration**: Web search tool je úspěšně registrován ✅
- **Background Services**: Metrics service a Tool Initializer fungují ✅
- **DI Registration**: Konflikt HttpClient registrace vyřešen ✅
- **Entity Updates**: Message entity rozšířena o UserId property ✅
- **Collection Types**: ReadOnly vs Mutable collections opraveny ✅
- **API Errors**: Dictionary conversion errors v OrchestratorsController opraveny ✅

### 🚀 Co je Ready k použití HNED
- **ConversationOrchestrator**: Plně funkční s tool detection ✅
- **Web Search Integration**: Funguje s DuckDuckGo API ✅
- **Keyword Detection**: "search", "find", "vyhledej", "najdi", etc. ✅
- **Parameter Extraction**: Automaticky extrahuje search query z textu ✅
- **Confidence Scoring**: Rozhoduje kdy použít nástroj (>70% confidence) ✅
- **Error Handling**: Robustní error handling a fallback ✅
- **Metrics Collection**: Real-time sledování výkonu a použití ✅
- **Health Monitoring**: Kontrola stavu Ollama + Tools ✅
- **ChatHub Integration**: Orchestrátor plně integrován do real-time chatu ✅
- **Tool Status Events**: SignalR události pro tool usage ✅

### 📋 Úspěšně Implementováno
- **Tool Detection Keywords**: "search", "find", "vyhledej", "najdi", etc.
- **ConversationOrchestrator**: Plná implementace s automatickou detekcí nástrojů
- **Orchestrator API**: Controller implementován (potřebuje testování routingu)
- **Integration s Web Search Tool**: Funkční integrace
- **Real-time Metrics**: Monitoring a health checking
- **Database Integration**: In-memory databáze s entitami
- **Structured Logging**: Komplexní logování všech operací

## 🎯 Success Criteria

- [x] Orchestrátoři následují všechny konvence projektu ✅ **DOKONČENO**
- [x] Vše je automaticky registrováno přes DI ✅ **DOKONČENO**
- [x] Plná integrace s existující infrastrukturou ✅ **DOKONČENO**
- [x] Comprehensive logging a error handling ✅ **DOKONČENO**
- [x] Aplikace se úspěšně spouští s orchestrátory ✅ **DOKONČENO**
- [x] Tool detection funguje (keywords + regex) ✅ **DOKONČENO**
- [x] Web Search Tool integrace ✅ **DOKONČENO**
- [x] Chat může automaticky používat AI Tools ✅ **DOKONČENO**
- [x] UI zobrazuje průběh orchestrace ✅ **DOKONČENO**
- [ ] Výkon je optimalizovaný (caching, async)
- [ ] Bezpečnost je zajištěna (autorizace, rate limiting)

## 📝 Notes

- Všechny orchestrátory musí být **idempotentní**
- Podporovat **cancellation tokens** všude
- Používat **structured logging**
- Následovat **existing naming conventions**
- Implementovat **proper error handling**
- Zajistit **thread safety**