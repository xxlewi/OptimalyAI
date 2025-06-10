# E2E Workflow Design - OptimalyAI

## Koncept: Životní cyklus požadavku

Každý požadavek (ať už produktové foto, analýza dokumentu nebo jiný úkol) projde stejným, transparentním procesem:

```
📥 Příjem → 🔍 Analýza → 📋 Plánování → ⚙️ Zpracování → ✅ Kontrola → 📤 Doručení → 📊 Vyhodnocení
```

## Navrhovaná struktura menu

### 1. POŽADAVKY (Requests)
```
📥 Požadavky
├── 🆕 Nový požadavek        [/Requests/New]
├── 📨 Příchozí fronta       [/Requests/Queue]
├── 🔄 Aktivní zpracování    [/Requests/Active]
├── ✅ Dokončené             [/Requests/Completed]
└── ❌ Selhané               [/Requests/Failed]
```

### 2. WORKFLOW (Pracovní postupy)
```
⚙️ Workflow
├── 📋 Šablony workflow      [/Workflows/Templates]
├── 🔧 Editor workflow       [/Workflows/Editor]
├── 📊 Analýza výkonu        [/Workflows/Analytics]
└── 🧪 Testování             [/Workflows/Test]
```

### 3. NÁSTROJE (Tools)
```
🔨 Nástroje
├── 📚 Katalog nástrojů      [/Tools]
├── ➕ Registrace nástroje   [/Tools/Register]
├── 🔧 Konfigurace           [/Tools/Config]
└── 📊 Využití nástrojů      [/Tools/Usage]
```

### 4. ORCHESTRACE
```
🎭 Orchestrace
├── 🎯 Aktivní orchestrace   [/Orchestrators]
├── 📈 Metriky               [/Orchestrators/Metrics]
├── 🔍 Debug & Logs          [/Orchestrators/Debug]
└── ⚡ Real-time monitor     [/Orchestrators/Monitor]
```

### 5. INTEGRACE
```
🔌 Integrace
├── 🏪 E-shop platformy      [/Integrations/Eshop]
├── 🤖 AI modely             [/Integrations/AI]
├── 💾 Úložiště              [/Integrations/Storage]
└── 📧 Notifikace            [/Integrations/Notifications]
```

### 6. MONITORING
```
📊 Monitoring
├── 📈 Dashboard             [/Monitoring/Dashboard]
├── 🚨 Alerty                [/Monitoring/Alerts]
├── 📉 Reporty               [/Monitoring/Reports]
└── 💰 Náklady               [/Monitoring/Costs]
```

## UI Flow pro nový požadavek

### Krok 1: Vytvoření požadavku [/Requests/New]
```
┌─────────────────────────────────────────┐
│         🆕 NOVÝ POŽADAVEK               │
├─────────────────────────────────────────┤
│                                         │
│ Typ požadavku:                         │
│ ┌─────────────────────────┐            │
│ │ 📸 Produktové foto      │            │
│ │ 📄 Analýza dokumentu    │            │
│ │ 🔍 Web scraping         │            │
│ │ 🤖 Custom AI úloha      │            │
│ └─────────────────────────┘            │
│                                         │
│ Priorita: [Normal ▼]                   │
│                                         │
│ Deadline: [____________] 📅            │
│                                         │
│ [Další →]                              │
└─────────────────────────────────────────┘
```

### Krok 2: Konfigurace [/Requests/New/Configure]
```
┌─────────────────────────────────────────┐
│      📸 PRODUKTOVÉ FOTO - KONFIGURACE   │
├─────────────────────────────────────────┤
│                                         │
│ 📁 Nahrát soubory:                     │
│ ┌─────────────────────────┐            │
│ │ [Drag & drop zone]      │            │
│ │    nebo klikněte        │            │
│ └─────────────────────────┘            │
│                                         │
│ ⚙️ Nastavení zpracování:               │
│ ☑ Odstranit pozadí                    │
│ ☑ Odstranit figurínu                  │
│ ☑ Vylepšit kvalitu                    │
│ ☑ Generovat popis                     │
│                                         │
│ 🎯 Cílová platforma:                   │
│ [Shopify ▼]                           │
│                                         │
│ [← Zpět] [Náhled workflow →]           │
└─────────────────────────────────────────┘
```

### Krok 3: Náhled workflow [/Requests/New/Preview]
```
┌─────────────────────────────────────────┐
│        📋 NÁHLED WORKFLOW               │
├─────────────────────────────────────────┤
│                                         │
│  1️⃣ ANALÝZA (paralelně)                │
│  ├─ 🔍 Detekce objektů                 │
│  ├─ 🏷️ Klasifikace                    │
│  └─ 📝 OCR                            │
│          ↓                             │
│  2️⃣ ZPRACOVÁNÍ (sekvenčně)             │
│  ├─ ✂️ Segmentace                      │
│  ├─ 🎨 Inpainting                     │
│  └─ ⚡ Enhancement                     │
│          ↓                             │
│  3️⃣ METADATA                           │
│  ├─ 🎨 Analýza barev                  │
│  └─ 📝 Generování popisu              │
│          ↓                             │
│  4️⃣ EXPORT                             │
│  └─ 📤 Upload do Shopify              │
│                                         │
│ Odhadovaný čas: ~3 minuty              │
│                                         │
│ [← Upravit] [Spustit zpracování →]    │
└─────────────────────────────────────────┘
```

### Krok 4: Sledování průběhu [/Requests/{id}/Progress]
```
┌─────────────────────────────────────────┐
│     🔄 ZPRACOVÁNÍ #REQ-2024-001         │
├─────────────────────────────────────────┤
│                                         │
│ Celkový postup: ████████░░ 73%         │
│                                         │
│ ✅ 1. ANALÝZA                          │
│    ├─ ✓ Detekce objektů (1.2s)        │
│    ├─ ✓ Klasifikace (0.8s)           │
│    └─ ✓ OCR (0.5s)                   │
│                                         │
│ 🔄 2. ZPRACOVÁNÍ                       │
│    ├─ ✓ Segmentace (2.1s)            │
│    ├─ ⏳ Inpainting (45%)             │
│    └─ ⏸️ Enhancement (čeká)           │
│                                         │
│ ⏸️ 3. METADATA                         │
│ ⏸️ 4. EXPORT                           │
│                                         │
│ 📊 Live metriky:                       │
│ CPU: 45% | GPU: 89% | RAM: 12GB       │
│                                         │
│ [Pozastavit] [Zrušit] [Zobrazit logy] │
└─────────────────────────────────────────┘
```

### Krok 5: Výsledky [/Requests/{id}/Results]
```
┌─────────────────────────────────────────┐
│       ✅ DOKONČENO #REQ-2024-001        │
├─────────────────────────────────────────┤
│                                         │
│ 🖼️ Zpracované fotky:                   │
│ ┌─────────┬─────────┬─────────┐       │
│ │ Before  │ After   │ Masks   │       │
│ │ [IMG]   │ [IMG]   │ [IMG]   │       │
│ └─────────┴─────────┴─────────┘       │
│                                         │
│ 📋 Extrahovaná metadata:               │
│ • Kategorie: Dámské šaty               │
│ • Barvy: Černá (85%), Bílá (15%)      │
│ • Velikost: M                          │
│ • Značka: H&M                          │
│                                         │
│ 📝 Vygenerovaný popis:                 │
│ "Elegantní černé šaty s bílými..."     │
│                                         │
│ ⏱️ Čas zpracování: 2:47                │
│ 💰 Náklady: $0.08                      │
│                                         │
│ [📥 Stáhnout] [📤 Odeslat] [🔄 Znovu] │
└─────────────────────────────────────────┘
```

## Databázový model

```sql
-- Hlavní tabulka požadavků
CREATE TABLE Requests (
    Id uniqueidentifier PRIMARY KEY,
    RequestType nvarchar(50), -- 'ProductPhoto', 'DocumentAnalysis', etc.
    Status nvarchar(50), -- 'New', 'Queued', 'Processing', 'Completed', 'Failed'
    Priority int,
    Deadline datetime2,
    WorkflowTemplateId uniqueidentifier,
    CreatedBy nvarchar(100),
    CreatedAt datetime2,
    UpdatedAt datetime2,
    CompletedAt datetime2,
    Metadata nvarchar(max) -- JSON
);

-- Workflow šablony
CREATE TABLE WorkflowTemplates (
    Id uniqueidentifier PRIMARY KEY,
    Name nvarchar(200),
    Description nvarchar(1000),
    RequestType nvarchar(50),
    Definition nvarchar(max), -- JSON s definicí workflow
    IsActive bit,
    Version int,
    CreatedAt datetime2,
    UpdatedAt datetime2
);

-- Kroky zpracování
CREATE TABLE RequestSteps (
    Id uniqueidentifier PRIMARY KEY,
    RequestId uniqueidentifier FOREIGN KEY,
    StepName nvarchar(200),
    ToolId nvarchar(100),
    Status nvarchar(50),
    StartedAt datetime2,
    CompletedAt datetime2,
    Duration int, -- ms
    Input nvarchar(max), -- JSON
    Output nvarchar(max), -- JSON
    Error nvarchar(max)
);

-- Výsledky
CREATE TABLE RequestResults (
    Id uniqueidentifier PRIMARY KEY,
    RequestId uniqueidentifier FOREIGN KEY,
    ResultType nvarchar(50),
    FileUrl nvarchar(500),
    Metadata nvarchar(max), -- JSON
    CreatedAt datetime2
);
```

## SignalR Events pro real-time updates

```csharp
public interface IRequestHub
{
    // Client -> Server
    Task SubscribeToRequest(string requestId);
    Task UnsubscribeFromRequest(string requestId);
    
    // Server -> Client
    Task OnRequestStatusChanged(RequestStatusUpdate update);
    Task OnStepStarted(StepProgressUpdate update);
    Task OnStepProgress(StepProgressUpdate update);
    Task OnStepCompleted(StepProgressUpdate update);
    Task OnRequestCompleted(RequestCompletedUpdate update);
    Task OnRequestFailed(RequestFailedUpdate update);
}
```

## Implementační priority

### Fáze 1: Základní flow (1-2 týdny)
1. Request management (CRUD)
2. Workflow templates
3. UI pro vytvoření a sledování požadavku
4. SignalR pro real-time updates

### Fáze 2: Orchestrace (2-3 týdny)
1. Integrace s existujícím ToolChainOrchestrator
2. Workflow engine
3. Error handling a retry logika
4. Monitoring a metriky

### Fáze 3: Specializované features (2-3 týdny)
1. Batch processing
2. Scheduling a prioritizace
3. Cost tracking
4. A/B testing workflow

### Fáze 4: Pokročilé UI (1-2 týdny)
1. Drag & drop workflow editor
2. Visual workflow designer
3. Advanced monitoring dashboard
4. Mobile responsive design

## Závěr

Tento design vytváří jednotný, transparentní systém pro zpracování jakýchkoliv požadavků. Uživatel vidí celý proces od začátku do konce, může sledovat průběh v reálném čase a má plnou kontrolu nad workflow.

Klíčové je, že struktura je univerzální - ať už zpracováváte produktové fotky, analyzujete dokumenty nebo provádíte jiné AI úlohy, flow zůstává stejné. To zajišťuje konzistentní uživatelskou zkušenost a snadnou škálovatelnost.