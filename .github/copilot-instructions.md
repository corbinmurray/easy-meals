# Copilot Instructions: Easy Meals Monorepo

## Architecture Overview

- **Monorepo** managed with pnpm workspaces. Key folders:
  - `apps/web`: Next.js (App Router, TypeScript, Tailwind, shadcn, Motion for React)
  - `apps/api`: .NET 8 Web API (C#)
  - `apps/recipe-engine`: .NET 8 Console App (C#, DDD, Saga pattern, MongoDB)
  - `packages/ui`: Shared UI components (React, Tailwind, shadcn)
  - `packages/shared`: Shared C# infrastructure (MongoDB repositories, domain events)
  - `packages/typescript-config`, `packages/eslint-config`: Shared config
- **Data flow**: API (C#) provides backend, web app fetches data via API routes or direct fetch.
- **Recipe Engine**: Batch processing console app (scheduled via Coolify) for scraping recipes from providers
- **UI**: All React UI uses Tailwind, and shadcn patterns. Animations use Motion for React (`motion/react`).

## Developer Workflows

- **Install dependencies**: Use `pnpm install` (never `npm install`)
- **Add package**: Use `pnpm add <pkg>` in the correct subfolder
- **Run web app**: `pnpm dev` from `apps/web`
- **Run API**: Use `dotnet run` from `apps/api/src/EasyMeals.Api`
- **Build all**: `pnpm build` (runs builds for all packages/apps)
- **Test**: (Add test instructions here if/when tests exist)

## Project-Specific Patterns

- **Next.js App Router**: Use server components by default, client components only for interactivity/state.
- **UI components**: Co-locate feature components in `apps/web/components`, shared in `packages/ui/src/components`.
- **Styling**: Use Tailwind utility classes theme tokens, and shadcn conventions. See `globals.css` for theme. We are using Tailwind v4, please use the new conventions.
- **lucide-react**: Use `lucide-react` for icons.
- **Motion for React**: Use `<motion.* />` components for animation. Animate with `animate`, `initial`, `whileHover`, `whileTap`, `whileInView`, `exit`, and `layout` props. Use `AnimatePresence` for exit animations. Example:
  ```tsx
  import { motion } from "motion/react";
  <motion.div
    initial={{ opacity: 0 }}
    animate={{ opacity: 1 }}
    exit={{ opacity: 0 }}
  />;
  ```
- **Grid layouts**: Use CSS grid with `auto-fit`/`minmax` for responsive card galleries (see `RecipeGallery.tsx`).
- **Icons**: Use `lucide-react` for icons.

## Integration & Cross-Component Patterns

- **Shared types**: Place in `apps/web/types` or `packages/ui` as needed.
- **API integration**: Web app fetches from API project or Next.js API routes.
- **Motion**: For advanced animation, refer to https://motion.dev/docs/react for patterns like gestures, scroll, layout, and exit animations.

## Examples

- **RecipeCard**: Animated card using shadcn, DaisyUI theme, and Motion for React.
- **RecipeGallery**: Responsive grid using `grid-cols-[repeat(auto-fit,minmax(340px,1fr))]`.

## Conventions

- Use pnpm for all JS/TS package management
- Use Tailwind, DaisyUI, and shadcn for all styling
- Use Motion for React for all animation
- Use C# for backend API and Recipe Engine

---

## Recipe Engine Architecture

### Overview

The Recipe Engine is a batch processing console application that scrapes recipes from providers and stores them in MongoDB. It uses **Domain-Driven Design** with **Saga orchestration** for multi-step workflows and **Strategy pattern** for pluggable discovery implementations.

### Project Structure

```
apps/recipe-engine/
├── src/
│   ├── EasyMeals.RecipeEngine/                    # Entry point (console app)
│   ├── EasyMeals.RecipeEngine.Domain/             # Domain entities, value objects, events
│   ├── EasyMeals.RecipeEngine.Application/        # Sagas, interfaces, event handlers
│   └── EasyMeals.RecipeEngine.Infrastructure/     # HTTP, MongoDB, service implementations
└── tests/
    ├── EasyMeals.RecipeEngine.Tests.Contract/     # Saga state transition tests
    ├── EasyMeals.RecipeEngine.Tests.Integration/  # End-to-end workflow tests
    └── EasyMeals.RecipeEngine.Tests.Unit/         # Business logic tests
```

### Key Patterns

#### 1. Saga Pattern (MassTransit)

- **Purpose**: Orchestrate multi-step recipe processing workflow with crash recovery
- **Workflow**: Discovery → Fingerprinting → Processing → Persistence → Completed
- **State Persistence**: Saga state stored in MongoDB; resume from any stage after restart
- **Implementation**: `RecipeProcessingSaga` implements `MassTransitStateMachine<RecipeProcessingSagaState>`
- **Compensation**: Retry transient errors (network, timeout), skip permanent errors (invalid data), log all failures

#### 2. Strategy Pattern (Discovery)

- **Purpose**: Pluggable recipe URL discovery per provider (config-driven)
- **Strategies**:
  - `StaticCrawlDiscoveryService`: Static HTML parsing (HtmlAgilityPack)
  - `DynamicCrawlDiscoveryService`: JavaScript rendering (Playwright, headless Chromium)
  - `ApiDiscoveryService`: API-based discovery (HttpClient + JSON)
- **Configuration**: `ProviderConfiguration.DiscoveryStrategy` enum (Static/Dynamic/Api)
- **Resolution**: Resolve by strategy enum at runtime via DI

#### 3. Repository Pattern (MongoDB via EasyMeals.Shared.Data)

- **Purpose**: Data access abstraction for MongoDB persistence
- **Collections**: `recipes`, `recipe_batches`, `recipe_fingerprints`, `ingredient_mappings`, `recipe_processing_saga_states`
- **Usage**: Inject `IMongoRepository<TDocument>` for CRUD operations
- **Indexes**: Created at startup via `IHostedService` (compound indexes on ProviderId, URL, fingerprint hash)

#### 4. Rate Limiting (Token Bucket Algorithm)

- **Purpose**: Enforce provider rate limits (e.g., 10 req/min) to avoid IP bans
- **Implementation**: `TokenBucketRateLimiter` with background refill task
- **Configuration**: `ProviderConfiguration.MaxRequestsPerMinute`, optional `BurstSize`
- **Usage**: `await rateLimiter.WaitForTokenAsync(providerId, cancellationToken)` before HTTP requests

### Key Entities

#### Domain Layer

- **RecipeBatch**: Aggregate root representing a batch of up to 100 recipes processed within a time window
- **Recipe**: Aggregate root representing a recipe (extended with ProviderId, FingerprintHash, Ingredients list)
- **IngredientMapping**: Maps provider-specific codes (e.g., "HF-BROCCOLI-FROZEN-012") to canonical forms (e.g., "broccoli, frozen")
- **RecipeFingerprint**: Content-based fingerprint (SHA256 of URL + title + description) for duplicate detection
- **ProviderConfiguration** (Value Object): Immutable provider settings (batch size, time window, delays, rate limits, discovery strategy)
- **RateLimitToken** (Value Object): Tracks available request quota for rate limiting

#### Application Layer

- **RecipeProcessingSagaState**: Saga state entity persisted to MongoDB (CorrelationId, CurrentState, URLs lists, CurrentIndex for crash recovery)

### Service Interfaces

Located in `EasyMeals.RecipeEngine.Application/Interfaces/`:

- **IDiscoveryService**: Discover recipe URLs from provider (3 implementations: Static/Dynamic/Api)
- **IIngredientNormalizer**: Normalize provider codes to canonical forms (MongoDB lookup)
- **IRecipeFingerprinter**: Generate SHA256 fingerprints, check duplicates, store fingerprints
- **IRateLimiter**: Token bucket rate limiting per provider
- **IRecipeProcessingSaga**: Saga orchestration (StartProcessingAsync, ResumeProcessingAsync, GetBatchStatusAsync)

### Configuration

**Database-Driven Configuration (MongoDB)**

Provider settings are stored in the `provider_configurations` MongoDB collection, not in `appsettings.json`. This ensures sensitive provider URLs, rate limits, and TOS-related data are never committed to GitHub.

**ProviderConfigurationDocument Schema**:

```csharp
{
  "providerId": "provider_001",              // Unique provider identifier
  "enabled": true,                           // Whether provider is active
  "discoveryStrategy": "Dynamic",            // Static/Dynamic/Api
  "recipeRootUrl": "{provider_url}",         // SENSITIVE: Never expose publicly
  "batchSize": 100,
  "timeWindowMinutes": 60,
  "minDelaySeconds": 2,
  "maxDelaySeconds": 4,
  "maxRequestsPerMinute": 10,
  "retryCount": 3,
  "requestTimeoutSeconds": 30,
  "createdAt": ISODate("2025-03-15T10:00:00Z"),
  "createdBy": "admin",
  "updatedAt": null,
  "updatedBy": null
}
```

**Loading Strategy**: `IHostedService` loads provider configurations from MongoDB at startup and caches in memory. Configurations are refreshed periodically or on-demand via admin CLI tool.

**Management**: Add/update providers via MongoDB Compass, mongosh, or admin CLI tool (not via appsettings.json).

**Security**: Provider URLs and rate limits are TOS-sensitive and must never be committed to GitHub or exposed via public APIs.

### Stealth Measures (IP Ban Avoidance)

- **Randomized Delays**: Vary delays by ±20% (e.g., 2s becomes 1.6-2.4s) to mimic human behavior
- **Rotating User Agents**: Load list of realistic browser user agents, rotate per request
- **Connection Pooling**: Reuse TCP connections via HttpClient singleton (SocketsHttpHandler)
- **Crawl Headers**: Include `Accept-Language`, `Accept-Encoding` headers matching real browser

### Testing Strategy

- **Contract Tests**: Saga state machine transitions (all states: Idle → Discovering → ... → Completed)
- **Integration Tests**: End-to-end workflow with MongoDB Testcontainers and HTTP mocks
- **Unit Tests**: Business logic (normalization, fingerprinting, rate limiting, configuration validation)
- **Coverage Goal**: 80%+ for critical paths (saga, normalization, fingerprinting, rate limiting)

### Performance Goals

- **100 recipes per hour**: ~36 seconds per recipe (including HTTP, parsing, normalization, persistence)
- **<500ms per recipe processing**: After HTTP fetch (parsing + normalization + persistence)
- **<100MB memory**: Saga state + HttpClient pool + discovery cache

### Development Workflow

1. **Start MongoDB**: `docker run -d --name easy-meals-mongodb -p 27017:27017 mongo:7.0`
2. **Set User Secrets**: `dotnet user-secrets set "MongoDB:ConnectionString" "mongodb://localhost:27017"`
3. **Seed Provider Config**: Use MongoDB Compass or mongosh to add provider configurations to `provider_configurations` collection
4. **Build**: `dotnet build EasyMeals.RecipeEngine.sln`
5. **Run Tests**: `dotnet test EasyMeals.RecipeEngine.sln`
6. **Run Locally**: `dotnet run` (from `apps/recipe-engine/src/EasyMeals.RecipeEngine/`)

### Deployment

- **Platform**: Docker containers via Coolify scheduler (hourly batch processing)
- **Image**: Multi-stage Dockerfile with Playwright pre-installed (`mcr.microsoft.com/playwright/dotnet:v1.40.0-focal`)
- **Secrets**: MongoDB connection string, provider credentials via environment variables

### Troubleshooting

- **MongoDB Connection**: Restart container, check user secrets (`dotnet user-secrets list`)
- **Playwright Browsers**: Install via `pwsh tools/playwright.ps1 install chromium` (pre-installed in Docker)
- **Rate Limiting**: Update provider configuration in MongoDB to adjust `MaxRequestsPerMinute` or `BatchSize`
- **Unmapped Ingredients**: Query MongoDB for null `CanonicalForm`, add mappings to `ingredient_mappings` collection
- **Missing Provider Config**: Ensure provider is seeded in `provider_configurations` collection with valid `recipeRootUrl`

### References

- **Specification**: `specs/001-recipe-engine/spec.md`
- **Implementation Plan**: `specs/001-recipe-engine/plan.md`
- **Research**: `specs/001-recipe-engine/research.md`
- **Data Model**: `specs/001-recipe-engine/data-model.md`
- **Contracts**: `specs/001-recipe-engine/contracts/`
- **Quickstart**: `specs/001-recipe-engine/quickstart.md`

---

For expert-level Motion for React usage, see https://motion.dev/docs/react and use props like `animate`, `initial`, `whileHover`, `whileTap`, `whileInView`, `exit`, `layout`, and `transition` for all animation needs.
