# IMPLEMENTAČNÍ PLÁN - NOVÝ WORKFLOW DESIGNER S JOINTJS

## PŘEHLED IMPLEMENTACE

**Cíl:** Nahradit současný primitivní workflow designer pokročilým JointJS řešením s real-time execution trackingem a detailním zobrazením výsledků.

**Hlavní funkcionality:**
- ✅ Drag & drop workflow editor s profesionálním UI
- ✅ Real-time step highlighting během execution
- ✅ Detailní zobrazení výsledků jednotlivých kroků
- ✅ Zachování stávající backend architektury
- ✅ Razor Views kompatibilita (bez React komplikací)

---

## PHASE 1: PŘÍPRAVA A ZÁKLADNÍ INTEGRACE

### 1.1 Frontend Dependencies a Setup
- [ ] **Přidat JointJS v3.6+ do projektu**
  - [ ] Instalovat přes npm/yarn nebo CDN
  - [ ] Konfigurovat Webpack pro bundling (pokud není)
  - [ ] Ověřit kompatibilitu s AdminLTE 3

- [ ] **Připravit nové CSS soubory**
  - [ ] Vytvořit `jointjs-workflow-designer.css`
  - [ ] Definovat custom workflow node styly
  - [ ] Připravit execution highlighting styly

- [ ] **Aktualizovat Layout a Views**
  - [ ] Zkopírovat `WorkflowDesigner/Index.cshtml` jako zálohu
  - [ ] Připravit nový layout pro JointJS canvas
  - [ ] Přidat prostor pro execution panel a results view

### 1.2 Backend API rozšíření
- [ ] **Rozšířit WorkflowDesignerApiController**
  - [ ] Přidat endpoint `GET /api/workflow/{id}/definition`
  - [ ] Přidat endpoint `POST /api/workflow/{id}/validate-structure`
  - [ ] Přidat endpoint `GET /api/workflow/tools-catalog`

- [ ] **Vytvořit WorkflowHub (SignalR)**
  - [ ] Implementovat `Hubs/WorkflowHub.cs`
  - [ ] Metody: `JoinWorkflowGroup`, `LeaveWorkflowGroup`
  - [ ] Events: `WorkflowUpdated`, `ExecutionStatusChanged`
  - [ ] Registrovat v Program.cs

- [ ] **Rozšířit WorkflowExecutor events**
  - [ ] Integrovat SignalR broadcast do stávajících events
  - [ ] Přidat `StepProgressChanged` event pro granulární tracking
  - [ ] Přidat `WorkflowValidationRequested` event

---

## PHASE 2: CORE JOINTJS IMPLEMENTACE

### 2.1 JointJS Designer Core
- [ ] **Vytvořit `js/workflow/jointjs-designer.js`**
  - [ ] Inicializace JointJS paper a graph
  - [ ] Custom workflow node shapes (tool, decision, adapter)
  - [ ] Drag & drop z toolboxu
  - [ ] Basic linking mezi nodes

- [ ] **Implementovat Custom Node Types**
  - [ ] `ToolNode` - reprezentace AI nástroje
  - [ ] `DecisionNode` - podmínkové větvení  
  - [ ] `AdapterNode` - input/output adaptéry
  - [ ] `StartNode` a `EndNode` - workflow hranice

- [ ] **Node Configuration Panels**
  - [ ] Modal dialog pro konfiguraci tool nodes
  - [ ] Dropdown pro výběr nástroje z ToolRegistry
  - [ ] Parametr mapping interface
  - [ ] Validation feedback

### 2.2 Workflow Data Integration
- [ ] **Converter mezi JointJS a Workflow DTOs**
  - [ ] `JointJSToWorkflowConverter.cs` - server-side
  - [ ] `workflow-data-converter.js` - client-side
  - [ ] Bidirectional conversion s validací

- [ ] **Implementovat Save/Load funkcionalitu**
  - [ ] Export JointJS graph do WorkflowDefinition DTO
  - [ ] Import z ProjectWorkflow.StepsDefinition
  - [ ] Preserve node positions v MetaData

- [ ] **Real-time Collaboration (basic)**
  - [ ] Broadcast změn přes WorkflowHub
  - [ ] Conflict resolution pro současné úpravy
  - [ ] User presence indikátory

---

## PHASE 3: EXECUTION MONITORING A VISUALIZATION

### 3.1 Real-time Execution Tracking
- [ ] **Execution Status Visualization**
  - [ ] Implementovat JointJS highlighters pro aktuální krok
  - [ ] Color coding pro stav kroků (pending, running, success, error)
  - [ ] Animated progression mezi kroky
  - [ ] Progress bar pro celkový completion

- [ ] **SignalR Integration pro Live Updates**
  - [ ] Subscribe na execution events z WorkflowExecutor
  - [ ] Update node styling based na step status
  - [ ] Show live execution log vedle diagramu

- [ ] **Step Results Detail Panel**
  - [ ] Expandable panel pro step outputs
  - [ ] JSON viewer pro structured data
  - [ ] Error details s stack traces
  - [ ] Timing informace (duration, start/end times)

### 3.2 Enhanced Execution Features
- [ ] **Execution History Timeline**
  - [ ] Chronological view všech execution runs
  - [ ] Click-to-replay functionality
  - [ ] Compare results mezi different runs
  - [ ] Export execution reports

- [ ] **Interactive Debugging**
  - [ ] Breakpoints na specific nodes
  - [ ] Step-by-step execution mode
  - [ ] Variable inspection během runtime
  - [ ] Manual variable override

---

## PHASE 4: POKROČILÉ FUNKCE A OPTIMIZATION

### 4.1 Advanced Designer Features
- [ ] **Auto-layout Algorithms**
  - [ ] Hierarchical layout pro complex workflows
  - [ ] Dagre.js integration pro automatic positioning
  - [ ] Compact view pro large workflows

- [ ] **Workflow Templates**
  - [ ] Pre-defined workflow patterns
  - [ ] Template gallery s import/export
  - [ ] AI-suggested workflow structures

- [ ] **Validation a Error Prevention**
  - [ ] Real-time structural validation
  - [ ] Circular dependency detection
  - [ ] Unreachable node warnings
  - [ ] Tool compatibility checks

### 4.2 Performance a UX Improvements
- [ ] **Large Workflow Support**
  - [ ] Virtual scrolling pro massive diagrams
  - [ ] Lazy loading pro node details
  - [ ] Zoom a pan optimizations

- [ ] **UI/UX Enhancements**
  - [ ] Context menus pro nodes a edges
  - [ ] Keyboard shortcuts
  - [ ] Undo/redo functionality
  - [ ] Search a filter nodes

- [ ] **Export a Integration**
  - [ ] Export do BPMN 2.0 formátu
  - [ ] PDF/PNG export vysoké kvality
  - [ ] Integration s project management tools

---

## PHASE 5: TESTING A DEPLOYMENT

### 5.1 Testing Strategy
- [ ] **Unit Tests**
  - [ ] Test WorkflowHub SignalR communication
  - [ ] Test JointJS data converters
  - [ ] Test execution event handling

- [ ] **Integration Tests**
  - [ ] End-to-end workflow creation a execution
  - [ ] Multi-user collaboration scenarios
  - [ ] Browser compatibility testing

- [ ] **Performance Testing**
  - [ ] Large workflow loading times
  - [ ] Real-time update performance
  - [ ] Memory usage optimization

### 5.2 Documentation a Training
- [ ] **User Documentation**
  - [ ] Workflow designer user guide
  - [ ] Best practices pro workflow design
  - [ ] Troubleshooting guide

- [ ] **Developer Documentation**
  - [ ] Architecture documentation
  - [ ] API reference
  - [ ] Extension guide pro custom nodes

---

## TECHNICKÉ DETAILY IMPLEMENTACE

### Klíčové Soubory k Vytvoření/Úpravě:

**Frontend:**
```
├── wwwroot/js/workflow/
│   ├── jointjs-designer.js          (NEW)
│   ├── workflow-execution-viewer.js (NEW)
│   ├── workflow-data-converter.js   (NEW)
│   └── workflow-hub-client.js       (NEW)
├── wwwroot/css/
│   └── jointjs-workflow-designer.css (NEW)
├── Views/WorkflowDesigner/
│   ├── Index.cshtml                 (MAJOR UPDATE)
│   ├── ExecutionView.cshtml         (NEW)
│   └── _WorkflowResults.cshtml      (NEW)
```

**Backend:**
```
├── Hubs/
│   └── WorkflowHub.cs               (NEW)
├── Controllers/
│   └── WorkflowDesignerApiController.cs (UPDATE)
├── Services/Workflow/
│   ├── WorkflowExecutor.cs          (UPDATE - add SignalR)
│   └── JointJSToWorkflowConverter.cs (NEW)
├── DTOs/Workflow/
│   └── WorkflowExecutionDtos.cs     (UPDATE)
```

### Závislosti k Přidání:
- **JointJS v3.6+** (CDN nebo npm)
- **Dagre.js** pro auto-layout
- **Lodash** (JointJS dependency)

### Databáze Změny:
- Žádné schema změny potřeba
- Využití stávajících `ProjectWorkflow.StepsDefinition` a `ProjectExecution`

---

## TIMELINE ODHAD

- **Phase 1**: 3-4 dny
- **Phase 2**: 5-7 dní  
- **Phase 3**: 4-5 dní
- **Phase 4**: 6-8 dní
- **Phase 5**: 2-3 dny

**Celkem**: ~20-27 dní pro kompletní implementaci

---

## KRITICKÉ ROZHODNUTÍ

### ✅ CO ZACHOVAT:
- Stávající WorkflowExecutor a jeho event system
- ProjectWorkflow database schema
- ToolRegistry a AdapterRegistry
- Současné API endpointy (rozšířit, ne přepsat)

### ❌ CO NAHRADIT:
- Současný Canvas-based designer → JointJS
- Statické workflow view → Real-time execution monitoring
- Primitivní UI → Profesionální drag & drop interface

### 🔄 CO ROZŠÍŘIT:
- SignalR pro real-time collaboration
- WorkflowExecutor events pro granulární tracking
- Workflow validation a debugging capabilities

---

*Tento implementační plán poskytuje step-by-step roadmap pro vytvoření moderního workflow designeru s důrazem na tvůj specifický use case: professionální editor + real-time execution tracking + detailní results view.*