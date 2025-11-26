# Implementation Plan: Provider Configuration System

**Branch**: `001-provider-config` | **Date**: 2025-11-25 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-provider-config/spec.md`

## Summary

Implement provider configuration infrastructure for the recipe-engine to control recipe discovery and extraction strategies. Provider configurations will be stored as runtime settings in MongoDB (not code) to avoid licensing issues, cached using `Microsoft.Extensions.Caching.Memory` with configurable TTL (default 30s). Domain entities reside in a new `EasyMeals.Domain` shared package, with persistence layer following existing `MongoRepository<T>` patterns. This spec covers infrastructure only—the recipe processing saga that consumes these configurations is out of scope.

**Phase Limitation**: Delete operations are handled via MongoDB direct access (Compass, mongosh) for this phase; programmatic delete is deferred to the API phase.

## Technical Context

**Language/Version**: C# 13 / .NET 10  
**Primary Dependencies**: MongoDB.Driver 3.5.1, Microsoft.Extensions.Caching.Memory 10.0.0  
**Storage**: MongoDB via `EasyMeals.Persistence.Mongo` shared package  
**Testing**: xUnit with Testcontainers.MongoDb 4.3.0 for integration tests  
**Target Platform**: Linux container (Coolify deployment)  
**Project Type**: Monorepo with shared packages and Clean Architecture  
**Performance Goals**: Provider config load <100ms p95; cache refresh <100ms p95  
**Constraints**: Cache TTL configurable via appsettings.json + env var; eventual consistency acceptable  
**Scale/Scope**: Support 20+ provider configurations without degradation

## Constitution Check

*GATE: All items verified for pre-Phase 0. Will re-check after Phase 1 design.*

### I. Code Quality & Maintainability ✅

- **Linters/Type-checks**: .editorconfig enforced; `dotnet build` with nullable reference types enabled
- **Structure**: Clean Architecture layers in recipe-engine; domain entities in shared `EasyMeals.Domain` package
- **SOLID adherence**: 
  - SRP: Separate domain entities, repository interfaces, and caching concerns
  - DIP: All layers depend on abstractions (`IProviderConfigurationRepository`, `IMemoryCache`)
  - ISP: Repository interface focused on config-specific operations
- **Public APIs**: `IProviderConfigurationRepository` interface documented with XML comments; value objects immutable

**Verification**: `dotnet build` succeeds with no warnings; code review checklist includes SOLID validation

### II. Testing Standards ✅ (MANDATORY)

- **Unit Tests**: 
  - `ProviderConfiguration` aggregate validation logic
  - `ExtractionSelectors`, `RateLimitSettings` value object equality/immutability
  - Strategy selection logic
  - **CSS selector validation** (valid/invalid selector parsing via AngleSharp)
  - **Provider name normalization** (lowercase, allowed characters)
- **Integration Tests**: 
  - Repository CRUD against MongoDB (Testcontainers)
  - Cache TTL behavior verification
  - Cache returns same instance within TTL window
  - **Cache `ClearCache()` forces DB refresh**
  - **Optimistic concurrency conflict handling**
  - **Index enforcement verification** (indexes exist after startup)
  - **Observability metrics emission** (cache hit/miss counters)
- **Contract Tests**: Repository interface contract verified by integration tests
- **CI Jobs**: Tests run in `deploy.yml` as part of build-recipe-engine job
- **Coverage**: >80% for new code; maintain existing levels

**Verification**: `dotnet test` runs in CI pre-merge; coverage reported via CI output

### III. UX Consistency ✅

- **Not user-facing this phase**: Provider configurations managed via MongoDB (direct queries, Compass)
- **Future API/UI deferred**: Will inherit design system components when implemented

**Verification**: N/A for this phase

### IV. Performance & Scalability ✅ (REQUIRED)

| Metric | Target | Validation Method |
|--------|--------|-------------------|
| Config load time | p95 < 100ms | Integration test with timing assertions |
| Cache refresh | p95 < 100ms | Integration test with cache expiration |
| Memory overhead | < 10MB for 20 configs | Manual verification via profiling |
| Document size | < 64KB per config | Unit test validates serialization size |

**Verification**: Integration tests include timing assertions; performance regression treated as bug

### V. Developer Workflow, Security & Observability ✅ (MANDATORY)

- **PR requirements**: Automated checks (linting, tests, type checks), one approving review
- **CI Security**: Dependency scanning already configured in `deploy.yml` workflow
- **Secrets**: 
  - No secrets in code; provider configs in MongoDB (not public repo)
  - **API credentials stored as secret references only** (e.g., `secret:provider-name-apikey`)
  - **Secrets resolved at runtime from secure store** (Azure Key Vault, AWS Secrets Manager, env vars for dev)
- **Observability**: 
  - Structured logging for config load/cache operations
  - **Metrics: cache hit/miss counters, load duration histogram, validation error counter**
  - **Log events: ConfigLoaded, CacheHit, CacheMiss, CacheCleared, ValidationFailed**
- **Complexity justification**: New `EasyMeals.Domain` package justified to prepare for future Recipe migration

**Verification**: CI gates enforce all checks; complexity tracked in table below

## Project Structure

### Documentation (this feature)

```text
specs/001-provider-config/
├── plan.md              # This file
├── research.md          # Phase 0: IMemoryCache patterns, MongoDB best practices
├── data-model.md        # Phase 1: ProviderConfiguration aggregate design
├── quickstart.md        # Phase 1: Developer guide for using the infrastructure
├── contracts/           # Phase 1: Repository interface definitions
│   └── IProviderConfigurationRepository.cs
└── tasks.md             # Phase 2: Implementation tasks (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
packages/easy-meals/
├── EasyMeals.Domain/                         # NEW PACKAGE
│   ├── EasyMeals.Domain.csproj
│   └── ProviderConfiguration/
│       ├── ProviderConfiguration.cs          # Aggregate root
│       ├── DiscoveryStrategy.cs              # Enum: Api, Crawl
│       ├── FetchingStrategy.cs               # Enum: Api, StaticHtml, DynamicHtml
│       ├── ExtractionSelectors.cs            # Value object
│       ├── RateLimitSettings.cs              # Value object
│       ├── ApiSettings.cs                    # Value object
│       ├── CrawlSettings.cs                  # Value object
│       └── CssSelectorValidator.cs           # NEW: Validates selectors via AngleSharp
├── EasyMeals.Persistence.Abstractions/
│   └── Repositories/
│       ├── IProviderConfigurationRepository.cs  # Interface (domain types)
│       └── ICacheableProviderConfigurationRepository.cs  # NEW: Extends with ClearCache()
├── EasyMeals.Persistence.Mongo/
│   ├── Documents/
│   │   └── ProviderConfiguration/            # NEW FOLDER
│   │       └── ProviderConfigurationDocument.cs
│   ├── Repositories/
│   │   └── ProviderConfigurationRepository.cs   # Implementation (handles mapping)
│   └── Indexes/
│       └── ProviderConfigurationIndexes.cs   # NEW: Index definitions
└── EasyMeals.Packages.sln                    # Updated to include new project

apps/recipe-engine/
├── src/
│   └── EasyMeals.RecipeEngine.Infrastructure/
│       ├── Caching/                          # NEW FOLDER
│       │   ├── CachedProviderConfigurationRepository.cs  # Implements ICacheableProviderConfigurationRepository
│       │   └── ProviderConfigurationCacheOptions.cs
│       ├── Metrics/                          # NEW FOLDER
│       │   └── ProviderConfigurationMetrics.cs  # Cache hit/miss counters
│       └── ServiceCollectionExtensions.cs    # Updated for DI registration
└── tests/
    └── EasyMeals.RecipeEngine.Infrastructure.Tests/  # NEW PROJECT
        ├── Caching/
        │   └── CachedProviderConfigurationRepositoryTests.cs
        ├── Repositories/
        │   └── ProviderConfigurationRepositoryIntegrationTests.cs
        ├── Indexes/
        │   └── IndexEnforcementTests.cs          # NEW: Verify indexes exist
        └── Validation/
            └── CssSelectorValidatorTests.cs      # NEW: Selector validation tests
```

**Structure Decision**: Monorepo with shared packages pattern. New `EasyMeals.Domain` package centralizes domain entities for cross-app reuse (recipe-engine now, API later). Infrastructure caching decorator pattern wraps repository for cache-aside strategy.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New `EasyMeals.Domain` package | Prepares for Recipe domain migration; shared domain entities across apps | Putting domain in recipe-engine would duplicate when API needs it |
| Caching decorator pattern | Separate concerns: repository knows persistence, decorator knows caching | Mixing caching logic in repository violates SRP |
| Repository abstraction | Supports future API/UI consumption; enables testing with mocks | Direct MongoDB access would couple consumers to persistence details |
