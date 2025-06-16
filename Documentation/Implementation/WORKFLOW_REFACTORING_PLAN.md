# Workflow Designer - Refactoring Plan

## 📋 Přehled
Tento dokument obsahuje detailní plán refaktoringu Workflow Designeru v OptimalyAI projektu.

**Aktuální stav:**
- `SimpleWorkflowDesigner.cshtml` - 3400 řádků
- Veškerá logika je inline v jednom souboru
- Žádná modularizace, těžká údržba

**Cílový stav:**
- Modulární architektura
- Oddělené concerns (HTML, CSS, JS)
- Testovatelný kód
- Lepší výkon

---

## ✅ Checklist - Fáze 1: Separace stylů (2-3 hodiny)

### CSS Extrakce
- [ ] Vytvořit `/wwwroot/css/workflow-designer.css`
- [ ] Přesunout všechny `<style>` bloky ze `SimpleWorkflowDesigner.cshtml`
- [ ] Přidat reference na CSS soubor do view
- [ ] Otestovat že vše funguje stejně

### Struktura CSS souborů:
```
/wwwroot/css/
├── workflow-designer.css          # Hlavní styly
├── workflow-designer-nodes.css    # Styly pro uzly
└── workflow-designer-canvas.css   # Styly pro canvas
```

---

## ✅ Checklist - Fáze 2: Separace HTML do partial views (1 den)

### Vytvořit partial views:
- [ ] `_WorkflowHeader.cshtml` - hlavička s tlačítky
- [ ] `_WorkflowToolbox.cshtml` - levý panel s nástroji
- [ ] `_WorkflowCanvas.cshtml` - hlavní pracovní plocha
- [ ] `_WorkflowModals.cshtml` - všechny modální okna

### Struktura:
```
/Views/WorkflowDesigner/
├── SimpleWorkflowDesigner.cshtml   # Hlavní view (pouze layout)
├── Partials/
│   ├── _WorkflowHeader.cshtml
│   ├── _WorkflowToolbox.cshtml
│   ├── _WorkflowCanvas.cshtml
│   └── _WorkflowModals.cshtml
```

### Příklad hlavního view po refaktoringu:
```html
@model OptimalyAI.ViewModels.WorkflowGraphViewModel

<div class="workflow-designer-container">
    @await Html.PartialAsync("Partials/_WorkflowHeader", Model)
    
    <div class="designer-content">
        @await Html.PartialAsync("Partials/_WorkflowToolbox", Model)
        @await Html.PartialAsync("Partials/_WorkflowCanvas", Model)
    </div>
</div>

@await Html.PartialAsync("Partials/_WorkflowModals", Model)

@section Scripts {
    <script src="~/js/workflow/workflow-designer.js"></script>
}
```

---

## ✅ Checklist - Fáze 3: JavaScript modularizace (3-4 dny)

### Vytvořit JavaScript moduly:

#### 1. Hlavní modul - `workflow-designer.js`
- [ ] Vytvořit třídu `WorkflowDesigner`
- [ ] Inicializace všech sub-modulů
- [ ] Globální state management
- [ ] Event handling

#### 2. Node Manager - `node-manager.js`
- [ ] Třída `NodeManager`
- [ ] CRUD operace pro uzly
- [ ] Node rendering
- [ ] Node selection/deselection

#### 3. Connection Manager - `connection-manager.js`
- [ ] Třída `ConnectionManager`
- [ ] Kreslení spojení (SVG)
- [ ] Validace spojení
- [ ] Connection events

#### 4. Drag & Drop - `drag-drop-handler.js`
- [ ] Třída `DragDropHandler`
- [ ] Drag z toolboxu
- [ ] Drop na canvas
- [ ] Node repositioning

#### 5. Workflow API - `workflow-api.js`
- [ ] Třída `WorkflowAPI`
- [ ] AJAX volání na server
- [ ] Promise-based API
- [ ] Error handling

### Struktura souborů:
```
/wwwroot/js/workflow/
├── workflow-designer.js        # Hlavní třída
├── modules/
│   ├── node-manager.js
│   ├── connection-manager.js
│   ├── drag-drop-handler.js
│   ├── workflow-api.js
│   └── workflow-validator.js
├── utils/
│   ├── constants.js           # Konstanty
│   └── helpers.js             # Helper funkce
```

### Příklad modulu:
```javascript
// node-manager.js
export class NodeManager {
    constructor(designer) {
        this.designer = designer;
        this.nodes = new Map();
    }
    
    addNode(type, x, y, data = {}) {
        const nodeId = this.generateNodeId();
        const node = {
            id: nodeId,
            type: type,
            position: { x, y },
            ...data
        };
        
        this.nodes.set(nodeId, node);
        this.renderNode(node);
        return nodeId;
    }
    
    removeNode(nodeId) {
        if (!this.nodes.has(nodeId)) return false;
        
        // Remove connections first
        this.designer.connectionManager.removeNodeConnections(nodeId);
        
        // Remove DOM element
        document.getElementById(nodeId)?.remove();
        
        // Remove from state
        this.nodes.delete(nodeId);
        return true;
    }
    
    updateNode(nodeId, updates) {
        const node = this.nodes.get(nodeId);
        if (!node) return false;
        
        Object.assign(node, updates);
        this.renderNode(node);
        return true;
    }
    
    private renderNode(node) {
        // Rendering logic here
    }
    
    private generateNodeId() {
        return `node_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }
}
```

---

## ✅ Checklist - Fáze 4: ViewComponents (2 dny)

### Vytvořit ViewComponents:

#### 1. WorkflowToolboxViewComponent
- [ ] Vytvořit `ViewComponents/WorkflowToolboxViewComponent.cs`
- [ ] Načítat tools z ToolRegistry
- [ ] View: `Views/Shared/Components/WorkflowToolbox/Default.cshtml`

#### 2. WorkflowNodeViewComponent
- [ ] Renderování jednotlivých uzlů
- [ ] Podpora různých typů uzlů

#### 3. WorkflowCanvasViewComponent
- [ ] Canvas s podporou zoom/pan
- [ ] Grid background

### Příklad ViewComponent:
```csharp
public class WorkflowToolboxViewComponent : ViewComponent
{
    private readonly IToolRegistry _toolRegistry;
    
    public WorkflowToolboxViewComponent(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }
    
    public async Task<IViewComponentResult> InvokeAsync(bool readOnly = false)
    {
        var model = new WorkflowToolboxViewModel
        {
            Tools = await _toolRegistry.GetAllToolsAsync(),
            ReadOnly = readOnly,
            Categories = new[] { "Input/Output", "Processing", "AI Tools" }
        };
        
        return View(model);
    }
}
```

---

## ✅ Checklist - Fáze 5: API Controller (2 dny)

### Vytvořit API endpoints:

#### WorkflowApiController
- [ ] `GET /api/workflow/{projectId}` - načíst workflow
- [ ] `POST /api/workflow/{projectId}/save` - uložit celé workflow
- [ ] `POST /api/workflow/{projectId}/nodes` - přidat uzel
- [ ] `PUT /api/workflow/{projectId}/nodes/{nodeId}` - upravit uzel
- [ ] `DELETE /api/workflow/{projectId}/nodes/{nodeId}` - smazat uzel
- [ ] `POST /api/workflow/{projectId}/connections` - přidat spojení
- [ ] `DELETE /api/workflow/{projectId}/connections/{connectionId}` - smazat spojení
- [ ] `POST /api/workflow/{projectId}/validate` - validovat workflow
- [ ] `POST /api/workflow/{projectId}/execute` - spustit workflow

### DTOs:
```csharp
public class NodeDto
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
    public PositionDto Position { get; set; }
    public Dictionary<string, object> Properties { get; set; }
}

public class ConnectionDto
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public string SourcePort { get; set; }
    public string TargetPort { get; set; }
}
```

---

## ✅ Checklist - Fáze 6: Migrace dat a testování (2 dny)

### Migrace:
- [ ] Zajistit zpětnou kompatibilitu
- [ ] Migrační skript pro existující workflows
- [ ] Backup původních souborů

### Testování:
- [ ] Unit testy pro JS moduly
- [ ] Integration testy pro API
- [ ] E2E testy pro workflow designer
- [ ] Performance testy

### Test cases:
- [ ] Vytvoření nového workflow
- [ ] Načtení existujícího workflow
- [ ] Drag & drop uzlů
- [ ] Spojování uzlů
- [ ] Validace workflow
- [ ] Export/Import workflow
- [ ] Undo/Redo operace

---

## 📊 Časový harmonogram

| Fáze | Časová náročnost | Priorita |
|------|------------------|----------|
| Fáze 1: CSS separace | 2-3 hodiny | Vysoká |
| Fáze 2: HTML partial views | 1 den | Vysoká |
| Fáze 3: JS modularizace | 3-4 dny | Kritická |
| Fáze 4: ViewComponents | 2 dny | Střední |
| Fáze 5: API Controller | 2 dny | Střední |
| Fáze 6: Testing | 2 dny | Vysoká |
| **Celkem** | **10-12 dní** | |

---

## 🚀 Quick Wins (lze udělat okamžitě)

1. **Extrakce CSS** - 2 hodiny, okamžitý benefit
2. **Odstranění mrtvého kódu** - ✅ Již hotovo
3. **Přesun konstant do konfigurace** - 1 hodina
4. **Základní JSDoc dokumentace** - průběžně

---

## 📝 Poznámky

- Zachovat stávající funkcionalitu
- Postupovat po malých krocích
- Každou fázi otestovat před pokračováním
- Verzovat změny v Gitu

---

## 🎯 Měřitelné cíle

- Snížit velikost hlavního view z 3400 na < 500 řádků
- Zlepšit load time o 30%
- Umožnit unit testing (code coverage > 70%)
- Snížit coupling mezi komponentami

---

## 🔄 Aktualizace plánu

- **15.6.2025**: Vytvořen iniciální plán
- **[datum]**: [co bylo upraveno]

---

*Tento dokument průběžně aktualizujte podle postupu prací.*