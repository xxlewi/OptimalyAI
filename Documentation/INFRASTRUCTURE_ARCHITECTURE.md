# 🏗️ INFRASTRUCTURE ARCHITECTURE - CLEAN ARCHITECTURE GUIDE

## 🚨 CRITICAL RULES FOR CLAUDE CODE

### ❌ ABSOLUTE PROHIBITIONS

1. **NEVER CREATE `Infrastructure/` FOLDER IN ROOT PROJECT**
   ```
   OptimalyAI/
   ├── Infrastructure/  ❌ FORBIDDEN! NEVER CREATE THIS!
   ```

2. **NEVER MIX LAYER NAMESPACES**
   ```csharp
   // ❌ WRONG - mixing layers
   namespace OptimalyAI.Services          // FORBIDDEN!
   namespace OptimalyAI.Infrastructure    // FORBIDDEN!
   namespace OptimalyAI.Repositories      // FORBIDDEN!
   ```

3. **NEVER MOVE COMPONENTS WITHOUT CHECKING LAYER**
   - Always ask: "Which layer does this TRULY belong to?"
   - Infrastructure ≠ Business Logic ≠ Presentation

## ✅ CORRECT CLEAN ARCHITECTURE

### 🎯 Layer Definition & Responsibilities

#### 1. OptimalyAI/ (🌐 Presentation Layer)
```
OptimalyAI/                    # PRESENTATION LAYER ONLY
├── Controllers/               # API & MVC endpoints
├── Views/                    # Razor views + AdminLTE
├── Hubs/                     # SignalR real-time communication
├── Extensions/               # DI configuration (Composition Root)
│   ├── ServiceCollectionExtensions.cs  # DI setup
│   ├── ApplicationBuilderExtensions.cs # Middleware pipeline
│   └── ConfigurationExtensions.cs      # Config helpers
├── Configuration/            # App-specific config
├── Validation/              # FluentValidation (input validation)
└── wwwroot/                 # Static files
```

**What belongs here:**
- Web controllers and API endpoints
- Razor views and view models
- SignalR hubs for real-time features
- Extension methods for app startup
- Input validation (FluentValidation)
- Static web assets

**Namespace:** `OptimalyAI.*`

#### 2. OAI.ServiceLayer/ (💼 Business Logic Layer)
```
OAI.ServiceLayer/             # BUSINESS LOGIC LAYER
├── Services/                 # Core business services
│   ├── AI/                  # AI-specific services
│   ├── Orchestration/       # Business orchestrators
│   ├── Tools/              # Business tools
│   ├── Workflow/           # Workflow management
│   └── Monitoring/         # Business metrics
├── Middleware/              # Cross-cutting concerns
│   └── GlobalExceptionMiddleware.cs  # Exception handling
└── Interfaces/              # Service contracts
```

**What belongs here:**
- Business logic and domain services
- Application services and orchestrators
- AI tools and integrations
- Cross-cutting concerns (exception handling, logging)
- Business workflows and processes
- Service interfaces and contracts

**Namespace:** `OAI.ServiceLayer.*`

#### 3. OAI.Core/ (🎯 Domain + Contracts)
```
OAI.Core/                    # DOMAIN + CONTRACTS LAYER
├── Entities/                # Domain entities
├── DTOs/                   # Data transfer objects
├── Interfaces/             # Core abstractions
│   ├── IRepository.cs      # Data access contracts
│   ├── IUnitOfWork.cs     # Transaction contracts
│   └── Tools/             # Tool abstractions
└── Enums/                 # Domain enumerations
```

**What belongs here:**
- Domain entities and value objects
- Data transfer objects (DTOs)
- Repository and Unit of Work interfaces
- Tool and service interfaces
- Domain enums and constants

**Namespace:** `OAI.Core.*`

#### 4. OAI.DataLayer/ (🗄️ Data Access Layer)
```
OAI.DataLayer/              # DATA ACCESS LAYER
├── Context/                # Entity Framework context
│   ├── AppDbContext.cs     # Main DbContext
│   └── DesignTimeDbContextFactory.cs  # For migrations
├── Repositories/           # Data access implementations
│   ├── Repository.cs       # Generic repository for int ID entities
│   └── GuidRepository.cs   # Generic repository for Guid ID entities
├── UnitOfWork/            # Transaction implementations
│   └── UnitOfWork.cs      # Transaction management + repository factory
├── Extensions/            # Data layer extensions
│   └── DbContextExtensions.cs  # Entity configuration helpers
└── Migrations/            # Database migrations
```

**What belongs here:**
- Entity Framework DbContext
- Repository pattern implementations
- Unit of Work implementations
- Database migrations and configurations
- Data access logic
- NO business logic or validation
- NO calculations or business rules

**Key Features:**
- ✅ Generic repository pattern for both int and Guid primary keys
- ✅ Unit of Work with transaction support
- ✅ Automatic entity registration via reflection
- ✅ Automatic timestamp management (CreatedAt/UpdatedAt)
- ✅ Support for multiple database providers (PostgreSQL default)
- ✅ Proper use of ILogger instead of Console.WriteLine

**Namespace:** `OAI.DataLayer.*`

## 🔍 COMPONENT CLASSIFICATION GUIDE

### How to Determine Component Layer

#### Ask These Questions:
1. **Is it UI/Web related?** → `OptimalyAI/` (Presentation)
2. **Is it business logic?** → `OAI.ServiceLayer/` (Business)
3. **Is it domain model/contract?** → `OAI.Core/` (Domain)
4. **Is it data access?** → `OAI.DataLayer/` (Data)

#### Examples:

**Controllers** → `OptimalyAI/Controllers/`
- Handle HTTP requests/responses
- Coordinate between services
- Input validation and output formatting

**Services** → `OAI.ServiceLayer/Services/`
- Business logic and rules
- Application workflows
- Cross-service coordination

**Middleware** → `OAI.ServiceLayer/Middleware/`
- Cross-cutting concerns
- Request/response processing
- Exception handling

**Entities** → `OAI.Core/Entities/`
- Domain models
- Business rules and invariants

**Repositories** → `OAI.DataLayer/Repositories/`
- Data access and persistence
- Database operations

## 🛠️ DEVELOPMENT RULES

### ✅ CORRECT Component Addition

#### Adding a Service:
```csharp
// ✅ CORRECT
namespace OAI.ServiceLayer.Services
{
    public class PaymentService : IPaymentService
    {
        // Business logic for payments
    }
}
```

#### Adding a Repository:
```csharp
// ✅ CORRECT
namespace OAI.DataLayer.Repositories
{
    public class PaymentRepository : Repository<Payment>, IPaymentRepository
    {
        // Data access for payments
    }
}
```

#### Adding a Controller:
```csharp
// ✅ CORRECT
namespace OptimalyAI.Controllers
{
    public class PaymentsController : BaseApiController
    {
        // HTTP endpoints for payments
    }
}
```

### ❌ INCORRECT Component Addition

```csharp
// ❌ WRONG - Services in presentation layer
namespace OptimalyAI.Services
{
    public class PaymentService // FORBIDDEN!
}

// ❌ WRONG - Infrastructure in root
namespace OptimalyAI.Infrastructure
{
    public class PaymentRepository // FORBIDDEN!
}
```

## 🔄 DEPENDENCY FLOW

### Correct Dependency Direction:
```
Presentation → Business Logic → Domain ← Data Access
     ↓              ↓             ↑          ↑
OptimalyAI → OAI.ServiceLayer → OAI.Core ← OAI.DataLayer
```

### Dependency Rules:
- **Presentation** can depend on **Business Logic** and **Domain**
- **Business Logic** can depend on **Domain** only
- **Data Access** can depend on **Domain** only
- **Domain** depends on nothing (pure domain logic)

## 🚨 EMERGENCY CHECKLIST

### Before Adding ANY Component:

1. **🔍 Identify Purpose:**
   - What does this component DO?
   - Is it UI, business logic, domain model, or data access?

2. **📍 Determine Layer:**
   - UI/Web concerns → `OptimalyAI/`
   - Business logic → `OAI.ServiceLayer/`
   - Domain models → `OAI.Core/`
   - Data access → `OAI.DataLayer/`

3. **✅ Check Namespace:**
   - Does the namespace match the layer?
   - `OptimalyAI.*` = Presentation only
   - `OAI.ServiceLayer.*` = Business only
   - `OAI.Core.*` = Domain only
   - `OAI.DataLayer.*` = Data only

4. **🔄 Verify Dependencies:**
   - Is the dependency flow correct?
   - No circular dependencies?
   - Following Clean Architecture rules?

### Red Flags (STOP AND RECONSIDER):
- Creating `Infrastructure/` in root project
- Mixing layer namespaces
- Putting business logic in controllers
- Putting UI logic in services
- Creating circular dependencies

## 📋 ARCHITECTURE VIOLATIONS TO WATCH FOR

### Common Mistakes:
1. **Infrastructure Confusion:** 
   - Infrastructure ≠ catch-all folder
   - True infrastructure = external concerns (databases, APIs, file systems)

2. **Layer Mixing:**
   - Controllers with business logic
   - Services with data access code
   - Entities with UI concerns

3. **Namespace Violations:**
   - `OptimalyAI.Services` (should be `OAI.ServiceLayer.Services`)
   - `OptimalyAI.Infrastructure` (forbidden completely)

4. **Dependency Inversions:**
   - Domain depending on data access
   - Business logic depending on presentation

## 🎯 ARCHITECTURE BENEFITS

### Why This Structure Matters:
- **Testability:** Each layer can be tested independently
- **Maintainability:** Clear separation of concerns
- **Scalability:** Easy to modify and extend
- **Team Collaboration:** Clear ownership boundaries
- **Deployment Flexibility:** Layers can be deployed separately

### Clean Architecture Principles:
- **Independence:** Layers don't depend on implementation details
- **Testable:** Business logic can be tested without UI or database
- **Framework Independent:** Not tied to specific technologies
- **Database Independent:** Can swap data stores easily
- **UI Independent:** Business logic doesn't know about UI

## 📊 DATA LAYER QUALITY METRICS

### Clean Architecture Compliance: 95/100 🏆

**Strengths:**
- ✅ Proper dependency flow (only depends on OAI.Core)
- ✅ No business logic leakage
- ✅ Clean repository and UoW implementations
- ✅ Automatic entity configuration
- ✅ Support for multiple database providers

**Recent Improvements:**
- ✅ Replaced Console.WriteLine with ILogger
- ✅ Fixed duplicate DefaultModelId column in migrations
- ✅ Corrected foreign key type mismatches (Guid → int)

---

**🚨 REMEMBER: When in doubt, ask "What is the PRIMARY responsibility of this component?" and place it in the appropriate layer.**

**🏗️ Clean Architecture is not about perfect folder organization - it's about dependency management and separation of concerns!**