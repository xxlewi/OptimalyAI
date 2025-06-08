# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Build and Run
```bash
# Development mode with hot reload
python run-dev.py

# Direct dotnet commands
dotnet build                                    # Build all projects
dotnet run --project OptimalyAI.csproj         # Run the web application
dotnet watch run --project OptimalyAI.csproj   # Run with hot reload
```

### Build Guidance
- dělej buil výhradně přes pytho (always build exclusively through Python)

### Ports and URLs
- Application runs on `https://localhost:5005`
- Swagger documentation: `https://localhost:5005/api/docs`
- Logs are written to: `logs/optimaly-ai-{date}.log`

## Architecture Overview

This is a **3-layer ASP.NET Core application** with automatic service registration and enterprise features:

### Layer Structure
1. **OptimalyAI** (Presentation Layer)
   - Web API controllers inheriting from `BaseApiController`
   - MVC views using AdminLTE UI framework
   - Global exception handling via middleware
   - Automatic FluentValidation on all API endpoints

2. **OAI.ServiceLayer** (Business Logic Layer)
   - Services inherit from `BaseService<T>` with CRUD operations
   - Repository and Unit of Work pattern implementations
   - Mapping between entities and DTOs

3. **OAI.Core** (Domain Layer)
   - All entities inherit from `BaseEntity` (Id, CreatedAt, UpdatedAt)
   - DTOs inherit from `BaseDto`, `CreateDtoBase`, or `UpdateDtoBase`
   - Repository and mapping interfaces

### Key Architectural Patterns

#### Automatic Service Registration
Everything is registered automatically in `Program.cs` via:
```csharp
builder.Services.AddOptimalyAI(builder.Configuration);
```

This automatically registers:
- All classes ending with "Service" → Scoped
- All classes ending with "Mapper" → Singleton
- All FluentValidation validators → Scoped
- Repository pattern for all entities
- DbContext with automatic entity discovery

#### Entity Framework Convention
- Entities inheriting from `BaseEntity` are auto-discovered
- Tables named as entity plural (User → Users)
- CreatedAt/UpdatedAt handled automatically
- Indexes created on timestamp fields

#### API Response Pattern
All API responses use standardized format:
```csharp
return Ok(data, "Success message");  // Returns ApiResponse<T>
throw new NotFoundException("Entity", id);  // Returns 404 with ApiResponse
```

#### Global Exception Handling
Exceptions are automatically converted to appropriate HTTP responses:
- `ValidationException` → 400 with validation details
- `NotFoundException` → 404
- `BusinessException` → 400
- `UnauthorizedAccessException` → 401
- Others → 500 (with details only in Development)

## Creating New Features

When adding new functionality, follow this pattern:

1. **Create Entity** in `OAI.Core/Entities/`
   - Must inherit from `BaseEntity`
   - Use data annotations for validation

2. **Create DTOs** in `OAI.Core/DTOs/`
   - Response DTO inherits from `BaseDto`
   - Create DTO inherits from `CreateDtoBase`
   - Update DTO inherits from `UpdateDtoBase`

3. **Create Validator** in `Validation/`
   - Name must end with "Validator"
   - Inherit from `SimpleBaseValidator<T>`

4. **Create Mapper** in `OAI.ServiceLayer/Mapping/`
   - Name must end with "Mapper"
   - Inherit from `BaseMapper<TEntity, TDto>`
   - Create corresponding interface

5. **Create Service** in `OAI.ServiceLayer/Services/`
   - Name must end with "Service"
   - Inherit from `BaseService<TEntity>`
   - Create corresponding interface

6. **Create Controller** in `Controllers/`
   - API controllers inherit from `BaseApiController`
   - Use XML documentation for Swagger
   - Use `ProducesResponseType` attributes

## Important Conventions

- **No manual DI registration needed** - use naming conventions
- **No manual DbSet registration** - entities are auto-discovered
- **Always use structured logging** with parameters, not string interpolation
- **Always throw specific exceptions** that map to HTTP status codes
- **Always use DTOs** for API input/output, never expose entities directly
- **Test framework not configured** - ask user for test commands if needed

## Security Features

The application includes:
- CORS configuration (check `appsettings.json`)
- Rate limiting (100 req/min general, 60 req/min for API)
- Security headers automatically applied
- Structured logging with Serilog
- Global exception handling