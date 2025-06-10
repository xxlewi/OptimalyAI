# Project Workflow Redesign - Implementační plán

## Přehled
Redesign projektů jako vícekrokový wizard pro vytváření AI řešení s vizuálním workflow designerem.

## Cíl
Transformovat projekty z jednoduchých entit na komplexní workflow builder, který umožní:
- Vizuální design workflow
- Výběr orchestrátorů a ReAct agentů pro každý krok
- Konfigurace multiple toolů
- Testování a ladění
- Automatické spouštění

## Architektura

### Workflow struktura
```
PROJECT
  └── STAGE (krok workflow)
       ├── Orchestrator (řídí vykonávání)
       ├── ReAct Agent (AI reasoning - volitelný)
       └── Tools[] (konkrétní akce)
```

## Implementační checklist

### 1. Backend - Databázový redesign
- [ ] Vytvořit novou entitu `ProjectStage` (nahradí/rozšíří ProjectWorkflow)
- [ ] Vytvořit entitu `ProjectStageTool` (many-to-many vazba)
- [ ] Vytvořit entitu `ProjectStageConfiguration`
- [ ] Přidat sloupce do Project entity:
  - [ ] `WorkflowVersion` (pro verzování workflow)
  - [ ] `IsTemplate` (pro ukládání jako šablonu)
  - [ ] `TemplateId` (odkaz na šablonu)
- [ ] Vytvořit migraci
- [ ] Migrovat data z ProjectWorkflow do ProjectStage

### 2. Backend - Domain Layer (OAI.Core)
- [ ] Vytvořit DTOs:
  - [ ] `ProjectStageDto`
  - [ ] `CreateProjectStageDto`
  - [ ] `UpdateProjectStageDto`
  - [ ] `ProjectStageToolDto`
  - [ ] `ProjectWorkflowDesignDto` (pro celý workflow)
- [ ] Vytvořit enums:
  - [ ] `StageType` (Input, Processing, Output, Decision)
  - [ ] `ExecutionStrategy` (Sequential, Parallel, Conditional)
- [ ] Aktualizovat `ProjectDto` o stages

### 3. Backend - Service Layer
- [ ] Vytvořit `ProjectStageService`:
  - [ ] `CreateStageAsync()`
  - [ ] `UpdateStageAsync()`
  - [ ] `ReorderStagesAsync()`
  - [ ] `DeleteStageAsync()`
  - [ ] `CloneStageAsync()`
- [ ] Vytvořit `ProjectWorkflowDesignerService`:
  - [ ] `GetAvailableOrchestratorsAsync()`
  - [ ] `GetAvailableReActAgentsAsync()`
  - [ ] `GetAvailableToolsAsync()`
  - [ ] `ValidateWorkflowAsync()`
  - [ ] `SaveWorkflowAsync()`
- [ ] Vytvořit `ProjectTemplateService`:
  - [ ] `CreateTemplateFromProjectAsync()`
  - [ ] `CreateProjectFromTemplateAsync()`
  - [ ] `GetTemplatesAsync()`
- [ ] Rozšířit `ProjectService`:
  - [ ] `TestWorkflowAsync()` - dry run
  - [ ] `PublishWorkflowAsync()` - přepnout do produkce
- [ ] Aktualizovat `ProjectExecutionService`:
  - [ ] Integrovat s novým stage systémem
  - [ ] Použít správné orchestrátory a ReAct agenty

### 4. Backend - Mappery
- [ ] Vytvořit `ProjectStageMapper`
- [ ] Vytvořit `ProjectStageToolMapper`
- [ ] Aktualizovat `ProjectMapper` pro stages

### 5. Backend - Validátory
- [ ] Vytvořit `ProjectStageValidator`
- [ ] Vytvořit `ProjectWorkflowValidator` (validace celého workflow)
- [ ] Validační pravidla:
  - [ ] Alespoň jeden stage
  - [ ] Každý stage má orchestrátor
  - [ ] Každý stage má alespoň jeden tool (nebo ReAct agent)
  - [ ] Unikátní názvy stages

### 6. API Controllers
- [ ] Vytvořit `ProjectWorkflowController`:
  - [ ] `GET /api/projects/{id}/workflow` - získat workflow design
  - [ ] `PUT /api/projects/{id}/workflow` - uložit workflow design
  - [ ] `POST /api/projects/{id}/workflow/test` - test run
  - [ ] `POST /api/projects/{id}/workflow/publish` - publikovat
- [ ] Vytvořit `ProjectStagesController`:
  - [ ] CRUD operace pro stages
  - [ ] `POST /api/projects/{id}/stages/{stageId}/reorder`
  - [ ] `POST /api/projects/{id}/stages/{stageId}/clone`
- [ ] Vytvořit `WorkflowDesignerController`:
  - [ ] `GET /api/workflow-designer/orchestrators`
  - [ ] `GET /api/workflow-designer/react-agents`
  - [ ] `GET /api/workflow-designer/tools`
  - [ ] `GET /api/workflow-designer/templates`

### 7. Frontend - MVC Views
- [ ] Vytvořit `Views/Projects/WorkflowDesigner.cshtml`:
  - [ ] Vizuální designer (drag & drop)
  - [ ] Stage konfigurátor
  - [ ] Tool selector
  - [ ] Test runner
- [ ] Aktualizovat `Views/Projects/Create.cshtml`:
  - [ ] Přidat wizard kroky
  - [ ] Integrace s workflow designerem
- [ ] Vytvořit `Views/Projects/Templates.cshtml`:
  - [ ] Seznam šablon
  - [ ] Preview šablony

### 8. Frontend - JavaScript/TypeScript
- [ ] Vytvořit workflow designer komponentu:
  - [ ] Canvas pro vizualizaci
  - [ ] Drag & drop stages
  - [ ] Connection lines mezi stages
  - [ ] Stage properties panel
- [ ] Vytvořit stage konfigurátor:
  - [ ] Orchestrator selector
  - [ ] ReAct agent selector (podmíněný)
  - [ ] Tool multi-selector
  - [ ] Configuration forms
- [ ] Vytvořit test runner:
  - [ ] Upload test dat
  - [ ] Real-time progress
  - [ ] Výsledky zobrazení

### 9. Integrace s existujícími systémy
- [ ] Zajistit kompatibilitu s `ToolChainOrchestrator`
- [ ] Integrovat s `ConversationOrchestrator`
- [ ] Propojit s ReAct agenty
- [ ] Zachovat SignalR monitoring

### 10. Testování
- [ ] Unit testy pro nové services
- [ ] Integrační testy pro workflow execution
- [ ] E2E testy pro wizard flow
- [ ] Performance testy pro složité workflows

### 11. Dokumentace
- [ ] API dokumentace (Swagger)
- [ ] Uživatelská příručka pro workflow designer
- [ ] Příklady workflow šablon
- [ ] Migration guide pro existující projekty

### 12. Migration a deployment
- [ ] Migrace existujících projektů na nový systém
- [ ] Backup starých dat
- [ ] Postupný rollout (feature flag?)
- [ ] Monitoring nového systému

## Prioritizace

### Fáze 1 - Základ (1-2 týdny)
1. Databázový redesign
2. Domain layer (entity, DTOs)
3. Základní services
4. API controllers

### Fáze 2 - UI (2-3 týdny)
1. Workflow designer view
2. JavaScript komponenty
3. Stage konfigurátor
4. Základní testování

### Fáze 3 - Integrace (1 týden)
1. Propojení s orchestrátory
2. ReAct agent integrace
3. Tool integrace
4. SignalR monitoring

### Fáze 4 - Polish (1 týden)
1. Šablony
2. Validace
3. Error handling
4. Performance optimalizace

## Poznámky
- Zachovat zpětnou kompatibilitu kde možné
- Fokus na UX - wizard musí být intuitivní
- Připravit na budoucí rozšíření (např. větvení workflow)