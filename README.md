# OptimalyAI

Moderní **production-ready** ASP.NET Core aplikace s Clean Architecture, Repository pattern, automatickou registrací služeb, AdminLTE UI, enterprise-grade funkcemi a pokročilou AI orchestrací s ReAct pattern.

## 🏗️ Clean Architecture

Podrobný popis architektury najdete v [Documentation/INFRASTRUCTURE_ARCHITECTURE.md](Documentation/INFRASTRUCTURE_ARCHITECTURE.md)

### Vrstvová struktura
```
OptimalyAI/                 # 🌐 Presentation Layer (Web + API)
├── Controllers/            # MVC a API controllery
├── ViewModels/            # ViewModels pro MVC views
├── Views/                 # Razor views s AdminLTE
├── Hubs/                  # SignalR hubs (real-time komunikace)
├── Extensions/            # Extension methods pro DI (Composition Root)
├── Configuration/         # Serilog, Swagger, Security config
├── Validation/           # FluentValidation components
└── wwwroot/              # AdminLTE + statické soubory

OAI.ServiceLayer/          # 💼 Business Logic Layer
├── Services/             # Business logika a orchestrátory
│   ├── AI/              # AI služby (Ollama, ConversationManager)
│   ├── Orchestration/   # AI orchestrátory s ReAct pattern
│   ├── Tools/           # AI nástroje a jejich implementace
│   ├── Workflow/        # Workflow management
│   └── Monitoring/      # Metriky a monitoring
├── Middleware/          # Middleware komponenty (GlobalExceptionHandler)
└── Interfaces/          # Service interfaces

OAI.Core/                 # 🎯 Domain + Data Contracts
├── Entities/            # Domain entity (BaseEntity)
├── Interfaces/          # Repository, UoW, Tool interfaces
├── DTOs/               # Data Transfer Objects
└── Enums/              # Domain enums

OAI.DataLayer/           # 🗄️ Data Access Layer
├── Context/            # Entity Framework DbContext
├── Repositories/       # Repository implementace
├── UnitOfWork/        # Unit of Work implementace
└── Migrations/        # EF Core migrace
```

## 🚨 DŮLEŽITÉ PRAVIDLA PRO CLAUDE CODE

### ❌ ZAKÁZANÉ AKCE
1. **NIKDY nevytvářej Infrastructure složku v root projektu!**
   - Infrastructure komponenty patří do OAI.ServiceLayer nebo OAI.DataLayer
   - Root projekt je jen Presentation Layer

2. **NIKDY neměň architekturu bez povolení!**
   - Services → OAI.ServiceLayer/Services/
   - Middleware → OAI.ServiceLayer/Middleware/
   - Repositories → OAI.DataLayer/Repositories/
   
3. **NIKDY nemíchej namespace z různých vrstev!**
   - `OptimalyAI.*` = Presentation Layer
   - `OAI.ServiceLayer.*` = Business Logic
   - `OAI.Core.*` = Domain + Contracts
   - `OAI.DataLayer.*` = Data Access

### ✅ POVINNÉ KONVENCE

#### **Přidávání nových služeb**
```csharp
// ✅ SPRÁVNĚ - do OAI.ServiceLayer/Services/
namespace OAI.ServiceLayer.Services
{
    public class MyNewService : IMyNewService
    {
        // implementace
    }
}

// ❌ ŠPATNĚ - do root projektu
namespace OptimalyAI.Services  // ZAKÁZÁNO!
```

#### **Přidávání middleware**
```csharp
// ✅ SPRÁVNĚ - do OAI.ServiceLayer/Middleware/
namespace OAI.ServiceLayer.Middleware
{
    public class MyMiddleware
    {
        // implementace
    }
}

// ❌ ŠPATNĚ - do root projektu
namespace OptimalyAI.Middleware  // ZAKÁZÁNO!
```

#### **Přidávání entit**
```csharp
// ✅ SPRÁVNĚ - do OAI.Core/Entities/
namespace OAI.Core.Entities
{
    public class MyEntity : BaseEntity
    {
        // implementace
    }
}
```

#### **Přidávání repository**
```csharp
// ✅ SPRÁVNĚ - do OAI.DataLayer/Repositories/
namespace OAI.DataLayer.Repositories
{
    public class MyRepository : Repository<MyEntity>, IMyRepository
    {
        // implementace
    }
}
```

### 🔍 KONTROLNÍ SEZNAM PŘED KAŽDOU ZMĚNOU

1. **Ověř vrstvu**: Kam komponenta skutečně patří podle Clean Architecture?
2. **Zkontroluj namespace**: Odpovídá namespace vrstvě?
3. **Najdi existující**: Existuje už podobná komponenta ve správné vrstvě?
4. **Dodržuj konvence**: Končí třídy správným suffixem (*Service, *Repository, *Mapper)?

## 🚀 Production-Ready Features

### ✅ **1. Global Exception Handling**
Centralizované zpracování všech výjimek s jednotnými API odpověďmi:

```csharp
// Middleware v OAI.ServiceLayer/Middleware/GlobalExceptionMiddleware.cs
public class GlobalExceptionMiddleware
{
    // Automatické zpracování všech chyb
    // ValidationException → HTTP 400 s detaily validace
    // NotFoundException → HTTP 404
    // BusinessException → HTTP 400
    // UnauthorizedAccessException → HTTP 401
    // Všechny ostatní → HTTP 500 (s detaily jen v Development)
}
```

### ✅ **2. FluentValidation**
Automatická validace s robustními pravidly:

```csharp
public class CreateUserDtoValidator : SimpleBaseValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email je povinný")
            .EmailAddress().WithMessage("Email není v platném formátu");
    }
}
// Automaticky se aplikuje na všechny API endpointy!
```

### ✅ **3. Serilog Structured Logging**
Pokročilé logování pro monitoring a debugging:

```csharp
_logger.LogInformation("Creating user {Email} with ID {UserId}", 
                      user.Email, user.Id);
```

**Log konfigurace:**
- **Console** (Development) - barevný výstup
- **File** - `logs/optimaly-ai-{date}.log` (rotace každý den, 30 dní retention)
- **Structured** - JSON formát připravený pro ELK/Splunk

### ✅ **4. Clean Architecture s DDD**
Správné oddělení zodpovědností:

- **Presentation Layer** (OptimalyAI.*) - Controllers, Views, Hubs, Extensions
- **Business Logic** (OAI.ServiceLayer.*) - Services, Orchestrators, Tools, Middleware  
- **Domain + Contracts** (OAI.Core.*) - Entities, DTOs, Interfaces
- **Data Access** (OAI.DataLayer.*) - DbContext, Repositories, Migrations

### ✅ **5. AI Orchestration s ReAct Pattern**
Pokročilá AI orchestrace s multi-step reasoning:

```csharp
// ReAct (Reasoning + Acting) Pattern
var request = new ConversationOrchestratorRequestDto
{
    Message = "Najdi informace o počasí v Praze a porovnej s Brnem",
    EnableTools = true,
    Metadata = new Dictionary<string, object>
    {
        ["enable_react"] = true  // Aktivuje ReAct pattern
    }
};

// ReAct provede:
// 1. Thought: "Potřebuji získat počasí pro Prahu"
// 2. Action: web_search("počasí Praha")
// 3. Observation: "Praha: 15°C, oblačno"
// 4. Thought: "Teď potřebuji počasí pro Brno"
// 5. Action: web_search("počasí Brno")
// 6. Observation: "Brno: 17°C, slunečno"
// 7. Thought: "Mohu porovnat obě města"
// 8. Final Answer: "V Praze je 15°C a oblačno, zatímco v Brně je tepleji..."
```

### ✅ **6. AI Tools Infrastructure**
Robustní systém pro integraci a správu AI nástrojů:

```csharp
// Automatická registrace nástrojů při startu
public class MyCustomTool : ITool
{
    public string Id => "my_custom_tool";
    public string Name => "My Custom Tool";
    public string Category => "Custom";
    
    public async Task<IToolResult> ExecuteAsync(
        Dictionary<string, object> parameters, 
        CancellationToken cancellationToken)
    {
        // Implementace nástroje
    }
}
// Nástroj bude automaticky zaregistrován a dostupný!
```

**Integrované nástroje:**
- **Web Search** - DuckDuckGo API pro vyhledávání
- **LLM Tornado** - Přístup k různým LLM poskytovatelům
- **Firecrawl** - Web scraping
- **Jina Reader** - Čtení a zpracování dokumentů

## 🛠️ Vývoj s Claude Code

### ✅ SPRÁVNÝ POSTUP pro novou funkcionalitu

**1. Vytvořte entitu (OAI.Core/Entities/):**
```csharp
namespace OAI.Core.Entities
{
    public class Product : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }
    }
}
```

**2. Vytvořte DTOs (OAI.Core/DTOs/):**
```csharp
namespace OAI.Core.DTOs
{
    public class ProductDto : BaseDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class CreateProductDto : CreateDtoBase
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
```

**3. Vytvořte Validator (OptimalyAI/Validation/):**
```csharp
namespace OptimalyAI.Validation
{
    public class CreateProductDtoValidator : SimpleBaseValidator<CreateProductDto>
    {
        public CreateProductDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název produktu je povinný");
        }
    }
}
```

**4. Vytvořte Service (OAI.ServiceLayer/Services/):**
```csharp
namespace OAI.ServiceLayer.Services
{
    public class ProductService : BaseService<Product>, IProductService
    {
        public ProductService(
            IRepository<Product> repository, 
            IUnitOfWork unitOfWork,
            ILogger<ProductService> logger) : base(repository, unitOfWork)
        {
            // implementace
        }
    }

    public interface IProductService : IBaseService<Product>
    {
        // vlastní metody
    }
}
```

**5. Vytvořte Controller (OptimalyAI/Controllers/):**
```csharp
namespace OptimalyAI.Controllers
{
    [Route("api/[controller]")]
    public class ProductsController : BaseApiController
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<ProductDto>>>> GetProducts()
        {
            // implementace
        }
    }
}
```

### 🎯 Automatické konvence (dodržuj!)

- **Tabulky**: User → Users (množné číslo)
- **Services**: končí na "Service" a implementují I*Service
- **Repositories**: končí na "Repository" a implementují I*Repository  
- **Validators**: končí na "Validator" a dědí z SimpleBaseValidator<T>
- **Mappers**: končí na "Mapper" a implementují I*Mapper
- **Tools**: končí na "Tool" a implementují ITool
- **CreatedAt/UpdatedAt**: nastavuje se automaticky
- **Validace**: aplikuje se automaticky na všechny API endpointy

## 🗄️ Databáze

### Konfigurace
- **Development**: Uses In-Memory database (no local database needed)
- **Production**: Uses PostgreSQL via Docker
- **NEVER** use LocalDB, SQLite, or any local file-based database
- Migrations are created but not applied in development (In-Memory DB recreates on each run)

### Switching Between Databases
- Set `"UseProductionDatabase": true` in appsettings.json for PostgreSQL
- Set `"UseProductionDatabase": false` in appsettings.json for In-Memory (default)

### PostgreSQL with Docker
```bash
# Start PostgreSQL
./docker-db-start.sh

# Stop PostgreSQL  
./docker-db-stop.sh

# Apply migrations
dotnet ef database update
```

### Connection Details
- Host: localhost
- Port: 5432  
- Database: optimalyai_db
- Username: optimaly
- Password: OptimalyAI2024!

## 🚀 Spuštění

### Development Commands
```bash
# Build and run (ALWAYS use Python script!)
python run-dev.py          # Restartuje a spustí aplikaci v pozadí
python run-dev.py status   # Zobrazí status aplikace
python run-dev.py logs     # Zobrazí logy
python run-dev.py stop     # Zastaví aplikaci
python run-dev.py restart  # Restartuje aplikaci

# Direct dotnet commands (pouze pokud Python nefunguje)
dotnet build                                    # Build all projects
dotnet run --project OptimalyAI.csproj         # Run the web application
dotnet watch run --project OptimalyAI.csproj   # Run with hot reload
```

### Důležité URL adresy
- 🏠 **Dashboard**: `https://localhost:5005/`
- 🤖 **AI Chat**: `https://localhost:5005/Chat`
- 🛠️ **AI Tools**: `https://localhost:5005/Tools`
- 🎯 **Orchestrators**: `https://localhost:5005/Orchestrators`
- 📊 **API Documentation**: `https://localhost:5005/api/docs`
- 📝 **Logs**: `logs/optimaly-ai-{date}.log`

## 🔧 Pravidla pro development

### ❌ CO NEDĚLAT
1. **Nevytvářej Infrastructure složku v root projektu**
2. **Nemiž components mezi vrstvami bez důvodu**
3. **Neporušuj namespace konvence**
4. **Nevkládej business logiku do Controllers**
5. **Neobcházej validace a exception handling**

### ✅ CO DĚLAT
1. **Vždy kontroluj, do které vrstvy komponenta patří**
2. **Dodržuj Clean Architecture principy**
3. **Používej připravené base classes (BaseEntity, BaseService, atd.)**
4. **Přidávaj structured logging všude**
5. **Testuj funkcionalita přes `/Tools` UI**

## 📁 Kompletní struktura

```
OptimalyAI/                           # 🌐 PRESENTATION LAYER
├── Controllers/                      # API a MVC controllers
├── Views/                           # Razor views s AdminLTE
├── Hubs/                            # SignalR hubs
├── Extensions/                      # DI konfigurace (Composition Root)
├── Configuration/                   # App konfigurace
├── Validation/                      # FluentValidation
├── wwwroot/                         # Static files
└── Program.cs                       # Entry point

OAI.ServiceLayer/                    # 💼 BUSINESS LOGIC LAYER
├── Services/                        # Business services
│   ├── AI/                         # AI services (Ollama, ConversationManager)
│   ├── Orchestration/              # AI orchestrators s ReAct pattern
│   ├── Tools/                      # AI tools (WebSearch, LLMTornado)
│   ├── Workflow/                   # Workflow management
│   └── Monitoring/                 # Metrics a monitoring
├── Middleware/                      # Business middleware
└── Interfaces/                      # Service contracts

OAI.Core/                           # 🎯 DOMAIN + CONTRACTS
├── Entities/                       # Domain entities
├── DTOs/                          # Data transfer objects
├── Interfaces/                     # Abstrakce (Repository, UoW, Tools)
└── Enums/                         # Domain enums

OAI.DataLayer/                      # 🗄️ DATA ACCESS LAYER
├── Context/                        # EF DbContext
├── Repositories/                   # Repository implementations
├── UnitOfWork/                     # UoW implementation
└── Migrations/                     # EF migrations
```

## 🏆 Production-Ready Status

### ✅ **Implementováno**
1. ✅ **Clean Architecture** - správně oddělené vrstvy
2. ✅ **Global Exception Handling** - centralizované error handling
3. ✅ **Structured Logging** - Serilog s file rotation
4. ✅ **Input Validation** - FluentValidation s automatickou aplikací
5. ✅ **API Documentation** - Swagger/OpenAPI s XML komentáři
6. ✅ **Security Headers** - XSS, CSRF, CSP protection
7. ✅ **Repository Pattern** - abstrakce nad datovou vrstvou
8. ✅ **DTO Pattern** - čisté API rozhraní
9. ✅ **AI Orchestration** - ConversationOrchestrator s ReAct pattern
10. ✅ **AI Tools Infrastructure** - rozšiřitelný systém pro AI nástroje
11. ✅ **PostgreSQL/In-Memory DB** - flexibilní databázová konfigurace
12. ✅ **AdminLTE UI** - profesionální admin rozhraní

### 🔄 **Připraveno k implementaci**
- Health Checks pro monitoring
- Caching layer (Redis/Memory)  
- Background Services (Hangfire)
- Authentication/Authorization (JWT)
- Unit a Integration testy

---

**🤖 Vytvořeno pro efektivní a bezpečný vývoj s AI asistenty jako Claude Code**

**🏗️ Clean Architecture | 🔒 Production-Ready | 🤖 AI-Powered**

**Status: ✅ Production Ready** 🚀