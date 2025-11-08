---
description: "Implementation task breakdown for Recipe Engine"
---

# Tasks: Multi-Provider Recipe Engine with Database-Driven Configuration

**Input**: Design documents from `/specs/001-hellofresh-recipe-engine/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are NOT explicitly requested in the feature specification, but we include comprehensive test coverage (contract, integration, unit) to ensure production-readiness with 80%+ coverage goal.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Repository structure: `apps/recipe-engine/` with DDD layers (Domain, Application, Infrastructure)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Start MongoDB via Docker Compose `docker-compose up -d mongodb` from repository root (verify with `docker ps`)
- [x] T002 [P] Verify Docker network `easymeals-local` exists and MongoDB is accessible at `mongodb://admin:devpassword@mongodb:27017`
- [x] T003 [P] Install Playwright browsers in local environment via `pwsh tools/playwright.ps1 install chromium` for dynamic crawling support (required for development/testing, pre-installed in Docker image for production)
- [x] T004 Build solution via `dotnet build apps/recipe-engine/EasyMeals.RecipeEngine.sln` to verify all dependencies resolve
- [x] T005 [P] Create `tools/playwright.ps1` script if it doesn't exist with Playwright installation commands (or document manual installation via `npx playwright install chromium`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Create `ProviderConfigurationDocument` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Documents/ProviderConfigurationDocument.cs` with properties per data-model.md (ProviderId, Enabled, DiscoveryStrategy, RecipeRootUrl, BatchSize, TimeWindowMinutes, MinDelaySeconds, MaxRequestsPerMinute, RetryCount, RequestTimeoutSeconds, audit fields)
- [x] T007 [P] Create `RecipeBatchDocument` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Documents/RecipeBatchDocument.cs` with properties per data-model.md (ProviderId, BatchSize, TimeWindowMinutes, StartedAt, CompletedAt, ProcessedCount, SkippedCount, FailedCount, Status, ProcessedUrls, FailedUrls)
- [x] T008 [P] Create `IngredientMappingDocument` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Documents/IngredientMappingDocument.cs` with properties per data-model.md (ProviderId, ProviderCode, CanonicalForm, Notes, CreatedAt, UpdatedAt)
- [x] T009 [P] Create `RecipeFingerprintDocument` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Documents/RecipeFingerprintDocument.cs` with properties per data-model.md (FingerprintHash, ProviderId, RecipeUrl, RecipeId, ProcessedAt)
- [x] T010 Extend `RecipeDocument` in `packages/shared/src/EasyMeals.Shared.Data/` to add ProviderId, ProviderRecipeId, FingerprintHash, ScrapedAt, LastUpdatedAt, Ingredients (list of IngredientReferenceDocument)
- [x] T011 Create `ProviderConfiguration` value object in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/ValueObjects/ProviderConfiguration.cs` with properties and validation per data-model.md (ProviderId, Enabled, DiscoveryStrategy enum, RecipeRootUrl, BatchSize, TimeWindow, MinDelay, MaxRequestsPerMinute, RetryCount, RequestTimeout)
- [x] T012 [P] Create `DiscoveryStrategy` enum in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/ValueObjects/DiscoveryStrategy.cs` with values (Static, Dynamic, Api)
- [x] T013 [P] Create `RateLimitToken` value object in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/ValueObjects/RateLimitToken.cs` with properties and methods per data-model.md (AvailableTokens, MaxTokens, RefillRate, LastRefillAt, ConsumeToken(), RefillTokens())
- [x] T014 [P] Create `IngredientReference` value object in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/ValueObjects/IngredientReference.cs` with properties per data-model.md (ProviderCode, CanonicalForm, Quantity, DisplayOrder)
- [x] T015 Create `RecipeBatch` aggregate root in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Entities/RecipeBatch.cs` with properties and behavior methods per data-model.md (CreateBatch factory, MarkRecipeProcessed, MarkRecipeSkipped, MarkRecipeFailed, CompleteBatch, ShouldStopProcessing)
- [x] T016 [P] Create `IngredientMapping` aggregate root in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Entities/IngredientMapping.cs` with properties and Create factory method per data-model.md (ProviderId, ProviderCode, CanonicalForm, Notes, UpdateCanonicalForm method)
- [x] T017 [P] Create `RecipeFingerprint` aggregate root in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Entities/RecipeFingerprint.cs` with properties and Create factory method per data-model.md (FingerprintHash, ProviderId, RecipeUrl, RecipeId, ProcessedAt)
- [x] T018 Extend `Recipe` aggregate root in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Entities/Recipe.cs` to add ProviderId, ProviderRecipeId, FingerprintHash, ScrapedAt, LastUpdatedAt, Ingredients list, and AddIngredient/UpdateFingerprint methods
- [x] T019 [P] Create `BatchStartedEvent` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Events/BatchStartedEvent.cs` with properties (Guid BatchId, string ProviderId, DateTime StartedAt)
- [x] T020 [P] Create `BatchCompletedEvent` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Events/BatchCompletedEvent.cs` with properties (Guid BatchId, int ProcessedCount, int SkippedCount, int FailedCount, DateTime CompletedAt)
- [x] T021 [P] Create `RecipeProcessedEvent` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Events/RecipeProcessedEvent.cs` with properties (Guid RecipeId, string Url, string ProviderId, DateTime ProcessedAt)
- [x] T022 [P] Create `ProcessingErrorEvent` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Events/ProcessingErrorEvent.cs` with properties (string Url, string ProviderId, string ErrorMessage, DateTime OccurredAt)
- [x] T023 [P] Create `IngredientMappingMissingEvent` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Events/IngredientMappingMissingEvent.cs` with properties (string ProviderId, string ProviderCode, string RecipeUrl)
- [x] T024 Create `IRecipeBatchRepository` interface in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Repositories/IRecipeBatchRepository.cs` with methods per data-model.md (GetByIdAsync, GetActiveAsync, CreateAsync, SaveAsync, GetRecentBatchesAsync)
- [x] T025 [P] Create `IIngredientMappingRepository` interface in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Repositories/IIngredientMappingRepository.cs` with methods per data-model.md (GetByCodeAsync, GetAllByProviderAsync, SaveAsync, GetUnmappedCodesAsync)
- [x] T026 [P] Create `IRecipeFingerprintRepository` interface in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Repositories/IRecipeFingerprintRepository.cs` with methods per data-model.md (GetByUrlAsync, ExistsAsync, SaveAsync, SaveBatchAsync, CountByProviderAsync)
- [x] T027 [P] Create `IRecipeRepository` interface in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Repositories/IRecipeRepository.cs` with methods per data-model.md (GetByIdAsync, GetByUrlAsync, SaveAsync, SaveBatchAsync, CountByProviderAsync)
- [x] T028 Create `IRecipeDuplicationChecker` domain service interface in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Services/IRecipeDuplicationChecker.cs` with methods per data-model.md (IsDuplicateAsync, GetExistingFingerprintAsync)
- [x] T029 [P] Create `IBatchCompletionPolicy` domain service interface in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Domain/Services/IBatchCompletionPolicy.cs` with methods per data-model.md (ShouldCompleteBatch, GetCompletionReason)
- [x] T030 Create `IProviderConfigurationLoader` interface in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/Interfaces/IProviderConfigurationLoader.cs` with methods (GetByProviderIdAsync, GetAllEnabledAsync, LoadConfigurationsAsync)
- [x] T031 Implement `MongoRecipeBatchRepository` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Repositories/MongoRecipeBatchRepository.cs` extending MongoRepository<RecipeBatchDocument> with IRecipeBatchRepository implementation
- [x] T032 [P] Implement `MongoIngredientMappingRepository` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Repositories/MongoIngredientMappingRepository.cs` extending MongoRepository<IngredientMappingDocument> with IIngredientMappingRepository implementation
- [x] T033 [P] Implement `MongoRecipeFingerprintRepository` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Repositories/MongoRecipeFingerprintRepository.cs` extending MongoRepository<RecipeFingerprintDocument> with IRecipeFingerprintRepository implementation
- [x] T034 [P] Implement `MongoRecipeRepository` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Repositories/MongoRecipeRepository.cs` extending existing RecipeRepository from Shared.Data with IRecipeRepository implementation
- [x] T035 Implement `RecipeDuplicationChecker` domain service in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Services/RecipeDuplicationChecker.cs` implementing IRecipeDuplicationChecker with IRecipeFingerprintRepository dependency
- [x] T036 [P] Implement `BatchCompletionPolicy` domain service in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Services/BatchCompletionPolicy.cs` implementing IBatchCompletionPolicy with batch size and time window logic
- [x] T037 Implement `ProviderConfigurationLoader` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Services/ProviderConfigurationLoader.cs` implementing IProviderConfigurationLoader with MongoDB query logic
- [x] T038 Implement `ConfigurationHostedService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Services/ConfigurationHostedService.cs` implementing IHostedService to load configurations at startup and cache in memory
- [x] T039 Update `Program.cs` in `apps/recipe-engine/src/EasyMeals.RecipeEngine/Program.cs` to register MongoDB via EasyMeals.Shared.Data fluent API with all document types (ProviderConfigurationDocument, RecipeBatchDocument, IngredientMappingDocument, RecipeFingerprintDocument, RecipeDocument), configure default indexes and custom indexes per data-model.md, call EnsureDatabaseAsync() to create collections and indexes, and configure connection string from environment variable (MongoDB\_\_ConnectionString) for Docker compatibility
- [x] T040 Register all repositories, domain services, and hosted service in DI via `ServiceCollectionExtensions.cs` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 2.5: Database Seeding & Initial Configuration

**Purpose**: Seed MongoDB with initial configuration and test data AFTER repositories and indexes are created

**⚠️ PREREQUISITE**: Phase 2 must be complete (repositories implemented, indexes created via EnsureDatabaseAsync())

**Approach**: Run the Recipe Engine app once to create collections and indexes, then seed data

- [ ] T040a Run Recipe Engine app in Docker via `docker-compose up easymeals-recipe-engine` to create MongoDB collections and indexes (app will fail due to missing provider config - this is expected)
- [ ] T040b Verify collections exist in MongoDB via `docker exec -it easymeals-mongodb-local mongosh -u admin -p devpassword --eval "use easymeals; db.getCollectionNames()"` (should show: provider_configurations, recipe_batches, ingredient_mappings, recipe_fingerprints, recipes)
- [ ] T040c [P] Seed provider configuration via MongoDB Compass or mongosh script in `tools/seed-provider-config.js` with provider_001 settings (enabled=true, discoveryStrategy=Dynamic, recipeRootUrl="{placeholder}", batchSize=10, timeWindowMinutes=10, minDelaySeconds=2, maxRequestsPerMinute=10, retryCount=3, requestTimeoutSeconds=30)
- [ ] T040d [P] Seed sample ingredient mappings via MongoDB Compass or mongosh script in `tools/seed-ingredient-mappings.js` with at least 10 common ingredients for provider_001 (e.g., BROCCOLI-FROZEN-012 → "broccoli, frozen", CHICKEN-BREAST-024 → "chicken breast, boneless")
- [ ] T040e [P] Create seeding documentation in `specs/001-hellofresh-recipe-engine/quickstart.md` section "Seeding Test Data" with mongosh commands and MongoDB Compass instructions
- [ ] T040f Verify seeded data via `docker exec -it easymeals-mongodb-local mongosh -u admin -p devpassword --eval "use easymeals; db.provider_configurations.find(); db.ingredient_mappings.countDocuments()"`

**Checkpoint**: MongoDB is seeded with test data - ready for end-to-end testing

---

## Phase 3: User Story 1 - Process Recipes Within Rate & Time Limits (Priority: P1) 🎯 MVP

**Goal**: Enable the recipe engine to process up to 100 recipes (or available quantity) within a configured time window (default: 1 hour), respecting rate limits and batch size constraints, with zero data loss on application restart.

**Independent Test**: Mock HelloFresh endpoints, verify that exactly 100 recipes (or fewer if time limit reached) are processed within the configured time window, with appropriate delays between requests, and that saga state is persisted after each recipe for crash recovery.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T041 [P] [US1] Contract test for RecipeProcessingSaga state transitions (Idle → Discovering → Fingerprinting → Processing → Persisting → Completed) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Contract/RecipeProcessingSagaContractTests.cs`
- [ ] T042 [P] [US1] Contract test for RecipeProcessingSaga compensation logic (retry transient errors, skip permanent errors) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Contract/RecipeProcessingSagaCompensationTests.cs`
- [ ] T043 [P] [US1] Contract test for RecipeProcessingSaga crash recovery (save state mid-batch, restart, resume from saved state) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Contract/RecipeProcessingSagaCrashRecoveryTests.cs`
- [ ] T044 [US1] Integration test for rate limiting (verify max requests per minute enforced, burst handling) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/RateLimitingIntegrationTests.cs` with MongoDB Testcontainers
- [ ] T045 [US1] Integration test for batch processing workflow (discovery → fingerprinting → processing → persistence) with mocked HTTP responses in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/RecipeProcessingWorkflowTests.cs` with MongoDB Testcontainers
- [ ] T046 [P] [US1] Unit test for token bucket rate limiter (token consumption, refill, burst) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/RateLimiting/TokenBucketRateLimiterTests.cs`
- [ ] T047 [P] [US1] Unit test for batch completion policy (batch size reached, time window exceeded, both) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/DomainServices/BatchCompletionPolicyTests.cs`

### Implementation for User Story 1

- [ ] T048 [P] [US1] Implement `TokenBucketRateLimiter` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/RateLimiting/TokenBucketRateLimiter.cs` implementing IRateLimiter interface with token bucket algorithm per research.md (AvailableTokens, MaxTokens, RefillRate, ConsumeToken, RefillTokens background task)
- [ ] T049 [P] [US1] Update `RecipeProcessingSagaState` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/Sagas/RecipeProcessingSagaState.cs` to add missing properties per data-model.md (DiscoveredUrls, FingerprintedUrls, ProcessedUrls, FailedUrls, CurrentIndex for crash recovery)
- [ ] T050 [US1] Implement `RecipeProcessingSaga` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/Sagas/RecipeProcessingSaga.cs` implementing MassTransitStateMachine<RecipeProcessingSagaState> with all state transitions per data-model.md and contracts/IRecipeProcessingSaga.cs (Discovering → Fingerprinting → Processing → Persisting → Completed)
- [ ] T051 [US1] Implement saga Discovering state handler in `RecipeProcessingSaga.cs` to call IDiscoveryService and store URLs in sagaState.DiscoveredUrls, persist saga state, transition to Fingerprinting
- [ ] T052 [US1] Implement saga Fingerprinting state handler in `RecipeProcessingSaga.cs` to call IRecipeFingerprinter for each URL, filter duplicates, store non-duplicates in sagaState.FingerprintedUrls, persist saga state, transition to Processing
- [ ] T053 [US1] Implement saga Processing state handler in `RecipeProcessingSaga.cs` to iterate through FingerprintedUrls, call IRateLimiter.WaitForTokenAsync before each HTTP request, parse recipe data, call IIngredientNormalizer, create Recipe entities, update sagaState.ProcessedUrls and CurrentIndex after each recipe, persist saga state after each recipe for crash recovery, check IBatchCompletionPolicy.ShouldCompleteBatch after each recipe, transition to Persisting when batch complete or time window exceeded
- [ ] T054 [US1] Implement saga Persisting state handler in `RecipeProcessingSaga.cs` to batch insert Recipe entities via IRecipeRepository.SaveBatchAsync, batch insert RecipeFingerprint entities via IRecipeFingerprintRepository.SaveBatchAsync, update RecipeBatch with final counts, persist RecipeBatch via IRecipeBatchRepository.SaveAsync, emit BatchCompletedEvent, transition to Completed
- [ ] T055 [US1] Implement saga compensation logic in `RecipeProcessingSaga.cs` to retry transient errors (network, timeout) with exponential backoff up to RetryCount, skip permanent errors (invalid data) and add to FailedUrls, log all failures with context (URL, error details, retry attempt)
- [ ] T056 [US1] Implement saga crash recovery in `RecipeProcessingSaga.cs` to load saga state from MongoDB on startup via ResumeProcessingAsync, resume from CurrentIndex in FingerprintedUrls, skip already processed URLs
- [ ] T057 [US1] Implement `RecipeProcessingApplicationService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/Services/RecipeProcessingApplicationService.cs` to coordinate saga workflow (load provider config, create RecipeBatch, call IRecipeProcessingSaga.StartProcessingAsync, handle domain events)
- [ ] T058 [US1] Implement `BatchStartedEventHandler` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/EventHandlers/BatchStartedEventHandler.cs` to log batch start with structured logging (providerId, batchId, batchSize, timeWindow)
- [ ] T059 [P] [US1] Implement `RecipeProcessedEventHandler` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/EventHandlers/RecipeProcessedEventHandler.cs` to log recipe processed with structured logging (recipeUrl, providerId, processingTime)
- [ ] T060 [US1] Update `Program.cs` in `apps/recipe-engine/src/EasyMeals.RecipeEngine/Program.cs` to configure MassTransit with MongoDB saga repository, register RecipeProcessingSaga, configure Serilog structured logging with enrichment (correlationId, providerId), and invoke RecipeProcessingApplicationService on startup

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently - the engine can process recipes in batches, respect rate limits, and recover from crashes

---

## Phase 4: User Story 2 - Normalize Proprietary Ingredients (Priority: P1)

**Goal**: Map provider-specific ingredient codes (e.g., "HF-BROCCOLI-FROZEN-012") to canonical forms (e.g., "broccoli, frozen") using MongoDB mapping database, store both provider code and canonical form in Recipe entities for auditability, and log warnings for unmapped ingredients without blocking processing.

**Independent Test**: Provide a set of HelloFresh proprietary ingredient identifiers, verify that each is mapped to a standardized canonical form, that unmapped ingredients are logged with warnings, and that both provider code and canonical form are stored in Recipe entities.

### Tests for User Story 2

- [ ] T061 [P] [US2] Unit test for ingredient normalization service (mapped code returns canonical form) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Normalization/IngredientNormalizationServiceTests.cs`
- [ ] T062 [P] [US2] Unit test for ingredient normalization service (unmapped code returns null, logs warning) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Normalization/IngredientNormalizationServiceTests.cs`
- [ ] T063 [P] [US2] Unit test for ingredient normalization service batch method (multiple codes mapped efficiently) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Normalization/IngredientNormalizationServiceTests.cs`
- [ ] T064 [US2] Integration test for ingredient normalization workflow (query MongoDB, return canonical form, cache frequently used mappings) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/IngredientNormalizationIntegrationTests.cs` with MongoDB Testcontainers

### Implementation for User Story 2

- [ ] T065 [P] [US2] Implement `IngredientNormalizationService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Normalization/IngredientNormalizationService.cs` implementing IIngredientNormalizer interface with IIngredientMappingRepository dependency, NormalizeAsync method to query MongoDB by (ProviderId, ProviderCode), return CanonicalForm or null if unmapped, log warning for unmapped ingredients
- [ ] T066 [US2] Implement `NormalizeBatchAsync` method in `IngredientNormalizationService.cs` to batch query MongoDB for multiple provider codes, return dictionary of providerCode → canonicalForm (null for unmapped), reduce database round-trips
- [ ] T067 [US2] Add in-memory LRU cache to `IngredientNormalizationService.cs` for frequently used mappings (e.g., "broccoli" appears in 100+ recipes), cache key = (ProviderId, ProviderCode), cache size = 1000 entries, TTL = 1 hour
- [ ] T068 [US2] Implement `IngredientMappingMissingEventHandler` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Application/EventHandlers/IngredientMappingMissingEventHandler.cs` to log unmapped ingredients with structured logging (providerId, providerCode, recipeUrl), emit IngredientMappingMissingEvent
- [ ] T069 [US2] Update saga Processing state handler in `RecipeProcessingSaga.cs` to call IIngredientNormalizer.NormalizeBatchAsync for all ingredient codes in recipe, create IngredientReference value objects with both ProviderCode and CanonicalForm (null if unmapped), emit IngredientMappingMissingEvent for unmapped ingredients, continue processing (non-blocking)
- [ ] T070 [US2] Register `IngredientNormalizationService` and `IngredientMappingMissingEventHandler` in DI via `ServiceCollectionExtensions.cs`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently - recipes are processed with normalized ingredients, unmapped ingredients are logged

---

## Phase 5: User Story 3 - Complete Recipe Processing Saga (Priority: P1)

**Goal**: Finalize the multi-step recipe processing saga (discovery → fingerprinting → processing → persistence) with graceful error handling, state persistence across restarts, compensation logic for transient/permanent failures, and comprehensive logging for operational visibility.

**Independent Test**: Simulate the full processing workflow with injected failures at each stage (discovery, fingerprinting, processing, persistence), verify that the saga transitions correctly through states, applies compensating transactions on failure (retry transient, skip permanent), logs progress with structured context, and can resume from a saved state after restart.

### Tests for User Story 3

- [ ] T071 [P] [US3] Contract test for saga transient error retry (network error, timeout) with exponential backoff in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Contract/RecipeProcessingSagaRetryTests.cs`
- [ ] T072 [P] [US3] Contract test for saga permanent error skip (invalid data) and continue processing in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Contract/RecipeProcessingSagaPermanentErrorTests.cs`
- [ ] T073 [US3] Integration test for end-to-end workflow with all error scenarios (discovery failure, fingerprinting failure, processing failure, persistence failure) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/RecipeProcessingErrorHandlingTests.cs` with MongoDB Testcontainers and HTTP mocks
- [ ] T074 [US3] Integration test for saga state persistence after each stage (verify sagaState.DiscoveredUrls, FingerprintedUrls, ProcessedUrls, FailedUrls, CurrentIndex updated correctly) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/RecipeProcessingSagaStatePersistenceTests.cs` with MongoDB Testcontainers

### Implementation for User Story 3

- [ ] T075 [P] [US3] Implement saga error logging in `RecipeProcessingSaga.cs` with structured logging context (correlationId=batchId, providerId, currentState, currentUrl, errorType, errorMessage, retryAttempt, stackTrace for debugging)
- [ ] T076 [P] [US3] Implement saga transient error detection in `RecipeProcessingSaga.cs` to classify errors as transient (HttpRequestException, TaskCanceledException, SocketException) or permanent (JsonException, NullReferenceException, InvalidOperationException)
- [ ] T077 [US3] Implement saga exponential backoff retry policy in `RecipeProcessingSaga.cs` using Polly (WaitAndRetryAsync with exponential backoff, jitter, max retry count from ProviderConfiguration.RetryCount)
- [ ] T078 [US3] Implement saga permanent error handling in `RecipeProcessingSaga.cs` to log error with full context, add URL to sagaState.FailedUrls, emit ProcessingErrorEvent, continue processing remaining URLs without blocking
- [ ] T079 [US3] Implement saga state persistence after each recipe in Processing state handler (call MongoDB update on sagaState after each recipe processed/failed, update CurrentIndex for crash recovery)
- [ ] T080 [US3] Implement saga timeout handling in `RecipeProcessingSaga.cs` to check elapsed time after each recipe via IBatchCompletionPolicy.ShouldCompleteBatch, save state and exit gracefully if time window exceeded, log remaining URLs for next run
- [ ] T081 [US3] Implement `RecipeProcessingApplicationService.ResumeProcessingAsync` to load incomplete saga states from MongoDB on startup (query sagaStates where CurrentState != "Completed"), call IRecipeProcessingSaga.ResumeProcessingAsync for each incomplete saga
- [ ] T082 [US3] Add batch completion logging in saga Completed state handler to log comprehensive summary (providerId, batchId, processedCount, skippedCount, failedCount, duration, averageProcessingTime per recipe)

**Checkpoint**: At this point, User Story 3 is complete - the saga orchestrates the full workflow with robust error handling, crash recovery, and operational visibility

---

## Phase 6: User Story 4 - Robust Multi-Provider Configuration (Priority: P2)

**Goal**: Enable the recipe engine to support multiple providers through database-driven configuration stored in MongoDB (not appsettings.json), with provider-specific settings (URLs, rate limits, discovery strategy, batch size, time window, delays, retries), environment-aware configuration loading, and ability to add new providers without code changes.

**Independent Test**: Load provider configurations from MongoDB for multiple providers (HelloFresh, Blue Apron, etc.), verify that each provider uses its own settings without conflict, that environment variables override MongoDB settings, that new providers can be added via MongoDB without code changes, and that disabled providers are skipped during processing.

### Tests for User Story 4

- [ ] T083 [P] [US4] Unit test for provider configuration loader (load all enabled providers from MongoDB) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Configuration/ProviderConfigurationLoaderTests.cs`
- [ ] T084 [P] [US4] Unit test for provider configuration validation (invalid settings throw exceptions at startup) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Configuration/ProviderConfigurationValidationTests.cs`
- [ ] T085 [US4] Integration test for provider configuration caching (load from MongoDB, cache in memory, refresh on TTL) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/ProviderConfigurationCachingTests.cs` with MongoDB Testcontainers
- [ ] T086 [US4] Integration test for multi-provider processing (load configs for provider_001 and provider_002, process batches sequentially with different settings) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/MultiProviderProcessingTests.cs` with MongoDB Testcontainers

### Implementation for User Story 4

- [ ] T087 [P] [US4] Add configuration caching to `ProviderConfigurationLoader.cs` with in-memory cache (key=ProviderId, TTL=1 hour, invalidate on refresh), LoadConfigurationsAsync method to load all enabled providers at startup
- [ ] T088 [P] [US4] Add configuration validation to `ProviderConfiguration` value object constructor (ProviderId required, BatchSize > 0, TimeWindow > 0, MinDelay >= 0, MaxRequestsPerMinute > 0, RetryCount >= 0, RequestTimeout > 0, RecipeRootUrl valid HTTPS URL)
- [ ] T089 [US4] Update `ProviderConfigurationHostedService.cs` to call ProviderConfigurationLoader.LoadConfigurationsAsync on startup, log all loaded providers with settings (sanitize RecipeRootUrl in logs), fail fast if no enabled providers or invalid configuration
- [ ] T090 [US4] Update `RecipeProcessingApplicationService.cs` to iterate through all enabled providers, create RecipeBatch for each provider, call IRecipeProcessingSaga.StartProcessingAsync sequentially (respect batch time windows, avoid overlapping batches)
- [ ] T091 [US4] Add provider filtering in `RecipeProcessingApplicationService.cs` to skip disabled providers (Enabled=false), log skipped providers with reason
- [ ] T092 [US4] Add provider-specific rate limiters in `TokenBucketRateLimiter.cs` to maintain separate token buckets per ProviderId (use ConcurrentDictionary<string, RateLimitToken>), resolve rate limiter by ProviderId at runtime

**Checkpoint**: At this point, User Stories 1, 2, 3, AND 4 are complete - the engine supports multiple providers with database-driven configuration

---

## Phase 7: User Story 5 - Stealth & IP Ban Avoidance (Priority: P2)

**Goal**: Implement practices to avoid IP bans and provider blocking: randomized delays between requests (±20% variance), rotating user agents, respectful crawl headers, connection pooling, and configurable rate limits to ensure the system is perceived as a well-behaved crawler, not a DDoS attack.

**Independent Test**: Intercept HTTP requests, verify that delays vary randomly (not fixed), user agents rotate between realistic browser strings, crawl headers are present (Accept-Language, Accept-Encoding), connections are reused via pooling, and request rate respects configured limits (e.g., max 10 requests per minute).

### Tests for User Story 5

- [ ] T093 [P] [US5] Unit test for randomized delay calculation (delay varies ±20% around MinDelay) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Stealth/RandomizedDelayTests.cs`
- [ ] T094 [P] [US5] Unit test for user agent rotation (round-robin or random selection from list) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Stealth/UserAgentRotationTests.cs`
- [ ] T095 [US5] Integration test for HTTP request headers (verify Accept-Language, Accept-Encoding, User-Agent present) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/HttpStealthTests.cs` with HTTP mock inspection
- [ ] T096 [US5] Integration test for connection pooling (verify HttpClient reuses TCP connections) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/ConnectionPoolingTests.cs` with HTTP mock connection tracking

### Implementation for User Story 5

- [ ] T097 [P] [US5] Create `RandomizedDelayService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Stealth/RandomizedDelayService.cs` with CalculateDelay method (MinDelay _ (0.8 + Random.NextDouble() _ 0.4) for ±20% variance)
- [ ] T098 [P] [US5] Create `UserAgentRotationService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Stealth/UserAgentRotationService.cs` with GetNextUserAgent method (load list from appsettings.json, rotate round-robin or random, realistic browser user agents for Chrome, Firefox, Safari, Edge)
- [ ] T099 [US5] Create user agent configuration in `apps/recipe-engine/src/EasyMeals.RecipeEngine/appsettings.json` with array of realistic browser user agents (at least 10 different user agent strings)
- [ ] T100 [US5] Update saga Processing state handler in `RecipeProcessingSaga.cs` to call RandomizedDelayService.CalculateDelay before each HTTP request, await Task.Delay with calculated delay
- [ ] T101 [US5] Configure HttpClient in `ServiceCollectionExtensions.cs` to use SocketsHttpHandler with connection pooling settings (PooledConnectionLifetime = 5 minutes, MaxConnectionsPerServer = 10, AutomaticDecompression = GZip | Deflate)
- [ ] T102 [US5] Register HttpClient with Polly policies in `ServiceCollectionExtensions.cs` (retry policy with exponential backoff, circuit breaker policy after 5 failures, timeout policy per request, configure per ProviderConfiguration.RequestTimeout and RetryCount)
- [ ] T103 [US5] Update HTTP request creation in saga Processing state handler to add rotating user agent header via UserAgentRotationService.GetNextUserAgent(), add Accept-Language header (en-US,en;q=0.9), add Accept-Encoding header (gzip, deflate, br)
- [ ] T104 [US5] Add stealth logging in saga Processing state handler to log delay variance and user agent used per request for monitoring (debug level only, not in production logs)

**Checkpoint**: All user stories should now be independently functional - the engine processes recipes stealthily with IP ban avoidance measures

---

## Phase 8: Discovery Strategy Implementation

**Goal**: Implement three pluggable discovery strategies (static crawl, dynamic crawl, API-based) with configuration-driven selection per provider, enabling extensibility for multiple providers without code changes.

**Independent Test**: Configure three different test providers (one for each strategy), verify that each strategy discovers recipe URLs correctly, that strategy selection is driven by ProviderConfiguration.DiscoveryStrategy enum, and that HelloFresh uses dynamic discovery with recursive link discovery from configurable recipe-root URL.

### Tests for Discovery Strategies

- [ ] T105 [P] [DISC] Unit test for static crawl discovery service (parse static HTML with HtmlAgilityPack, extract recipe links via CSS selectors) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Discovery/StaticCrawlDiscoveryServiceTests.cs`
- [ ] T106 [P] [DISC] Unit test for dynamic crawl discovery service (mock Playwright, verify JavaScript rendering, pagination, recursive discovery) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Discovery/DynamicCrawlDiscoveryServiceTests.cs`
- [ ] T107 [P] [DISC] Unit test for API discovery service (mock API responses, parse JSON, extract recipe URLs) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Discovery/ApiDiscoveryServiceTests.cs`
- [ ] T108 [DISC] Integration test for dynamic crawl discovery with Playwright (headless Chromium, real JavaScript execution, pagination handling) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/DynamicCrawlDiscoveryIntegrationTests.cs`

### Implementation of Discovery Strategies

- [ ] T109 [P] [DISC] Implement `StaticCrawlDiscoveryService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Discovery/StaticCrawlDiscoveryService.cs` implementing IDiscoveryService with HttpClient and HtmlAgilityPack dependencies, DiscoverRecipeUrlsAsync method to fetch HTML from RecipeRootUrl, parse with HtmlAgilityPack, extract recipe links via CSS selectors from configuration, return absolute HTTPS URLs
- [ ] T110 [P] [DISC] Implement `DynamicCrawlDiscoveryService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Discovery/DynamicCrawlDiscoveryService.cs` implementing IDiscoveryService with Playwright dependencies, DiscoverRecipeUrlsAsync method to launch headless Chromium, navigate to RecipeRootUrl, wait for JavaScript to render (configurable wait selector), extract recipe URLs, handle pagination (configurable pagination pattern), recursively discover links (configurable max depth), return absolute HTTPS URLs
- [ ] T111 [P] [DISC] Implement `ApiDiscoveryService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Discovery/ApiDiscoveryService.cs` implementing IDiscoveryService with HttpClient dependency, DiscoverRecipeUrlsAsync method to call API endpoint from RecipeRootUrl, parse JSON response, extract recipe URLs from configured JSON path, handle pagination via API parameters, return absolute HTTPS URLs
- [ ] T112 [DISC] Create discovery service factory in `ServiceCollectionExtensions.cs` to resolve IDiscoveryService by DiscoveryStrategy enum (Static → StaticCrawlDiscoveryService, Dynamic → DynamicCrawlDiscoveryService, Api → ApiDiscoveryService)
- [ ] T113 [DISC] Update saga Discovering state handler in `RecipeProcessingSaga.cs` to resolve IDiscoveryService via factory by ProviderConfiguration.DiscoveryStrategy, call DiscoverRecipeUrlsAsync, store URLs in sagaState.DiscoveredUrls
- [ ] T114 [DISC] Configure Playwright browser launch options in `DynamicCrawlDiscoveryService.cs` (headless=true, args=[--no-sandbox, --disable-setuid-sandbox] for Docker, timeout=30s)
- [ ] T115 [DISC] Add discovery error handling in all three discovery services to throw DiscoveryException with context (ProviderId, RecipeRootUrl, error message, inner exception)
- [ ] T116 [DISC] Register all three discovery services and factory in DI via `ServiceCollectionExtensions.cs`

**Checkpoint**: All three discovery strategies are implemented and tested - providers can use different discovery methods based on configuration

---

## Phase 9: Fingerprinting Implementation

**Goal**: Implement content-based recipe fingerprinting using SHA256 hash of (URL + title + description) for robust duplicate detection, with MongoDB persistence for fast duplicate lookups and handling of URL changes (provider site redesigns).

**Independent Test**: Generate fingerprints for a set of recipes, verify that duplicates are detected correctly (same fingerprint), that URL changes are detected (different fingerprint), and that fingerprints are stored in MongoDB with indexes for fast lookups.

### Tests for Fingerprinting

- [ ] T117 [P] [FP] Unit test for fingerprint generation (SHA256 hash of normalized URL + title + description) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Fingerprinting/RecipeFingerprintServiceTests.cs`
- [ ] T118 [P] [FP] Unit test for fingerprint duplicate detection (same content → same hash) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Unit/Fingerprinting/RecipeFingerprintServiceTests.cs`
- [ ] T119 [FP] Integration test for fingerprint persistence and lookup (save to MongoDB, query by hash, verify fast lookup with index) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Tests.Integration/RecipeFingerprintingIntegrationTests.cs` with MongoDB Testcontainers

### Implementation of Fingerprinting

- [ ] T120 [P] [FP] Implement `RecipeFingerprintService` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Fingerprinting/RecipeFingerprintService.cs` implementing IRecipeFingerprinter interface with IRecipeFingerprintRepository dependency
- [ ] T121 [FP] Implement GenerateFingerprintAsync method in `RecipeFingerprintService.cs` to normalize URL (lowercase, remove query params), normalize title (trim, lowercase), normalize description (substring first 200 chars, trim, lowercase), compute SHA256 hash of concatenated string, return hex string
- [ ] T122 [FP] Implement IsDuplicateAsync method in `RecipeFingerprintService.cs` to query MongoDB via IRecipeFingerprintRepository.GetByUrlAsync, compare fingerprint hashes, return true if match
- [ ] T123 [FP] Implement SaveFingerprintAsync method in `RecipeFingerprintService.cs` to create RecipeFingerprint entity, persist via IRecipeFingerprintRepository.SaveAsync
- [ ] T124 [FP] Update saga Fingerprinting state handler in `RecipeProcessingSaga.cs` to call IRecipeFingerprinter.GenerateFingerprintAsync for each URL in DiscoveredUrls, call IRecipeFingerprinter.IsDuplicateAsync, add non-duplicates to FingerprintedUrls, log skipped duplicates with count
- [ ] T125 [FP] Register `RecipeFingerprintService` in DI via `ServiceCollectionExtensions.cs`

**Checkpoint**: Fingerprinting is complete - duplicates are detected and skipped, fingerprints are stored for auditability

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T126 [P] Add comprehensive structured logging with Serilog enrichment (correlationId, providerId, batchId, currentState, recipeUrl, processingTime, errorType) in all saga state handlers
- [ ] T127 [P] Add performance metrics logging (recipes per second, average processing time, memory usage, MongoDB query time) in saga Completed state handler
- [ ] T128 [P] Add health checks for MongoDB, rate limiter, discovery services in `Program.cs` via ASP.NET Core health checks middleware (register IHealthCheck implementations)
- [ ] T129 Code cleanup and refactoring (extract magic numbers to constants, consolidate error handling patterns, apply DRY principle)
- [ ] T130 [P] Security review of provider URL handling (ensure RecipeRootUrl never logged in production, sanitize logs, validate HTTPS URLs only)
- [ ] T131 [P] Documentation updates in `specs/001-hellofresh-recipe-engine/quickstart.md` (add troubleshooting section for common errors, update MongoDB seed instructions, add example provider configuration JSON)
- [ ] T132 Run quickstart.md validation (follow all setup steps, verify MongoDB connection, seed test data, run tests, run application, verify logs)
- [ ] T133 Create Dockerfile for Recipe Engine in `apps/recipe-engine/Dockerfile` based on mcr.microsoft.com/playwright/dotnet:v1.40.0-focal with multi-stage build (restore, build, publish, runtime with Chromium pre-installed)
- [ ] T134 [P] Create Docker Compose configuration for local development in `apps/recipe-engine/docker-compose.yml` with MongoDB service and Recipe Engine service
- [ ] T135 Add CI/CD pipeline configuration in `.github/workflows/recipe-engine-ci.yml` to build, test (contract, integration, unit), publish Docker image, run security scan (dotnet list package --vulnerable)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phases 3-7)**: All depend on Foundational phase completion
  - US1 (Process recipes with rate & time limits) - No dependencies on other stories
  - US2 (Normalize ingredients) - Can run in parallel with US1, integrates in saga Processing state
  - US3 (Complete saga) - Depends on US1, US2 partial completion (needs saga structure)
  - US4 (Multi-provider configuration) - Can run in parallel with US1-3 (separate config layer)
  - US5 (Stealth measures) - Can run in parallel with US1-3 (separate HTTP layer)
- **Discovery (Phase 8)**: Depends on Foundational (uses IDiscoveryService interface) - Can run in parallel with US1-5
- **Fingerprinting (Phase 9)**: Depends on Foundational (uses IRecipeFingerprinter interface) - Can run in parallel with US1-5
- **Polish (Phase 10)**: Depends on all previous phases being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) - Integrates with US1 in saga Processing state
- **User Story 3 (P1)**: Depends on US1 partial completion (needs saga structure and state handlers)
- **User Story 4 (P2)**: Can start after Foundational (Phase 2) - Separate config layer, no dependencies on US1-3
- **User Story 5 (P2)**: Can start after Foundational (Phase 2) - Separate HTTP stealth layer, no dependencies on US1-3

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD approach)
- Domain entities and value objects before repositories
- Repositories before services
- Services before saga state handlers
- Saga state handlers before application service coordination
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002, T003, T004)
- All Foundational tasks marked [P] can run in parallel within their dependency group:
  - Documents (T007-T009) can run in parallel
  - Value objects (T012-T014) can run in parallel after T011
  - Entities (T016, T017) can run in parallel after T015
  - Events (T019-T023) can run in parallel
  - Repositories interfaces (T025-T027) can run in parallel after T024
  - Repository implementations (T032-T034) can run in parallel after T031
  - Domain services (T036) can run in parallel with T035
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows):
  - US1 tests (T041-T047) can run in parallel
  - US1 implementation tasks (T048-T049) can run in parallel before T050
  - US2 tests (T061-T064) can run in parallel
  - US2 implementation tasks (T065-T066) can run in parallel
  - US4 tests (T083-T086) can run in parallel
  - US5 tests (T093-T096) can run in parallel
  - US5 implementation tasks (T097-T098) can run in parallel
  - Discovery tests (T105-T107) can run in parallel
  - Discovery implementations (T109-T111) can run in parallel
  - Fingerprinting tests (T117-T118) can run in parallel
- Polish tasks marked [P] can run in parallel (T126-T128, T130-T131, T134)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together (write tests first, ensure they fail):
Task T041: "Contract test for RecipeProcessingSaga state transitions"
Task T042: "Contract test for RecipeProcessingSaga compensation logic"
Task T043: "Contract test for RecipeProcessingSaga crash recovery"
Task T046: "Unit test for token bucket rate limiter"
Task T047: "Unit test for batch completion policy"

# After tests fail, launch parallel implementations:
Task T048: "Implement TokenBucketRateLimiter"
Task T049: "Update RecipeProcessingSagaState"

# Then sequential saga implementation (T050-T056 must be in order)

# Launch parallel event handlers and application services:
Task T058: "Implement BatchStartedEventHandler"
Task T059: "Implement RecipeProcessedEventHandler"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (Process recipes with rate & time limits)
4. Complete Phase 8: Discovery Strategy Implementation (at least StaticCrawlDiscoveryService)
5. Complete Phase 9: Fingerprinting Implementation
6. **STOP and VALIDATE**: Test User Story 1 independently with a small batch (10 recipes, 10 minutes)
7. Deploy/demo if ready

**MVP Scope**: Basic recipe processing for a single provider with static discovery, rate limiting, batch processing, and crash recovery. No ingredient normalization yet (store raw provider codes).

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 + Discovery + Fingerprinting → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 (Ingredient normalization) → Test independently → Deploy/Demo
4. Add User Story 3 (Complete saga error handling) → Test independently → Deploy/Demo
5. Add User Story 4 (Multi-provider configuration) → Test independently → Deploy/Demo
6. Add User Story 5 (Stealth measures) → Test independently → Deploy/Demo
7. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (Saga implementation) + Discovery + Fingerprinting
   - Developer B: User Story 2 (Ingredient normalization) + US4 (Configuration)
   - Developer C: User Story 5 (Stealth measures) + Polish (Logging, metrics, health checks)
3. Developer A completes saga structure (T050-T056) first - this is the critical path
4. Developers B and C integrate their work into saga state handlers after Developer A completes structure
5. Stories complete and integrate independently

---

## Summary

- **Total Tasks**: 135 tasks
- **Task Count by Phase**:
  - Phase 1 (Setup): 5 tasks
  - Phase 2 (Foundational): 35 tasks (CRITICAL PATH)
  - Phase 3 (US1 - Rate & Time Limits): 20 tasks
  - Phase 4 (US2 - Ingredient Normalization): 10 tasks
  - Phase 5 (US3 - Complete Saga): 11 tasks
  - Phase 6 (US4 - Multi-Provider Configuration): 10 tasks
  - Phase 7 (US5 - Stealth Measures): 12 tasks
  - Phase 8 (Discovery Strategies): 12 tasks
  - Phase 9 (Fingerprinting): 9 tasks
  - Phase 10 (Polish): 11 tasks
- **Parallel Opportunities**: 45+ tasks can run in parallel (marked with [P])
- **Independent Test Criteria**: Each user story has clear acceptance criteria and can be tested independently
- **Suggested MVP Scope**: Phase 1 + Phase 2 + Phase 3 (US1) + Phase 8 (Static Discovery) + Phase 9 (Fingerprinting) = ~70 tasks for minimal viable product
- **Format Validation**: ✅ All tasks follow the checklist format (checkbox, ID, [P] if parallel, [Story] label for user story tasks, description with file path)

---

## Notes

- [P] tasks = different files, no dependencies on previous tasks in the same group
- [Story] label maps task to specific user story for traceability and independent delivery
- Each user story should be independently completable and testable
- Tests are written FIRST (TDD approach) - ensure they fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Database-driven configuration (MongoDB) ensures provider URLs and TOS-sensitive data never committed to GitHub
- Discovery strategies (Static/Dynamic/API) enable extensibility for multiple providers
- Saga pattern with state persistence enables crash recovery without data loss
- Token bucket rate limiting and stealth measures (randomized delays, rotating user agents) prevent IP bans
