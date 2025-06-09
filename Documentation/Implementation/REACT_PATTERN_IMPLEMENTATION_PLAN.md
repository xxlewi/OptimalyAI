# ReAct Pattern Implementation Plan for OptimalyAI

## 🏛️ Architektonické rozhodnutí

### ReAct jako SLUŽBA, ne nástroj
ReAct pattern bude implementován jako **služba (service)** v rámci Service Layer, nikoliv jako nástroj (tool). Toto rozhodnutí zajišťuje:
- Možnost využití ve všech orchestrátorech
- Přístup ke všem registrovaným nástrojům
- Centralizovanou správu reasoning logiky

### Architektura systému
```
┌─────────────────────────────────────────────────────────────┐
│                      Orchestrators Layer                      │
├─────────────────┬────────────────┬─────────────┬────────────┤
│ Conversation    │   ToolChain    │ Analytical  │    Code    │
│ Orchestrator    │  Orchestrator  │Orchestrator │Generation  │
└────────┬────────┴───────┬────────┴──────┬──────┴─────┬──────┘
         │                │                │            │
         └────────────────┴────────────────┴────────────┘
                                  │
                                  ▼
                    ┌──────────────────────────┐
                    │   IReActAgent Service    │
                    │  (Shared Reasoning Core) │
                    └────────────┬─────────────┘
                                 │
                    ┌────────────▼─────────────┐
                    │     IToolRegistry        │
                    │    IToolExecutor         │
                    └────────────┬─────────────┘
                                 │
         ┌───────────────────────┼───────────────────────┐
         ▼                       ▼                       ▼
   ┌──────────┐          ┌──────────┐           ┌──────────┐
   │WebSearch │          │   JSON   │           │    LLM   │
   │   Tool   │          │   Tool   │           │  Tornado │
   └──────────┘          └──────────┘           └──────────┘
```

### Klíčové rozhodnutí
1. **IReActAgent** bude injektován do orchestrátorů jako závislost
2. ReAct **koordinuje nástroje**, není sám nástrojem
3. Všechny orchestrátory mohou využívat stejnou ReAct logiku
4. Komunikace s nástroji probíhá výhradně přes IToolRegistry/IToolExecutor

## 🎯 Cíl
Implementovat ReAct (Reasoning + Acting) pattern do OptimalyAI orchestrátoru pro inteligentní zpracování výsledků z nástrojů a zlepšení kvality odpovědí.

## 🚧 Aktuální stav: IMPLEMENTOVÁNO S PROBLÉMY (85%)
- ✅ Veškerá infrastruktura implementována
- ✅ Integrace s ConversationOrchestrator dokončena
- ⚠️ Problém s konfigurací - ReAct se neaktivuje
- 🔍 Debugging v procesu

## 📋 Implementation Checklist

### 1. Core ReAct Infrastructure (SERVICE LAYER)

#### Interfaces (OAI.Core/Interfaces/Orchestration/)
- [x] `IReActAgent` - Základní interface pro ReAct agenty (bude injektován do orchestrátorů)
- [x] `IAgentMemory` - Interface pro správu paměti agenta
- [x] `IThoughtProcess` - Interface pro reasoning proces
- [x] `IActionExecutor` - Interface pro vykonávání akcí (komunikuje s IToolExecutor)
- [x] `IObservationProcessor` - Interface pro zpracování pozorování z nástrojů

#### DTOs (OAI.Core/DTOs/Orchestration/ReAct/)
- [x] `AgentThought` - Reprezentace myšlenky agenta
- [x] `AgentAction` - Akce k provedení
- [x] `AgentObservation` - Pozorování z nástroje
- [x] `AgentScratchpad` - Historie myšlenek a akcí
- [x] `ReActPromptTemplate` - Šablona promptu pro ReAct

#### Base Classes (OAI.ServiceLayer/Services/Orchestration/ReAct/)
- [x] `BaseReActAgent` - Základní implementace ReAct agenta (SERVICE, ne tool)
- [x] `AgentMemory` - Implementace paměti agenta
- [x] `ThoughtParser` - Parser pro extrakci myšlenek z LLM odpovědi
- [x] `ActionParser` - Parser pro extrakci akcí
- [x] `ObservationFormatter` - Formátovač pozorování
- [x] `ActionExecutor` - Koordinátor pro komunikaci s nástroji přes IToolRegistry

### 2. ReAct Agent Implementation

#### Core Agent (OAI.ServiceLayer/Services/Orchestration/ReAct/)
- [x] `ConversationReActAgent` - ReAct agent pro konverzace
  - [x] Reasoning loop implementace
  - [x] Tool selection logic
  - [x] Observation processing
  - [x] Final answer generation
  - [x] Error handling a retry logic
  - [x] Timeout management

#### Prompt Engineering
- [x] `ReActPromptTemplate` - Templates pro ReAct prompty
  - [x] System prompt template
  - [x] Tool descriptions formatter
  - [x] Scratchpad formatter
  - [x] Few-shot examples
  - [x] Language-specific templates (CS/EN)

#### Parsers
- [x] `ThoughtParser` - Parser LLM výstupu
  - [x] Regex patterns pro Thought/Action/Observation
  - [x] Fallback parsing strategies
  - [x] Error recovery
  - [x] Validation logic

### 3. Tool Integration Enhancement

#### Tool Result Processing
- [x] `ObservationProcessor` - Obohacení výsledků nástrojů
  - [x] Web search result processor
  - [x] JSON data formatter
  - [x] Error message handler
  - [x] Metadata extractor

#### Tool Description Generator
- [x] `ActionExecutor` - Generování popisů nástrojů pro LLM
  - [x] Parameter description formatter
  - [x] Usage examples generator
  - [x] Capability summarizer

### 4. Integration with Existing System

#### Multiple Orchestrator Support
ReAct agent bude dostupný pro všechny orchestrátory v systému:

1. **ConversationOrchestrator** (existující)
   - Konverzační interakce s uživatelem
   - Automatická detekce potřeby nástrojů
   - Streaming odpovědí s reasoning procesem

2. **ToolChainOrchestrator** (pro komplexní workflows)
   - Řetězení více nástrojů
   - Dependency resolution mezi nástroji
   - Paralelní zpracování kde je to možné

3. **AnalyticalOrchestrator** (pro analýzu dat)
   - Specializace na data analysis tasks
   - Automatická volba analytických nástrojů
   - Vizualizace reasoning procesu

4. **CodeGenerationOrchestrator** (pro generování kódu)
   - Specializace na programovací úlohy
   - Integrace s code analysis tools
   - Iterativní vylepšování kódu

#### ConversationOrchestrator Updates
- [x] Add ReAct mode toggle
- [x] Integrate ReActAgent via DI
- [x] Update tool execution flow to use ReAct
- [x] Add scratchpad to context
- [ ] Implement streaming for thoughts

#### Configuration
- [x] Add ReAct settings to appsettings.json
  ```json
  "ReActSettings": {
    "Enabled": true,
    "MaxIterations": 5,
    "ThoughtVisibility": "Full|Summary|None",
    "EnableParallelTools": false,
    "TimeoutSeconds": 30
  }
  ```

### 5. LLM Integration

#### LLM Service Updates
- [ ] `IReActLlmService` - Specializovaná služba pro ReAct
  - [ ] Thought generation method
  - [ ] Action extraction method
  - [ ] Final answer generation
  - [ ] Token optimization

#### Prompt Templates
- [ ] Czech ReAct prompts
- [ ] English ReAct prompts
- [ ] Tool-specific prompts
- [ ] Error recovery prompts

### 6. Observability & Debugging

#### Logging
- [ ] Structured logging pro každý krok
- [ ] Thought process logging
- [ ] Tool execution logging
- [ ] Performance metrics

#### Tracing
- [ ] `ReActExecutionTrace` - Trace celého procesu
- [ ] Step-by-step visualization data
- [ ] Decision tree export

### 7. UI Integration

#### SignalR Updates
- [ ] `ThoughtStreamStarted` event
- [ ] `ThoughtGenerated` event
- [ ] `ActionExecuting` event
- [ ] `ObservationReceived` event
- [ ] `FinalAnswerReady` event

#### UI Components
- [ ] Thought process viewer
  - [ ] Collapsible thought steps
  - [ ] Action/Tool indicators
  - [ ] Observation results
  - [ ] Progress indicator
- [ ] ReAct mode toggle
- [ ] Debug mode view

### 8. Testing Infrastructure

#### Unit Tests
- [x] Parser tests
- [x] Agent logic tests (ConversationReActAgentTests.cs)
  - [x] Simple query execution
  - [x] Tool usage scenarios
  - [x] Max iterations handling
- [x] Tool integration tests
- [x] Error handling tests

#### Integration Tests
- [x] End-to-end ReAct flow (ConversationOrchestratorReActTests.cs)
- [x] Multi-tool scenarios
- [x] Error recovery scenarios
- [x] Timeout handling
- [x] Performance comparison tests
- [x] Concurrent execution tests

#### Test Scripts
- [x] test-react.sh - Comprehensive test suite
- [x] test-react-simple.sh - Quick functionality check
- [x] test-react-integration.sh - Advanced integration tests
- [x] REACT_TESTING_GUIDE.md - Complete testing documentation

#### Test Scenarios
- [x] Simple search + process
- [x] Multi-step reasoning
- [x] Tool failure recovery
- [x] Max iterations prevention
- [x] Auto-enable for complex queries
- [x] Context persistence across messages

### 9. Performance Optimization

#### Caching
- [ ] Tool result caching
- [ ] Thought pattern caching
- [ ] Common reasoning paths

#### Parallel Execution
- [ ] Parallel tool execution (when safe)
- [ ] Async observation processing
- [ ] Batch LLM requests

### 10. Advanced Features

#### Learning & Improvement
- [ ] Success pattern detection
- [ ] Failure analysis
- [ ] Prompt optimization hints
- [ ] Tool usage statistics

#### Multi-Agent Support
- [ ] Agent collaboration protocol
- [ ] Task delegation
- [ ] Result aggregation

## 🏗️ Implementační postup

### Fáze 1: Core Infrastructure (1-2 dny)
1. Vytvořit interfaces a DTOs
2. Implementovat base classes
3. Vytvořit základní ReActAgent
4. Implementovat parsery

### Fáze 2: Integration (2-3 dny)
1. Integrovat s ConversationOrchestrator
2. Upravit tool execution flow
3. Implementovat result processing
4. Přidat configuration

### Fáze 3: UI & Observability (1-2 dny)
1. Přidat SignalR eventy
2. Implementovat UI komponenty
3. Přidat logging a tracing
4. Vytvořit debug view

### Fáze 4: Testing & Optimization (1 den)
1. Napsat unit testy
2. Provést integration testing
3. Optimalizovat performance
4. Dokumentace

## 📊 Success Metrics

- [ ] Agent úspěšně zpracovává multi-tool scénáře
- [ ] Transparentní reasoning proces
- [ ] Odpovědi využívají tool results efektivně
- [ ] UI zobrazuje thought process
- [ ] Performance < 5s pro běžné queries
- [ ] Error recovery funguje spolehlivě

## 🔧 Technické detaily

### ReAct Loop Pseudocode
```csharp
while (!complete && iterations < maxIterations) {
    // 1. Generate thought
    var thought = await GenerateThought(context, scratchpad);
    
    // 2. Parse action from thought
    var action = ParseAction(thought);
    
    // 3. Execute action/tool
    if (action.RequiresTool) {
        var observation = await ExecuteTool(action.Tool, action.Input);
        scratchpad.Add(thought, action, observation);
    }
    
    // 4. Check if we have final answer
    if (action.IsFinalAnswer) {
        complete = true;
        result = action.Answer;
    }
}
```

### Příklad ReAct procesu
```
User: "Jaké je počasí v Praze a New Yorku?"

Thought: Potřebuji zjistit počasí ve dvou městech - Praze a New Yorku.
Action: web_search
Action Input: {"query": "počasí Praha aktuální"}
Observation: V Praze je 18°C, polojasno, vlhkost 65%

Thought: Mám počasí pro Prahu, teď potřebuji New York.
Action: web_search  
Action Input: {"query": "weather New York current"}
Observation: New York má 72°F (22°C), jasno, vlhkost 45%

Thought: Mám informace o počasí pro obě města.
Final Answer: V Praze je aktuálně 18°C s polojasnem a vlhkostí 65%. 
V New Yorku je tepleji - 22°C (72°F), jasná obloha a vlhkost 45%.
```

## 🚀 Očekávané přínosy

1. **Lepší kvalita odpovědí** - Agent využívá tool results efektivně
2. **Transparentnost** - Uživatel vidí reasoning proces
3. **Flexibilita** - Agent si sám určuje, které tools použít
4. **Robustnost** - Schopnost recovery z chyb
5. **Rozšiřitelnost** - Snadné přidávání nových tools

## ⚠️ Rizika a mitigace

| Riziko | Dopad | Mitigace |
|--------|-------|----------|
| Vysoká latence | UX degradace | Streaming, caching, timeouts |
| Vysoké náklady | Budget overrun | Token limits, prompt optimization |
| Parsing chyby | Selhání agenta | Fallback strategies, robust parsing |
| Circular reasoning | Nekonečná smyčka | Max iterations, loop detection |

## 📝 Poznámky

- ReAct pattern vyžaduje kvalitní prompty
- Důležité je správné formátování tool descriptions
- Streaming zlepšuje perceived performance
- Debug mode je kritický pro troubleshooting
- Metrikování pomáhá optimalizaci