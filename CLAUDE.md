# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 🚨 CRITICAL ARCHITECTURE RULES

### ❌ FORBIDDEN ACTIONS
1. **NEVER create Infrastructure folder in root project!**
   - Infrastructure components belong to OAI.ServiceLayer or OAI.DataLayer
   - Root project is ONLY Presentation Layer

2. **NEVER change architecture without permission!**
   - Services → OAI.ServiceLayer/Services/
   - Middleware → OAI.ServiceLayer/Middleware/
   - Repositories → OAI.DataLayer/Repositories/
   
3. **NEVER mix namespaces from different layers!**
   - `OptimalyAI.*` = Presentation Layer ONLY
   - `OAI.ServiceLayer.*` = Business Logic ONLY
   - `OAI.Core.*` = Domain + Contracts ONLY
   - `OAI.DataLayer.*` = Data Access ONLY

### ✅ MANDATORY CONVENTIONS

#### **Adding new services**
```csharp
// ✅ CORRECT - to OAI.ServiceLayer/Services/
namespace OAI.ServiceLayer.Services
{
    public class MyNewService : IMyNewService
    {
        // implementation
    }
}

// ❌ WRONG - to root project
namespace OptimalyAI.Services  // FORBIDDEN!
```

#### **Adding middleware**
```csharp
// ✅ CORRECT - to OAI.ServiceLayer/Middleware/
namespace OAI.ServiceLayer.Middleware
{
    public class MyMiddleware
    {
        // implementation
    }
}

// ❌ WRONG - to root project
namespace OptimalyAI.Middleware  // FORBIDDEN!
```

#### **Adding entities**
```csharp
// ✅ CORRECT - to OAI.Core/Entities/
namespace OAI.Core.Entities
{
    public class MyEntity : BaseEntity
    {
        // implementation
    }
}
```

#### **Adding repositories**
```csharp
// ✅ CORRECT - to OAI.DataLayer/Repositories/
namespace OAI.DataLayer.Repositories
{
    public class MyRepository : Repository<MyEntity>, IMyRepository
    {
        // implementation
    }
}
```

### 🔍 CHECKLIST BEFORE EVERY CHANGE

1. **Verify layer**: Where does the component truly belong according to Clean Architecture?
2. **Check namespace**: Does the namespace match the layer?
3. **Find existing**: Does a similar component already exist in the correct layer?
4. **Follow conventions**: Do classes end with the correct suffix (*Service, *Repository, *Mapper)?

## 🏗️ Clean Architecture Layers

### OptimalyAI/ (🌐 Presentation Layer)
```
OptimalyAI/                 # PRESENTATION LAYER ONLY
├── Controllers/            # API and MVC controllers
├── Views/                 # Razor views with AdminLTE
├── Hubs/                  # SignalR hubs (real-time communication)
├── Extensions/            # DI configuration (Composition Root)
├── Configuration/         # App configuration
├── Validation/           # FluentValidation components
└── wwwroot/              # Static files
```

### OAI.ServiceLayer/ (💼 Business Logic Layer)
```
OAI.ServiceLayer/          # BUSINESS LOGIC LAYER
├── Services/             # Business services
│   ├── AI/              # AI services (Ollama, ConversationManager)
│   ├── Orchestration/   # AI orchestrators with ReAct pattern
│   ├── Tools/           # AI tools and implementations
│   ├── Workflow/        # Workflow management
│   └── Monitoring/      # Metrics and monitoring
├── Middleware/          # Business middleware (GlobalExceptionHandler)
└── Interfaces/          # Service contracts
```

### OAI.Core/ (🎯 Domain + Contracts)
```
OAI.Core/                 # DOMAIN + CONTRACTS
├── Entities/            # Domain entities
├── DTOs/               # Data transfer objects
├── Interfaces/         # Abstractions (Repository, UoW, Tools)
└── Enums/             # Domain enums
```

### OAI.DataLayer/ (🗄️ Data Access Layer)
```
OAI.DataLayer/           # DATA ACCESS LAYER
├── Context/            # EF DbContext
├── Repositories/       # Repository implementations
├── UnitOfWork/        # UoW implementation
└── Migrations/        # EF migrations
```

## Database Configuration
- **All environments**: Uses PostgreSQL via Docker
- **NEVER** use LocalDB, SQLite, or any file-based database
- Always apply migrations when creating new entities or modifying existing ones

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

## Key URLs and Pages

- **Dashboard**: `https://localhost:5005/`
- **AI Tools UI**: `https://localhost:5005/Tools` - Interactive testing of AI tools
- **AI Tools API**: `https://localhost:5005/api/tools` - REST API for tools
- **Swagger Docs**: `https://localhost:5005/api/docs` - API documentation
- **Ollama Models**: `https://localhost:5005/Models` - AI model management
- **Chat Interface**: `https://localhost:5005/Chat` - AI chat conversations

## Development Commands

### Build and Run
```bash
# Development mode with hot reload (ALWAYS USE THIS!)
python run-dev.py

# Direct dotnet commands (only if Python doesn't work)
dotnet build                                    # Build all projects
dotnet run --project OptimalyAI.csproj         # Run the web application
dotnet watch run --project OptimalyAI.csproj   # Run with hot reload
```

### Build Guidance
- **ALWAYS build exclusively through Python** (dělej build výhradně přes python)
- Use `python run-dev.py` for all development work

### Ports and URLs
- Application runs on `https://localhost:5005`
- Swagger documentation: `https://localhost:5005/api/docs`
- Logs are written to: `logs/optimaly-ai-{date}.log`

## Razor Guidance
- **Always carefully check Razor syntax** to prevent syntax errors (Vždy pečlivě kontroluj syntaxi Razor, aby jsi předešel syntaktickým chybám)

## Browser Testing Tools

### Visual Testing and Verification
Pro vizuální ověření změn v prohlížeči používej následující nástroje:

1. **Screenshot capture**:
```bash
# Celá obrazovka
screencapture -x screenshot.png

# Konkrétní okno (interaktivní)
screencapture -w screenshot.png

# S časovým zpožděním
sleep 3 && screencapture -w screenshot.png
```

2. **Otevření URL v prohlížeči**:
```bash
open "https://localhost:5005/Projects/Create"
```

3. **Zobrazení screenshotu**:
```
# Použij Read tool pro zobrazení PNG souborů
Read file_path="/tmp/screenshot.png"
```

### Browser Inspector Tools
V adresáři `Tools/` jsou k dispozici pokročilé nástroje pro inspekci prohlížeče:

- **browser-inspector.js** - JavaScript knihovna pro zachytávání console logů, network requestů, chyb a DOM stavu
- **browser-test.sh** - Kompletní testovací nástroj s reportováním
- **analyze-browser-state.py** - Analyzátor exportovaných dat z prohlížeče

### Příklad použití pro testování
```bash
# 1. Otevřít stránku
open "https://localhost:5005/Projects/Create?customerId=19513ef7-90e5-4c5a-ac58-02684a445b5a"

# 2. Počkat na načtení a udělat screenshot
sleep 3 && screencapture -w /tmp/test.png

# 3. Zobrazit screenshot pomocí Read tool
Read file_path="/tmp/test.png"
```

## ✅ CORRECT Development Workflow

### Creating New Functionality

**1. Create Entity (OAI.Core/Entities/):**
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

**2. Create DTOs (OAI.Core/DTOs/):**
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

**3. Create Validator (OptimalyAI/Validation/):**
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

**4. Create Service (OAI.ServiceLayer/Services/):**
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
            // implementation
        }
    }

    public interface IProductService : IBaseService<Product>
    {
        // custom methods
    }
}
```

**5. Create Controller (OptimalyAI/Controllers/):**
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
            // implementation
        }
    }
}
```

## 🎯 Automatic Conventions (FOLLOW THESE!)

- **Tables**: User → Users (plural)
- **Services**: end with "Service" and implement I*Service
- **Repositories**: end with "Repository" and implement I*Repository  
- **Validators**: end with "Validator" and inherit from SimpleBaseValidator<T>
- **Mappers**: end with "Mapper" and implement I*Mapper
- **Tools**: end with "Tool" and implement ITool
- **CreatedAt/UpdatedAt**: set automatically
- **Validation**: applied automatically to all API endpoints

## Testing Workflow
1. Make changes in code
2. Run application: `python run-dev.py`
3. Open URL in browser: `open "https://localhost:5005/..."`
4. Take screenshot: `screencapture -w /tmp/test.png`
5. View result: `Read file_path="/tmp/test.png"`
6. Commit changes with descriptive commit message

## Recent Fixes and Features

### Customer Pre-selection in Projects (Fixed 14.6.2025)
- Při vytváření projektu ze stránky detailu zákazníka se nyní zákazník automaticky předvyplní
- Opravený JavaScript kód v `Views/Projects/Create.cshtml` správně nastavuje hodnotu v Select2 dropdownu
- Funguje stejně jako u vytváření požadavků

### Known Issues and Solutions

1. **JavaScript syntax errors v Razor views**:
   - Vždy používej `@Html.Raw(System.Text.Json.JsonSerializer.Serialize())` pro escapování C# hodnot v JavaScriptu
   - Nikdy nevkládej `@Model` properties přímo do JavaScript kódu

2. **Build errors**:
   - Pokud aplikace nechce naběhnout, zkontroluj logy: `tail -50 logs/dev-runner-*.log`
   - Časté chyby: chybějící properties v DTO, syntaktické chyby v Razor

## ❌ WHAT NOT TO DO
1. **Don't create Infrastructure folder in root project**
2. **Don't move components between layers without reason**
3. **Don't violate namespace conventions**
4. **Don't put business logic in Controllers**
5. **Don't bypass validation and exception handling**
6. **Don't create files unless absolutely necessary**
7. **Don't create documentation files (*.md) unless explicitly requested**

## ✅ WHAT TO DO
1. **Always check which layer the component belongs to**
2. **Follow Clean Architecture principles**
3. **Use prepared base classes (BaseEntity, BaseService, etc.)**
4. **Add structured logging everywhere**
5. **Test functionality through `/Tools` UI**
6. **ALWAYS prefer editing existing files to creating new ones**

## Important Instruction Reminders
- Do what has been asked; nothing more, nothing less
- NEVER create files unless they're absolutely necessary for achieving your goal
- ALWAYS prefer editing an existing file to creating a new one
- NEVER proactively create documentation files (*.md) or README files unless explicitly requested

## Git and Commit Guidelines
- nikdy do komitu nedávej obrázky a nepiš tam že jsi to generoval ty
- (never add images to commits and don't write that you generated it)