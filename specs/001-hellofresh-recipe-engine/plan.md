# Implementation Plan: Multi-Provider Recipe Engine with Database-Driven Configuration

**Branch**: `001-hellofresh-recipe-engine` | **Date**: November 2, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-hellofresh-recipe-engine/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Complete the Recipe Engine implementation with multi-provider extensibility. The system processes recipes in configurable batches (up to 100 recipes or 1 hour window), respecting provider rate limits through stealth measures (randomized delays, rotating user agents, connection pooling). Key features: ingredient normalization (proprietary codes to canonical forms), **database-driven configuration** (provider URLs, rate limits, and TOS-sensitive settings stored in MongoDB, not appsettings.json), completed Saga orchestration (discovery → fingerprinting → processing → persistence) with state persistence for crash recovery, and configurable discovery strategies (static crawl, dynamic crawl, API-based).

**Technical approach**: Domain-Driven Design with Saga pattern for workflow orchestration, Strategy pattern for pluggable discovery implementations, MongoDB via EasyMeals.Shared.Data for persistence (including ProviderConfigurationDocument collection), Docker containerization for Coolify deployment.

**Security Note**: All provider-specific data (URLs, rate limits, proprietary ingredient codes) are stored in MongoDB only and never committed to GitHub to prevent TOS violations and maintain legal compliance.

**Phase 0 (Research)**: ✅ COMPLETE - See [research.md](./research.md)  
**Phase 1 (Design)**: ✅ COMPLETE - See [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)  
**Phase 2 (Tasks)**: ⏳ PENDING - Run `/speckit.tasks` to generate [tasks.md](./tasks.md) with implementation breakdown

## Technical Context

**Language/Version**: C# .NET 8 (nullable reference types enabled, LINQ for queries)  
**Primary Dependencies**:

- EasyMeals.Shared.Data (MongoDB repository pattern with unit of work)
- MassTransit (saga orchestration and domain events)
- MongoDB.Driver 2.25+ (document storage)
- HttpClient with Polly (resilient HTTP with retries, circuit breaker, rate limiting)
- Serilog (structured logging)

**Storage**: MongoDB via `EasyMeals.Shared.Data` fluent repository builder:

- Automatic collection creation with `[BsonCollection]` attributes
- Automatic index creation via `WithDefaultIndexes()` and `WithCustomIndexes<T>()`
- Automatic DI registration via `.ConfigureEasyMealsDatabase().AddRepository<T>()`
- Automatic health checks registered
- **No manual IHostedService or Program.cs initialization required**
- See data-model.md for complete setup example  
  **Testing**: xUnit with FluentAssertions; contract → integration → unit hierarchy; 80%+ coverage required  
  **Target Platform**: Docker containers deployed via Coolify scheduler (Linux x64 runtime)  
  **Project Type**: single (apps/recipe-engine/EasyMeals.RecipeEngine with DDD layers: Domain, Application, Infrastructure)  
  **Performance Goals**:

- Process 100 recipes per hour batch window (configurable)
- <500ms processing time per recipe (including HTTP fetch, normalization, persistence)
- <100 MB memory footprint at rest per container instance
- Support dynamic site crawling with JavaScript rendering (Playwright/Selenium)

**Constraints**:

- MUST respect provider rate limits (configurable per provider, e.g., 10 req/min)
- MUST implement stealth measures (randomized delays ±20%, rotating user agents, connection pooling)
- MUST persist saga state after each recipe for crash recovery (no data loss on restart)
- MUST be extensible for multiple providers without code changes (config-driven)

**Scale/Scope**:

- Initial: Single provider with ~10,000 recipes expected in catalog
- Future: 5-10 additional providers (stored in MongoDB with unique provider IDs)
- Scheduled runs: 24 times per day (hourly batch processing)
- Discovery strategies: 3 pluggable implementations (static crawl, dynamic crawl, API)

## Constitution Check

_GATE: Must pass before Phase 0 research. Re-check after Phase 1 design._

### Principle I: Code Quality

- [ ] **Linting/Formatting**: All C# code MUST follow .editorconfig standards (nullable reference types enabled, LINQ preferred)
- [ ] **Self-Documenting Code**: Comments explain WHY (e.g., "Using exponential backoff to avoid 429 responses"), not WHAT
- [ ] **Security**: OWASP Top 10 applied (parameterized MongoDB queries, no hardcoded secrets, input validation for URLs); **database-driven configuration ensures provider URLs/rate limits never committed to GitHub (TOS compliance)**
- [ ] **Complexity Justified**: Saga pattern required for multi-step workflow orchestration (simpler state machine insufficient for compensating transactions); database-driven config justified for security/auditability
- [ ] **Dependencies Minimized**: Only essential packages (MassTransit for saga, Polly for HTTP resilience, Playwright for dynamic crawling)
- [ ] **Type Safety**: C# nullable reference types enabled, no nullable warnings allowed

**Status**: ✅ PASS (Saga pattern + database-driven config justified for security and crash recovery; DDD layers keep complexity organized)

### Principle II: Testing Standards

- [ ] **Test-First**: Red-Green-Refactor cycle enforced; tests written before implementation
- [ ] **Contract Tests**: All saga state transitions tested (Discovering → Fingerprinting → Processing → Persisting → Completed)
- [ ] **Integration Tests**: End-to-end workflow with test provider, MongoDB testcontainer, HTTP mocks
- [ ] **Unit Tests**: Business logic (ingredient normalization, rate limiting, fingerprint generation) with edge cases
- [ ] **Test Independence**: No shared state between tests; each test creates its own saga state
- [ ] **Acceptance Mapping**: Each user story acceptance scenario maps 1:1 to test case (5 stories × 5 scenarios = 25+ tests)
- [ ] **Coverage**: 80%+ for critical paths (saga orchestration, normalization, rate limiting)

**Status**: ✅ PASS (Test plan includes contract/integration/unit hierarchy; acceptance scenarios provide clear test cases)

### Principle III: User Experience Consistency

- [ ] **API Consistency**: Domain events use consistent schema (BatchStarted, BatchCompleted, ItemProcessed, ErrorOccurred)
- [ ] **Error Messages**: Structured logs with actionable context (e.g., "Recipe URL failed fingerprinting: [URL], Error: [details], Retry: [attempt]")
- [ ] **Operational Visibility**: Comprehensive logging (processing progress, error rates, state transitions, timing)

**Status**: ✅ PASS (No direct UI; API/logging consistency enforced through structured logging and domain events)

### Principle IV: Performance Requirements

- [ ] **Backend Latency**: <500ms per recipe processing (including HTTP fetch + normalization + persistence)
- [ ] **Memory Usage**: <100MB at rest per container (saga state, HTTP client pool, discovery cache)
- [ ] **Database Indexes**: Indexes on recipe URL/fingerprint for duplicate detection, provider ID for batch queries
- [ ] **Caching**: Connection pooling (HttpClient), saga state caching during batch processing
- [ ] **Monitoring**: Response times, error rates, batch completion metrics logged and visible

**Status**: ✅ PASS (Performance goals aligned with 100 recipes/hour target; connection pooling and indexing planned)

### Summary

**All Constitution gates PASS.** Feature can proceed to Phase 0 (Research) and Phase 1 (Design).

**Justified Complexities**:

- **Saga Pattern**: Required for multi-step workflow (discovery → fingerprinting → processing → persistence) with state persistence, compensating transactions, and crash recovery. Simpler alternatives (state machine without compensation, single-pass processing) insufficient because the system must resume after crashes without data loss and handle partial failures at each stage.
- **Strategy Pattern for Discovery**: Required for pluggable discovery implementations (static crawl, dynamic crawl, API-based) per provider. Simpler alternatives (single hardcoded implementation) violate the requirement for multi-provider extensibility and configuration-driven behavior.
- **DDD Layers**: Required for separation of concerns (Domain entities, Application saga orchestration, Infrastructure HTTP/MongoDB). Simpler alternatives (monolithic service) would tightly couple business logic with infrastructure, making testing and provider extensions difficult.

## Project Structure

### Documentation (this feature)

```text
specs/001-hellofresh-recipe-engine/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── IDiscoveryService.cs           # Discovery strategy contract (static/dynamic/API)
│   ├── IIngredientNormalizer.cs       # Ingredient normalization contract
│   ├── IRecipeFingerprinter.cs        # Recipe duplicate detection contract
│   ├── IRateLimiter.cs                # Rate limiting contract
│   └── IRecipeProcessingSaga.cs       # Saga orchestration contract (already exists)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
apps/recipe-engine/
├── EasyMeals.RecipeEngine.sln
└── src/
    ├── EasyMeals.RecipeEngine/                    # Entry point (console app for batch processing)
    │   ├── Program.cs                             # DI setup via EasyMeals.Shared.Data fluent API
    │   │                                          # NO manual IHostedService for index creation!
    │   ├── appsettings.json                       # MongoDB connection string only (DB config)
    │   ├── appsettings.Development.json           # Development overrides
    │   └── Dockerfile                             # Container image for Coolify deployment
    │
    ├── EasyMeals.RecipeEngine.Domain/             # Domain layer (entities, value objects, events)
    │   ├── Entities/
    │   │   ├── Recipe.cs                          # Recipe aggregate root (already exists)
    │   │   ├── RecipeBatch.cs                     # Batch aggregate root (enforce invariants)
    │   │   ├── IngredientMapping.cs               # Normalization mapping aggregate root
    │   │   └── RecipeFingerprint.cs               # Duplicate detection aggregate root
    │   ├── ValueObjects/
    │   │   ├── ProviderConfiguration.cs           # Provider settings (immutable, loaded from MongoDB)
    │   │   ├── DiscoveryStrategy.cs               # Strategy enum (Static, Dynamic, API)
    │   │   ├── RateLimitToken.cs                  # Rate limit tracking value object
    │   │   ├── IngredientReference.cs             # Ingredient value object (provider code + canonical form)
    │   │   └── RecipeProcessingResult.cs          # Batch processing result value object
    │   ├── Services/
    │   │   ├── IRecipeDuplicationChecker.cs       # Domain service: check duplicates across aggregates
    │   │   └── IBatchCompletionPolicy.cs          # Domain service: determine batch completion logic
    │   ├── Events/
    │   │   ├── BatchStartedEvent.cs               # Domain event: batch processing started
    │   │   ├── BatchCompletedEvent.cs             # Domain event: batch processing completed
    │   │   ├── RecipeProcessedEvent.cs            # Domain event: single recipe processed
    │   │   ├── ProcessingErrorEvent.cs            # Domain event: processing error occurred
    │   │   └── IngredientMappingMissingEvent.cs   # Domain event: unmapped ingredient detected
    │   ├── Repositories/
    │   │   ├── IRecipeBatchRepository.cs          # Aggregate repository contract
    │   │   ├── IRecipeRepository.cs               # Aggregate repository contract
    │   │   ├── IIngredientMappingRepository.cs    # Aggregate repository contract
    │   │   └── IRecipeFingerprintRepository.cs    # Aggregate repository contract
    │   └── Exceptions/
    │       ├── DomainException.cs                 # Base domain exception
    │       └── BatchSizeLimitExceededException.cs # Domain-specific exception
    │
    ├── EasyMeals.RecipeEngine.Application/        # Application layer (sagas, handlers, interfaces)
    │   ├── Services/
    │   │   └── RecipeProcessingApplicationService.cs  # NEW: Coordinates recipe processing use case
    │   ├── Sagas/
    │   │   ├── RecipeProcessingSaga.cs            # Saga implementation (COMPLETE THIS)
    │   │   └── RecipeProcessingSagaState.cs       # Saga state entity (add missing states)
    │   ├── EventHandlers/
    │   │   ├── BatchStartedEventHandler.cs        # NEW: Handle batch started domain event
    │   │   ├── RecipeProcessedEventHandler.cs     # NEW: Handle recipe processed domain event
    │   │   └── IngredientMappingMissingEventHandler.cs  # NEW: Log unmapped ingredients
    │   ├── Interfaces/
    │   │   ├── IDiscoveryService.cs               # NEW: Discovery strategy interface
    │   │   ├── IIngredientNormalizer.cs           # NEW: Ingredient normalization interface
    │   │   ├── IRecipeFingerprinter.cs            # NEW: Fingerprinting interface
    │   │   ├── IRateLimiter.cs                    # NEW: Rate limiting interface
    │   │   ├── IRecipeProcessingSaga.cs           # Saga interface (already exists)
    │   │   └── IProviderConfigurationLoader.cs    # NEW: Load provider config from MongoDB
    │   └── Options/
    │       └── MongoDbOptions.cs                  # Configuration binding class (connection string only)
    │
    └── EasyMeals.RecipeEngine.Infrastructure/     # Infrastructure layer (HTTP, MongoDB, implementations)
        ├── Discovery/
        │   ├── StaticCrawlDiscoveryService.cs     # NEW: Static site crawling implementation
        │   ├── DynamicCrawlDiscoveryService.cs    # NEW: Dynamic site crawling (Playwright)
        │   └── ApiDiscoveryService.cs             # NEW: API-based discovery implementation
        ├── Normalization/
        │   └── IngredientNormalizationService.cs  # NEW: Ingredient mapping service
        ├── Fingerprinting/
        │   └── RecipeFingerprintService.cs        # NEW: Content hash generation service
        ├── RateLimiting/
        │   └── TokenBucketRateLimiter.cs          # NEW: Token bucket rate limiter
        ├── Configuration/
        │   ├── ProviderConfigurationLoader.cs     # NEW: Load provider configs from MongoDB
        │   └── ProviderConfigurationHostedService.cs  # NEW: IHostedService for startup loading
        ├── Repositories/
        │   ├── MongoRecipeBatchRepository.cs      # NEW: Implements IRecipeBatchRepository
        │   │                                      # Extends MongoRepository<RecipeBatchDocument> from Shared.Data
        │   ├── MongoRecipeRepository.cs           # NEW: Implements IRecipeRepository
        │   │                                      # Extends existing RecipeRepository from Shared.Data
        │   ├── MongoIngredientMappingRepository.cs # NEW: Implements IIngredientMappingRepository
        │   │                                      # Extends MongoRepository<IngredientMappingDocument>
        │   └── MongoRecipeFingerprintRepository.cs # NEW: Implements IRecipeFingerprintRepository
        │                                          # Extends MongoRepository<RecipeFingerprintDocument>
        ├── DomainServices/
        │   ├── RecipeDuplicationChecker.cs        # NEW: Domain service implementation
        │   └── BatchCompletionPolicy.cs           # NEW: Domain service implementation
        ├── Documents/
        │   ├── RecipeBatchDocument.cs             # NEW: MongoDB document mapping for RecipeBatch
        │   ├── IngredientMappingDocument.cs       # NEW: MongoDB document mapping
        │   ├── RecipeFingerprintDocument.cs       # NEW: MongoDB document mapping
        │   └── ProviderConfigurationDocument.cs   # NEW: MongoDB document for provider settings
        └── DependencyInjection/
            └── ServiceCollectionExtensions.cs     # DI registration for Infrastructure services

tests/
├── EasyMeals.RecipeEngine.Tests.Contract/         # Contract tests (saga state transitions)
│   └── RecipeProcessingSagaContractTests.cs
├── EasyMeals.RecipeEngine.Tests.Integration/      # Integration tests (end-to-end with MongoDB testcontainer)
│   └── RecipeProcessingWorkflowTests.cs
└── EasyMeals.RecipeEngine.Tests.Unit/             # Unit tests (business logic, edge cases)
    ├── Normalization/
    │   └── IngredientNormalizationServiceTests.cs
    ├── Fingerprinting/
    │   └── RecipeFingerprintServiceTests.cs
    └── RateLimiting/
        └── TokenBucketRateLimiterTests.cs

packages/shared/src/EasyMeals.Shared.Data/         # Existing MongoDB infrastructure (DO NOT MODIFY)
├── MongoRepository.cs                             # Base repository pattern
├── RecipeDocument.cs                              # Recipe MongoDB document
└── UnitOfWork.cs                                  # Transaction coordination
```

---

## EasyMeals.Shared.Data Integration Strategy

**🎯 Key Principle**: Leverage existing shared infrastructure, don't duplicate functionality.

### What Shared.Data Provides (Use As-Is)

✅ **Automatic Index Management**:

- `MongoIndexConfiguration.CreateBaseDocumentIndexesAsync()` - Indexes on Id, CreatedAt, UpdatedAt
- `EasyMealsRepositoryBuilder.WithDefaultIndexes()` - Fluent API for base indexes
- `.WithCustomIndexes<T>(indexCreator)` - Fluent API for custom indexes per collection
- `.EnsureDatabaseAsync()` - Single call that creates collections AND indexes

✅ **Repository Pattern**:

- `IMongoRepository<T>` - Generic read-write repository
- `IReadOnlyMongoRepository<T>` - Generic read-only repository (principle of least privilege)
- `MongoRepository<T>` - Base implementation with CRUD, find, paging

✅ **Document Base Classes**:

- `BaseDocument` - Provides Id (ObjectId → string), CreatedAt, UpdatedAt, Version
- `BaseSoftDeletableDocument` - Extends BaseDocument with IsDeleted, DeletedAt, SoftDelete(), Restore()
- `[BsonCollection("name")]` - Attribute for collection name mapping

✅ **DI Registration**:

- `AddEasyMealsMongoDb(configuration)` - Register MongoDB client and database
- `.ConfigureEasyMealsDatabase()` - Fluent builder for repositories
- `.AddRepository<T>()` - Register repository in DI
- Health checks automatically registered

✅ **Health Checks**:

- Automatically registered when repositories added
- Available at `/health` endpoint (for ASP.NET Core)

### Recipe Engine-Specific Implementation

**DO**: Create domain-specific repository interfaces in Domain layer:

```csharp
// Domain/Repositories/IRecipeBatchRepository.cs
public interface IRecipeBatchRepository
{
    Task<RecipeBatch?> GetByIdAsync(Guid id);
    Task<RecipeBatch> CreateAsync(string providerId, ProviderConfiguration config);
    Task SaveAsync(RecipeBatch batch);
    Task<IEnumerable<RecipeBatch>> GetRecentAsync(string providerId, int count);
}
```

**DO**: Implement via MongoDB in Infrastructure layer:

```csharp
// Infrastructure/Repositories/MongoRecipeBatchRepository.cs
public class MongoRecipeBatchRepository : IRecipeBatchRepository
{
    private readonly IMongoRepository<RecipeBatchDocument> _mongoRepo;

    public async Task SaveAsync(RecipeBatch batch)
    {
        var document = RecipeBatchDocument.FromDomain(batch);
        await _mongoRepo.UpsertAsync(document);  // From Shared.Data
    }
}
```

**DON'T**: Create manual `IHostedService` for index creation - use fluent API in Program.cs:

```csharp
// ✅ CORRECT - Automatic (no service needed!)
await services
    .ConfigureEasyMealsDatabase()
    .AddRepository<RecipeBatchDocument>()
    .WithDefaultIndexes()
    .WithCustomIndexes<RecipeBatchDocument>(/* ... */)
    .EnsureDatabaseAsync();

// ❌ WRONG - Manual initialization (duplicates Shared.Data functionality)
public class DatabaseInitializer : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        // Don't do this! Shared.Data handles it
    }
}
```

**DON'T**: Reference `IMongoRepository<T>` in Domain layer:

```csharp
// ❌ WRONG - Infrastructure dependency in Domain
public class RecipeService
{
    public RecipeService(IMongoRepository<RecipeDocument> mongoRepo) { }
}

// ✅ CORRECT - Domain-specific repository interface
public class RecipeService
{
    public RecipeService(IRecipeRepository recipeRepo) { }
}
```

### Document Design

All Recipe Engine documents inherit from `BaseDocument`:

```csharp
[BsonCollection("recipe_batches")]
public class RecipeBatchDocument : BaseDocument
{
    public string ProviderId { get; set; }
    public int BatchSize { get; set; }
    public BatchStatus Status { get; set; }
    // ... other fields
    // Inherited: Id, CreatedAt, UpdatedAt, Version
}
```

Use `[BsonCollection("name")]` attribute so index configuration can auto-discover collection names.

---

## DDD Best Practices Validation

_GATE: Verify tactical patterns before implementation. Re-check during code review._

### Aggregate Roots & Boundaries

**✅ Properly Identified Aggregates**:

1. **RecipeBatch** (root) - Controls collection of recipes, enforces batch size/time window invariants
   - Boundary: Batch metadata, processed URLs, failed URLs
   - Invariants: BatchSize never exceeded, CompletedAt immutable after completion
   - Identity: Guid (unique per batch)

2. **Recipe** (root) - Represents scraped recipe with ingredients
   - Boundary: Recipe metadata, ingredients list (via IngredientReference value objects)
   - Invariants: ProviderId + Url unique, FingerprintHash immutable
   - Identity: Guid (unique per recipe)

3. **IngredientMapping** (root) - Maps provider codes to canonical forms
   - Boundary: ProviderId + ProviderCode → CanonicalForm mapping
   - Invariants: ProviderId + ProviderCode unique, CanonicalForm required
   - Identity: Guid (unique per mapping)

4. **RecipeFingerprint** (root) - Tracks content hashes for duplicate detection
   - Boundary: URL + fingerprint hash
   - Invariants: FingerprintHash immutable, ProviderId + Url unique
   - Identity: Guid (unique per fingerprint)

**✅ Aggregate Access Pattern**: Always load aggregate roots through repositories; never navigate between aggregates (use IDs for references).

### Value Objects

**✅ Immutability Enforced**:

- `ProviderConfiguration`: Loaded from MongoDB, read-only in domain
- `IngredientReference`: Immutable (constructor-only initialization)
- `RateLimitToken`: Immutable token count tracking
- `RecipeProcessingResult`: Immutable outcome record
- `DiscoveryStrategy`: Enum (inherently immutable)

**✅ Equality by Value**: Value objects implement `Equals()` and `GetHashCode()` based on properties, not reference.

### Domain Services

**✅ Cross-Aggregate Operations**:

1. **IRecipeDuplicationChecker**: Checks if recipe already exists (queries RecipeFingerprint aggregate)
2. **IBatchCompletionPolicy**: Determines if batch should complete (time window + batch size rules)

**Rationale**: These operations span multiple aggregates or require complex domain logic that doesn't belong to a single entity.

### Repository Pattern

**✅ Domain Layer Contracts**:

- `IRecipeBatchRepository`, `IRecipeRepository`, `IIngredientMappingRepository`, `IRecipeFingerprintRepository`
- Repositories defined in Domain layer, implemented in Infrastructure layer
- Abstractions prevent domain logic from depending on MongoDB specifics

**✅ Aggregate Persistence**: Repositories save/load entire aggregate roots, not individual properties.

### Domain Events

**✅ State Change Notifications**:

- `BatchStartedEvent`, `BatchCompletedEvent`, `RecipeProcessedEvent`, `ProcessingErrorEvent`, `IngredientMappingMissingEvent`
- Events raised by aggregate roots, published by Application layer
- Handlers in Application layer for cross-aggregate coordination

**Pattern**: Aggregates collect events via `AddDomainEvent()`, Application Service publishes after persistence (transactional consistency).

### Application Services

**✅ Use Case Coordination**:

- `RecipeProcessingApplicationService`: Orchestrates recipe processing workflow
- Loads aggregates, invokes domain services, persists results, publishes events
- Transaction boundary: Single batch processing operation

**Layering**: Application Services call Domain Services and Repositories, never the reverse.

### Anti-Pattern Prevention

**✅ No Anemic Domain Model**:

- Aggregates contain **behavior**, not just properties:
  - `RecipeBatch.ProcessRecipe()`, `RecipeBatch.CompleteBatch()`, `RecipeBatch.ShouldStopProcessing()`
  - `Recipe.AddIngredient()`, `Recipe.UpdateFingerprint()`
  - `IngredientMapping.UpdateCanonicalForm()`
- Business rules enforced in domain layer, not Application or Infrastructure

**✅ No Infrastructure Leakage**:

- Domain entities use `IRecipeRepository` interface, not `IMongoRepository<RecipeDocument>`
- Domain layer has ZERO references to MongoDB.Driver or EasyMeals.Shared.Data
- Infrastructure implements domain contracts, domain never references infrastructure

**✅ No Ubiquitous Language Violations**:

- Terms from spec.md used consistently: RecipeBatch, IngredientMapping, RecipeFingerprint, ProviderConfiguration
- No technical jargon in domain (e.g., "RecipeDTO" or "RecipeEntity")

### Summary

**All DDD tactical patterns validated.** Architecture follows best practices:

- Aggregate boundaries enforce invariants and consistency
- Value objects immutable with value-based equality
- Domain services handle cross-aggregate logic
- Repositories abstract persistence
- Domain events decouple aggregates
- Application services coordinate use cases
- No anemic domain model or infrastructure leakage

## Complexity Tracking

No additional violations beyond those justified in Constitution Check. All architectural decisions (Saga pattern, Strategy pattern, DDD layers) are essential for meeting requirements and have been approved.
