# Tasks: Provider Configuration System

**Input**: Design documents from `/specs/001-provider-config/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: Tests are MANDATORY. The project follows Test-First (TDD) practices. For every
user story tests MUST be written (unit, integration, and where applicable contract/acceptance)
and must fail before implementation begins. CI MUST run the test suites on PRs.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US4)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
- **Domain**: `packages/easy-meals/EasyMeals.Domain/`
- **Persistence Abstractions**: `packages/easy-meals/EasyMeals.Persistence.Abstractions/`
- **Persistence Mongo**: `packages/easy-meals/EasyMeals.Persistence.Mongo/`
- **Infrastructure**: `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/`
- **Tests**: `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/`

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Create new projects and configure dependencies

- [x] T001 Create `EasyMeals.Domain` project in `packages/easy-meals/EasyMeals.Domain/EasyMeals.Domain.csproj` with reference to `EasyMeals.Platform`
- [x] T002 Add `EasyMeals.Domain` project to `packages/easy-meals/EasyMeals.Packages.sln`
- [x] T003 [P] Add AngleSharp package reference to `Directory.Packages.props` (version 1.2.0)
- [x] T004 [P] Create test project `EasyMeals.RecipeEngine.Infrastructure.Tests` in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/`
- [x] T005 [P] Add test project to `apps/recipe-engine/EasyMeals.RecipeEngine.sln`
- [x] T006 Configure test project with xUnit, FluentAssertions, Testcontainers.MongoDb references

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain model and infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain Enums & Value Objects

- [x] T007 [P] Create `DiscoveryStrategy` enum in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/DiscoveryStrategy.cs`
- [x] T008 [P] Create `FetchingStrategy` enum in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/FetchingStrategy.cs`
- [x] T009 [P] Create `AuthMethod` enum in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/AuthMethod.cs`
- [x] T010 [P] Create `ExtractionSelectors` value object in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/ExtractionSelectors.cs`
- [x] T011 [P] Create `RateLimitSettings` value object in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/RateLimitSettings.cs`
- [x] T012 [P] Create `ApiSettings` value object in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/ApiSettings.cs`
- [x] T013 [P] Create `CrawlSettings` value object in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/CrawlSettings.cs`

### CSS Selector Validation

- [x] T014 Create `CssSelectorValidator` static class in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/CssSelectorValidator.cs` using AngleSharp

### Domain Aggregate Root

- [x] T015 Create `ProviderConfiguration` aggregate root in `packages/easy-meals/EasyMeals.Domain/ProviderConfiguration/ProviderConfiguration.cs` with validation logic (depends on T007-T014)

### Repository Interface

- [x] T016 Copy `IProviderConfigurationRepository` interface from `specs/001-provider-config/contracts/IProviderConfigurationRepository.cs` to `packages/easy-meals/EasyMeals.Persistence.Abstractions/Repositories/IProviderConfigurationRepository.cs` (contract already defined)
- [x] T017 Extract `ICacheableProviderConfigurationRepository` interface from contract file to `packages/easy-meals/EasyMeals.Persistence.Abstractions/Repositories/ICacheableProviderConfigurationRepository.cs`

### MongoDB Documents

- [x] T018 [P] Create `ProviderConfigurationDocument` in `packages/easy-meals/EasyMeals.Persistence.Mongo/Documents/ProviderConfiguration/ProviderConfigurationDocument.cs`
- [x] T019 [P] Create `ExtractionSelectorsDocument` embedded document in `packages/easy-meals/EasyMeals.Persistence.Mongo/Documents/ProviderConfiguration/ExtractionSelectorsDocument.cs`
- [x] T020 [P] Create `RateLimitSettingsDocument` embedded document in `packages/easy-meals/EasyMeals.Persistence.Mongo/Documents/ProviderConfiguration/RateLimitSettingsDocument.cs`
- [x] T021 [P] Create `ApiSettingsDocument` embedded document in `packages/easy-meals/EasyMeals.Persistence.Mongo/Documents/ProviderConfiguration/ApiSettingsDocument.cs`
- [x] T022 [P] Create `CrawlSettingsDocument` embedded document in `packages/easy-meals/EasyMeals.Persistence.Mongo/Documents/ProviderConfiguration/CrawlSettingsDocument.cs`

### Domain-Document Mapper

- [x] T023 Create `ProviderConfigurationMapper` in `packages/easy-meals/EasyMeals.Persistence.Mongo/Documents/ProviderConfiguration/ProviderConfigurationMapper.cs` (depends on T015, T018-T022)

### MongoDB Repository & Indexes

- [x] T024 Create `ProviderConfigurationRepository` implementation in `packages/easy-meals/EasyMeals.Persistence.Mongo/Repositories/ProviderConfigurationRepository.cs` (depends on T016, T023)
- [x] T025 Create `ProviderConfigurationIndexes` in `packages/easy-meals/EasyMeals.Persistence.Mongo/Indexes/ProviderConfigurationIndexes.cs`

### Caching Infrastructure

- [x] T026 [P] Create `ProviderConfigurationCacheOptions` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Caching/ProviderConfigurationCacheOptions.cs`
- [x] T027 Create `CachedProviderConfigurationRepository` decorator in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Caching/CachedProviderConfigurationRepository.cs` (depends on T016, T017, T026)

### Observability

- [x] T028 Create `ProviderConfigurationMetrics` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/Metrics/ProviderConfigurationMetrics.cs`

### DI Registration

- [x] T029 Update `ServiceCollectionExtensions.cs` in `apps/recipe-engine/src/EasyMeals.RecipeEngine.Infrastructure/ServiceCollectionExtensions.cs` with provider configuration registration (depends on T024, T027, T028)

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Configure a New Recipe Provider (Priority: P1) 🎯 MVP

**Goal**: Operators can add a new provider configuration to MongoDB and have the recipe engine load and use it

**Independent Test**: Insert a provider configuration document directly into MongoDB via Testcontainers and verify the repository loads it correctly

### Tests for User Story 1 (REQUIRED)

> **NOTE: Tests MUST be written FIRST and must FAIL before implementation.**

- [x] T030 [P] [US1] Unit tests for `ProviderConfiguration` validation in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Domain/ProviderConfigurationTests.cs`
- [x] T031 [P] [US1] Unit tests for `CssSelectorValidator` in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Validation/CssSelectorValidatorTests.cs`
- [x] T032 [P] [US1] Unit tests for value object equality/immutability in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Domain/ValueObjectTests.cs`
- [x] T033 [P] [US1] Integration tests for repository CRUD in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Repositories/ProviderConfigurationRepositoryTests.cs`
- [x] T034 [P] [US1] Integration tests for index enforcement in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Indexes/IndexEnforcementTests.cs`
- [x] T035 [P] [US1] Integration tests for `GetAllEnabledAsync` returns only enabled providers (ordered by priority) in same file as T033

### Implementation for User Story 1

- [ ] T036 [US1] Implement `ProviderConfiguration.Create()` factory method with full validation (in T015 file)
- [ ] T037 [US1] Implement `ProviderConfigurationRepository.AddAsync()` with provider name uniqueness check (in T024 file)
- [ ] T038 [US1] Implement `ProviderConfigurationRepository.GetAllEnabledAsync()` with soft-delete and priority ordering (in T024 file)
- [ ] T039 [US1] Implement index creation in `ProviderConfigurationIndexes` (in T025 file)
- [ ] T040 [US1] Verify `dotnet build` passes for all projects with no warnings

**Checkpoint**: User Story 1 complete - operators can insert provider configs and recipe engine loads them

---

## Phase 4: User Story 2 - Edit Existing Provider Configuration (Priority: P2)

**Goal**: Operators can modify existing provider configurations in MongoDB and changes are reflected after cache TTL

**Independent Test**: Update an existing provider document in MongoDB, wait for cache TTL, verify repository returns updated config

### Tests for User Story 2 (REQUIRED)

- [ ] T041 [P] [US2] Unit tests for `ProviderConfiguration.UpdateSelectors()` in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Domain/ProviderConfigurationTests.cs`
- [ ] T042 [P] [US2] Unit tests for `ProviderConfiguration.UpdateRateLimits()` in same file
- [ ] T043 [P] [US2] Integration tests for `UpdateAsync` with optimistic concurrency in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Repositories/ProviderConfigurationRepositoryTests.cs`
- [ ] T044 [P] [US2] Integration tests for cache TTL expiration in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Caching/CachedProviderConfigurationRepositoryTests.cs`
- [ ] T045 [P] [US2] Integration tests for cache returns same instance within TTL window in same file as T044

### Implementation for User Story 2

- [ ] T046 [US2] Implement `ProviderConfiguration.UpdateSelectors(ExtractionSelectors)` domain method (in T015 file)
- [ ] T047 [US2] Implement `ProviderConfiguration.UpdateRateLimits(RateLimitSettings)` domain method (in T015 file)
- [ ] T048 [US2] Implement `ProviderConfigurationRepository.UpdateAsync()` with concurrency token check (in T024 file)
- [ ] T049 [US2] Implement cache TTL behavior in `CachedProviderConfigurationRepository` (in T027 file)

**Checkpoint**: User Story 2 complete - operators can edit configs and changes reflect after cache TTL

---

## Phase 5: User Story 3 - Enable/Disable Provider Without Deletion (Priority: P2)

**Goal**: Operators can temporarily disable providers by updating `isEnabled` field in MongoDB

**Independent Test**: Set a provider's `isEnabled` to false, verify `GetAllEnabledAsync` excludes it after cache refresh

### Tests for User Story 3 (REQUIRED)

- [ ] T050 [P] [US3] Unit tests for `ProviderConfiguration.Enable()` and `Disable()` in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Domain/ProviderConfigurationTests.cs`
- [ ] T051 [P] [US3] Integration tests for enabled/disabled filtering in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Repositories/ProviderConfigurationRepositoryTests.cs`
- [ ] T052 [P] [US3] Integration test for soft-delete vs disable distinction in same file as T051

### Implementation for User Story 3

- [ ] T053 [US3] Implement `ProviderConfiguration.Enable()` domain method (in T015 file)
- [ ] T054 [US3] Implement `ProviderConfiguration.Disable()` domain method (in T015 file)
- [ ] T055 [US3] Ensure `GetAllEnabledAsync` filters by `isEnabled=true` AND `isDeleted=false` (verify in T038)

**Checkpoint**: User Story 3 complete - operators can enable/disable providers without deletion

---

## Phase 6: User Story 4 - Provider Configuration Supports Recipe Engine Processing (Priority: P1)

**Goal**: Domain model exposes all settings needed for future saga implementation (discovery, fetching, extraction, rate limiting)

**Independent Test**: Verify `ProviderConfiguration` entity exposes all required properties with correct types per data-model.md

### Tests for User Story 4 (REQUIRED)

- [ ] T056 [P] [US4] Unit tests verifying all domain properties accessible in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Domain/ProviderConfigurationTests.cs`
- [ ] T057 [P] [US4] Unit tests for strategy-specific settings validation (ApiSettings required when DiscoveryStrategy=Api) in same file
- [ ] T058 [P] [US4] Integration tests for round-trip persistence (save → load → verify all properties) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Repositories/ProviderConfigurationRepositoryTests.cs`

### Implementation for User Story 4

- [ ] T059 [US4] Verify `ProviderConfiguration` exposes: DiscoveryStrategy, FetchingStrategy, ExtractionSelectors, RateLimitSettings, ApiSettings, CrawlSettings (review T015)
- [ ] T060 [US4] Implement strategy-specific validation in `ProviderConfiguration.Validate()` (ApiSettings required for Api strategy, CrawlSettings for Crawl)
- [ ] T061 [US4] Verify mapper correctly maps all nested value objects (review T023)

**Checkpoint**: User Story 4 complete - domain model ready for saga consumption

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Observability, documentation, performance validation, and final verification

- [ ] T062 [P] Implement structured logging in `CachedProviderConfigurationRepository` (ConfigLoaded, CacheHit, CacheMiss, CacheCleared events)
- [ ] T063 [P] Implement metrics emission in `ProviderConfigurationMetrics` (cache hit/miss counters, load duration histogram)
- [ ] T064 [P] Wire up metrics in `CachedProviderConfigurationRepository` using `ProviderConfigurationMetrics`
- [ ] T065 [P] Add XML documentation to all public interfaces and domain entities
- [ ] T066 Run `dotnet build` for entire solution with TreatWarningsAsErrors to verify no warnings
- [ ] T067 Run `dotnet test` to verify all tests pass
- [ ] T068 Validate quickstart.md scenarios work end-to-end (manual verification against Testcontainers)
- [ ] T069 [P] Add integration test asserting config load time p95 < 100ms in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Performance/ConfigLoadPerformanceTests.cs`
- [ ] T070 [P] Add unit test verifying secret reference pattern (`secret:*`) is used for ApiSettings credentials (no raw secrets) in `apps/recipe-engine/tests/EasyMeals.RecipeEngine.Infrastructure.Tests/Security/SecretReferenceValidationTests.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phases 3-6)**: All depend on Foundational phase completion
  - US1 (P1) and US4 (P1) can run in parallel after Foundational
  - US2 (P2) and US3 (P2) can run in parallel after Foundational
  - US2 and US3 do NOT depend on US1 (all use same foundational infrastructure)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Shares infrastructure with US1 but independently testable
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - Shares infrastructure with US1 but independently testable
- **User Story 4 (P1)**: Can start after Foundational (Phase 2) - Verifies domain model completeness

### Within Each Phase

- Tests MUST be written and FAIL before implementation; CI must run tests for the branch and
  passing tests are required prior to merge
- Enums before value objects
- Value objects before aggregate root
- Aggregate root before repository
- Repository before caching decorator
- All [P] tasks within a phase can run in parallel

### Parallel Opportunities

**Phase 1 (Setup)**:
```
T003, T004, T005 can run in parallel
```

**Phase 2 (Foundational)**:
```
# Enums (all parallel)
T007, T008, T009 can run in parallel

# Value objects (all parallel, after enums)
T010, T011, T012, T013 can run in parallel

# Documents (all parallel)
T018, T019, T020, T021, T022 can run in parallel

# Cache options (parallel with documents)
T026 can run in parallel with T018-T022
```

**User Stories (all tests parallel within each story)**:
```
# US1 Tests
T030, T031, T032, T033, T034, T035 can run in parallel

# US2 Tests
T041, T042, T043, T044, T045 can run in parallel

# US3 Tests
T050, T051, T052 can run in parallel

# US4 Tests
T056, T057, T058 can run in parallel
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 4 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (Create provider configs)
4. Complete Phase 6: User Story 4 (Domain model completeness)
5. **STOP and VALIDATE**: Test US1 + US4 independently
6. Deploy/demo if ready - MVP complete!

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy (MVP baseline)
3. Add User Story 4 → Test independently → Verify domain completeness
4. Add User Story 2 → Test independently → Edit capability
5. Add User Story 3 → Test independently → Enable/disable capability
6. Polish phase → Production ready

### Parallel Team Strategy

With 2 developers:
1. Both complete Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 + User Story 4 (P1 priority)
   - Developer B: User Story 2 + User Story 3 (P2 priority)
3. Stories complete and integrate independently
4. Both contribute to Polish phase

---

## Summary

| Phase | Task Count | Parallel Opportunities |
|-------|------------|----------------------|
| Setup | 6 | 3 tasks parallel |
| Foundational | 23 | 15+ tasks parallel |
| US1 (P1) | 11 | 6 tests parallel |
| US2 (P2) | 9 | 5 tests parallel |
| US3 (P2) | 6 | 3 tests parallel |
| US4 (P1) | 6 | 3 tests parallel |
| Polish | 9 | 6 tasks parallel |
| **Total** | **70** | - |

**MVP Scope**: Phases 1-3 + Phase 6 (Setup + Foundational + US1 + US4) = ~46 tasks
**Full Feature**: All 70 tasks
