# Feature Specification: Provider Configuration System

**Feature Branch**: `001-provider-config`  
**Created**: November 25, 2025  
**Status**: Draft  
**Input**: User description: "Add provider configuration concept to recipe-engine for controlling recipe discovery and extraction strategies (API, crawl with static/dynamic HTML), CSS selectors for recipe properties, plus web UI for managing configurations on the fly."

**Scope**: Provider configuration domain model, persistence layer, and caching infrastructure only. This does NOT include implementing the recipe processing saga—only the configuration system that the saga will consume. API endpoints and Web UI are deferred to future work. Provider configurations are managed directly via MongoDB for this phase.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configure a New Recipe Provider (Priority: P1)

As an operator, I want to add a new recipe provider configuration to MongoDB so that the recipe engine can discover and extract recipes from a new source website.

**Why this priority**: This is the foundational capability—without the ability to define provider configurations, no other functionality can work. This enables the entire recipe discovery and extraction pipeline.

**Independent Test**: Can be fully tested by inserting a provider configuration document directly into MongoDB and verifying the recipe engine loads and uses it correctly.

**Acceptance Scenarios**:

1. **Given** I have MongoDB access, **When** I insert a valid provider configuration document with name, base URL, discovery strategy, fetching strategy, and CSS selectors, **Then** the recipe engine loads and uses this configuration within the cache TTL window
2. **Given** a provider configuration document exists in MongoDB, **When** the recipe engine starts or cache expires, **Then** it loads the configuration and can process recipes from that provider
3. **Given** multiple provider configurations exist in MongoDB, **When** the recipe engine queries for enabled providers, **Then** it retrieves all enabled configurations

---

### User Story 2 - Edit Existing Provider Configuration (Priority: P2)

As an operator, I want to modify an existing provider configuration in MongoDB so that I can adjust selectors or strategies when a source website changes its structure.

**Why this priority**: Websites frequently change their HTML structure. The ability to quickly adjust CSS selectors without code changes is critical for maintaining recipe extraction quality.

**Independent Test**: Can be fully tested by updating an existing provider document in MongoDB and verifying the recipe engine picks up changes after cache expiration.

**Acceptance Scenarios**:

1. **Given** a provider configuration exists in MongoDB, **When** I update the recipe title CSS selector directly in the database, **Then** the recipe engine uses the updated selector after cache TTL expires
2. **Given** a provider configuration exists, **When** I change the fetching strategy from static HTML to dynamic HTML in MongoDB, **Then** the recipe engine uses the appropriate fetching mechanism after cache refresh
3. **Given** I update a provider configuration, **When** the cache TTL (default 30s) expires, **Then** the recipe engine automatically loads the updated configuration

---

### User Story 3 - Enable/Disable Provider Without Deletion (Priority: P2)

As an operator, I want to temporarily disable a provider configuration by updating MongoDB so that I can pause recipe discovery from a source while preserving the configuration for later use.

**Why this priority**: Operators may need to pause providers due to rate limiting, temporary issues, or seasonal recipe sources without losing their carefully configured selectors.

**Independent Test**: Can be fully tested by setting a provider's `isEnabled` field to false in MongoDB and verifying the recipe engine skips it during discovery.

**Acceptance Scenarios**:

1. **Given** an enabled provider configuration in MongoDB, **When** I set `isEnabled` to false, **Then** the recipe engine skips this provider during discovery runs (after cache refresh)
2. **Given** a disabled provider configuration, **When** I set `isEnabled` to true in MongoDB, **Then** the recipe engine includes this provider in subsequent discovery runs
3. **Given** multiple provider configurations exist, **When** the recipe engine loads configurations, **Then** it filters to only process enabled providers

---

### User Story 4 - Provider Configuration Supports Recipe Engine Processing (Priority: P1)

As a developer implementing the recipe processing saga, I need provider configurations to expose all necessary settings so that I can build discovery, fetching, and extraction logic that adapts to each provider's requirements.

**Why this priority**: The configuration model must be complete and well-designed before the saga can be implemented. This story ensures the domain model captures all required settings.

**Independent Test**: Can be fully tested by verifying the `ProviderConfiguration` entity and its value objects contain all fields needed for discovery, fetching, and extraction strategies.

**Acceptance Scenarios**:

1. **Given** a provider configuration entity, **When** I access the discovery strategy, **Then** I can determine if it's `Api` or `Crawl` and retrieve the corresponding settings (ApiSettings or CrawlSettings)
2. **Given** a provider configuration entity, **When** I access the fetching strategy, **Then** I can determine if it's `Api`, `StaticHtml`, or `DynamicHtml` and retrieve appropriate settings
3. **Given** a provider configuration entity, **When** I access extraction selectors, **Then** I can retrieve CSS selectors for all recipe properties (title, ingredients, instructions, etc.)
4. **Given** a provider configuration entity, **When** I access rate limit settings, **Then** I can retrieve requests-per-minute, delay, and retry configuration

---

### User Story 5 - Test Provider Configuration Before Saving (Priority: P3 - DEFERRED)

As an operator, I want to test my CSS selectors against a sample recipe URL before finalizing the configuration so that I can verify the selectors work correctly.

**Why this priority**: This is deferred to the API/Web UI phase. For now, operators can validate configurations by running the recipe engine against a test provider and inspecting logs/results.

**Deferred to**: Future API/Web UI implementation phase

**Acceptance Scenarios**: (To be defined in future spec)

---

### Edge Cases

- What happens when a provider configuration is deleted while recipes from that provider exist? Recipes retain their provider name for attribution, but no new recipes are discovered.
- How does the system handle multiple providers with overlapping URL patterns? Providers are processed in priority order; first matching provider wins.
- What happens when CSS selectors return multiple matches? For single-value fields (title), use first match; for multi-value fields (ingredients), collect all matches.
- How does the system handle provider configurations with invalid CSS selectors? Validation occurs on save; runtime errors are logged and the extraction is marked as failed.
- What happens when a schema migration fails mid-execution? Migrations are idempotent and logged; failed migrations prevent application startup with a clear error message. Documents are not partially migrated.
- What happens when selector validation fails for only some selectors in a document? Validation fails the entire save operation with a detailed error listing all invalid selectors. Partial saves are not allowed.
- What happens when the cache TTL expires during a batch operation? Each cache access independently checks TTL; operations in progress use their already-retrieved configuration instance.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow users to create new provider configurations with unique provider names
- **FR-002**: System MUST persist provider configurations including: provider name, display name, base URL, enabled status, discovery strategy, fetching strategy, and CSS selectors
- **FR-003**: System MUST support two discovery strategies: API-based discovery and HTML crawl discovery
- **FR-004**: System MUST support three fetching strategies: API fetch, static HTML fetch, and dynamic HTML fetch (JavaScript rendering)
- **FR-005**: System MUST allow CSS selectors to be configured for the following recipe properties: title, description, ingredients (list), instructions (list), prep time, cook time, total time, servings, image URL, author, cuisine, difficulty, and nutritional information
- **FR-006**: System MUST allow users to enable or disable provider configurations without deletion
- **FR-007**: System MUST validate provider configurations before saving (required fields, valid URLs, valid CSS selectors)
- **FR-008**: System MUST allow users to edit existing provider configurations
- **FR-009**: System MUST allow users to delete provider configurations (with confirmation) — *For this phase: handled via MongoDB direct access; programmatic delete deferred to API phase*
- **FR-010**: Recipe engine MUST read provider configurations from the database and apply them during recipe processing
- **FR-011**: Recipe engine MUST respect the enabled/disabled status of provider configurations
- **FR-012**: System MUST support provider-specific rate limiting configuration (requests per minute, delay between requests)
- **FR-013**: System MUST support configurable retry behavior per provider (max retries, retry delay)
- **FR-014**: System MUST support URL pattern matching to identify recipe pages (include patterns, exclude patterns)

### Testing & Quality Requirements (MANDATORY)

- **Unit Tests**:
  - Provider configuration entity validation logic
  - CSS selector validation utilities
  - Discovery strategy selection logic
  - Fetching strategy selection logic
  - Provider configuration repository operations (using in-memory/mock database)

- **Integration Tests**:
  - Provider configuration CRUD operations against MongoDB (using Testcontainers)
  - Provider configuration repository loads configurations correctly
  - Cache TTL behavior (configuration refresh after expiration)
  - Cache returns same instance within TTL window

- **Integration Tests** (Testcontainers):
  - Insert provider configuration in MongoDB → verify repository loads it
  - Update provider configuration → verify cache returns updated config after TTL
  - Query enabled providers → verify filtering works correctly

- **CI Jobs**: Tests run in `deploy.yml` workflow as part of the build-api and build-recipe-engine jobs
- **Coverage Threshold**: Maintain existing coverage levels; new code must have >80% coverage

### Key Entities

- **ProviderConfiguration**: Core aggregate root representing a recipe provider's settings. Contains provider identity (name, display name, base URL), operational settings (enabled, priority), discovery strategy, fetching strategy, extraction selectors, and rate limit settings.

- **DiscoveryStrategy**: Enum/discriminated union indicating how recipe URLs are discovered. Options:
  - `Api` — Discover recipes via provider's API (requires endpoint, auth, pagination settings)
  - `Crawl` — Discover recipes by crawling HTML pages (requires seed URLs, link patterns)

- **FetchingStrategy**: Enum/discriminated union indicating how recipe content is retrieved. Options:
  - `Api` — Fetch structured data from provider's API
  - `StaticHtml` — Fetch HTML via simple HTTP GET (no JavaScript rendering)
  - `DynamicHtml` — Fetch HTML using browser automation (JavaScript rendering required)

- **ExtractionSelectors**: Value object containing CSS selectors for all recipe properties. Each selector can have a primary selector and optional fallback selectors for resilience.

- **RateLimitSettings**: Value object containing requests-per-minute limit, delay between requests, concurrent request limit, and retry/backoff settings.

- **ApiSettings**: Value object for API-based strategies containing endpoint URL, authentication method, headers, and pagination parameters.

- **CrawlSettings**: Value object for crawl-based strategies containing seed URLs, URL include/exclude patterns, max depth, and link extraction selectors.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can define a complete provider configuration document in MongoDB with clear schema documentation
- **SC-002**: The provider configuration repository correctly loads and caches configurations from MongoDB
- **SC-003**: Configuration changes are reflected within the cache TTL window (default 30 seconds) without application restart
- **SC-004**: Domain model exposes all settings needed for future saga implementation (discovery, fetching, extraction, rate limiting)
- **SC-005**: System supports loading at least 20 provider configurations without performance degradation
- **SC-006**: (DEFERRED) Provider configuration test feature provides extraction preview within 10 seconds

### Performance & Scalability Targets (MANDATORY when applicable)

- Provider configuration load time: p95 < 100ms for loading all configurations at engine startup
- Cache refresh time: p95 < 100ms for reloading configurations from MongoDB
- Cache hit rate: >95% during normal operation (validates TTL is appropriately set)
- Configuration document size: Support documents up to 64KB (well within MongoDB limits)
- Observability metrics latency: <1ms overhead for emitting cache hit/miss counters
- Observability: Provider configuration usage logged with provider name, strategy used, and extraction success/failure rates

## Out of Scope

- **Recipe Processing Saga implementation** — This spec covers only the configuration infrastructure, not the saga that consumes it
- **Discovery service implementation** — The saga will implement discovery logic using provider configurations
- **Fetching service implementation** — The saga will implement fetching logic (static/dynamic HTML, API)
- **Extraction service implementation** — The saga will implement CSS selector-based extraction
- **API endpoints** — Deferred to future phase
- **Web UI** — Deferred to future phase
- **Browser automation setup** — Infrastructure concern for dynamic HTML fetching, separate from this spec

## Assumptions

- Operators have MongoDB access (direct database queries, MongoDB Compass, or similar tools)
- MongoDB is the persistence layer for provider configurations (consistent with existing infrastructure)
- The recipe engine runs as a background worker that can reload configurations via cache TTL without full restart
- Browser automation for dynamic HTML fetching will use an existing solution (e.g., Playwright, Puppeteer) that is already available or will be added as a separate infrastructure concern
- API and Web UI for provider configuration management will be added in a future phase

## Clarifications

### Session 2025-11-25

- Q: Should provider configurations be stored in code/config files or database? → A: Runtime settings in MongoDB with caching
- Q: How should the recipe-engine worker be notified of configuration changes? → A: TTL-based cache expiration only
- Q: Where should the cache TTL value be configured? → A: appsettings.json with environment variable override
- Q: What is the scope of this feature? → A: Recipe-engine only; API/Web UI deferred to future work
- Q: Does this feature include implementing the recipe processing saga? → A: No, only the provider configuration infrastructure that the saga will consume
- Q: Where should domain entities live? → A: New `EasyMeals.Domain` shared package (prepares for future Recipe migration)

## Architectural Constraints

### Data Abstraction for Public Repository

Provider configurations MUST be stored as **runtime settings in MongoDB** (not in code, appsettings, or config files) to:

1. **Avoid licensing/legal issues** - Provider-specific details (URLs, selectors, API endpoints) are kept out of the public GitHub repository
2. **Enable runtime flexibility** - Configurations can be added, modified, or removed without code deployments
3. **Support private customization** - Each deployment can have its own provider configurations

### Infrastructure Pattern

**Shared Package Structure:**
- `EasyMeals.Domain` (NEW) — Domain entities: `ProviderConfiguration` aggregate root + value objects
- `EasyMeals.Persistence.Abstractions` — Repository interface: `IProviderConfigurationRepository`
- `EasyMeals.Persistence.Mongo` — MongoDB document: `ProviderConfigurationDocument` + repository implementation

**Implementation Details:**
- Provider configurations are persisted using the existing `EasyMeals.Persistence.Mongo` shared package
- A `ProviderConfigurationDocument` will be added to the Mongo persistence layer (following the pattern of `RecipeDocument`)
- The `recipe-engine` Infrastructure project will reference the shared packages (API integration deferred)
- Configurations are cached in-memory with configurable TTL (default 30 seconds) to reduce database load
- Cache TTL is configured via `appsettings.json` with environment variable override (standard .NET configuration pattern)
- Cache invalidation uses TTL-based expiration only—when cache expires, next access fetches fresh data from MongoDB
- No event-driven or message-bus invalidation required; eventual consistency is acceptable for configuration changes

### Future Extensibility (Design Considerations)

- New `EasyMeals.Domain` package prepares for future `Recipe` domain migration (keeps domain entities in one shared location)
- Domain entities and repository interfaces will be designed to support future API/Web UI consumption
- The `IProviderConfigurationRepository` interface will include CRUD operations even though this phase only uses read operations
- Document schema will be versioned to support future migrations
- Shared persistence packages ensure API can reuse the same domain model and data access layer later

### Security Constraints: Secrets Handling

> **⚠️ IMPORTANT: Never store raw secrets (API keys, tokens, passwords) in MongoDB.**

Provider configurations may require authentication credentials for API-based providers. Follow these guidelines:

1. **Secret References Only**: Store secret *references* (e.g., `"apiKey": "secret:provider-hellofresh-apikey"`) in MongoDB, not actual values.
2. **Runtime Resolution**: At runtime, resolve secret references from a secure store:
   - Azure Key Vault (preferred for Azure deployments)
   - AWS Secrets Manager
   - HashiCorp Vault
   - Environment variables (for local development only)
3. **Headers with Secrets**: For `ApiSettings.Headers`, use the pattern `"Authorization": "secret:provider-{name}-auth-header"` and resolve at fetch time.
4. **Rotation Support**: Secret references enable credential rotation without configuration changes.

**Example Configuration (MongoDB)**:
```json
{
  "apiSettings": {
    "endpoint": "https://api.provider.com/v1/recipes",
    "authMethod": "ApiKey",
    "headers": {
      "X-Api-Key": "secret:provider-example-apikey"
    }
  }
}
```

### CSS Selector Validation

CSS selectors in `ExtractionSelectors` MUST be validated before persistence:

1. **Validation Library**: Use [AngleSharp](https://anglesharp.github.io/) to parse and validate CSS selectors at save time.
2. **Validation Rules**:
   - Selector must parse without errors
   - Selector must not be empty for required fields (title, description, ingredients, instructions)
   - Fallback selectors are optional but must also parse if provided
3. **Runtime Behavior**: If a selector fails to match at extraction time, log a warning and use the fallback selector if available.
4. **Test Coverage**: Unit tests must verify selector validation rejects malformed selectors.

**Example Validation**:
```csharp
public static bool IsValidCssSelector(string selector)
{
    if (string.IsNullOrWhiteSpace(selector)) return false;
    try
    {
        var parser = new AngleSharp.Css.Parser.CssSelectorParser();
        var result = parser.ParseSelector(selector);
        return result is not null;
    }
    catch { return false; }
}
```

### Schema Versioning & Migration Plan

The `ProviderConfigurationDocument` includes a `version` field to support schema evolution:

1. **Version Field**: Every document has a `version` field (starts at 1).
2. **Migration Strategy**:
   - **Additive changes** (new optional fields): No migration needed; new fields default to null/empty.
   - **Breaking changes** (field renames, type changes): Implement a migration script to update existing documents.
   - **Deprecation**: Mark old fields with `[Obsolete]` for one release cycle before removal.
3. **Migration Execution**: Run migrations via a startup hosted service (`IMigrationRunner`) before the application accepts requests.
4. **Rollback Plan**: Keep backup of documents before migration; migrations should be idempotent.

**Example Migration**:
```csharp
// Migration from v1 to v2: Rename "cssSelectors" to "extractionSelectors"
public class MigrateV1ToV2 : IDocumentMigration
{
    public int FromVersion => 1;
    public int ToVersion => 2;
    
    public BsonDocument Migrate(BsonDocument doc)
    {
        if (doc.Contains("cssSelectors"))
        {
            doc["extractionSelectors"] = doc["cssSelectors"];
            doc.Remove("cssSelectors");
        }
        doc["version"] = ToVersion;
        return doc;
    }
}
```

### Observability: Metrics & Logging

The provider configuration system MUST expose the following observability signals:

**Structured Logging**:
- `ProviderConfigurationLoaded` — When configs are loaded from DB (count, duration)
- `ProviderConfigurationCacheHit` — When cache serves a request
- `ProviderConfigurationCacheMiss` — When cache misses and DB is queried
- `ProviderConfigurationCacheCleared` — When cache is manually cleared
- `ProviderConfigurationValidationFailed` — When a config fails validation (with details)

**Metrics** (via `System.Diagnostics.Metrics` or OpenTelemetry):
| Metric | Type | Description |
|--------|------|-------------|
| `provider_config_cache_hits_total` | Counter | Total cache hits |
| `provider_config_cache_misses_total` | Counter | Total cache misses |
| `provider_config_load_duration_ms` | Histogram | Time to load configs from DB |
| `provider_config_count` | Gauge | Number of enabled provider configs |
| `provider_config_validation_errors_total` | Counter | Validation failures by provider |

### Index Enforcement

MongoDB indexes are critical for performance and uniqueness. Indexes MUST be:

1. **Created on Startup**: Use `IndexCreationHostedService` (existing pattern in `EasyMeals.Persistence.Mongo.Extensions`).
2. **Verified in Integration Tests**: Add a test that asserts indexes exist after startup.
3. **Index Definitions**:
   ```javascript
   // Unique constraint on provider name
   { "providerName": 1 }, { unique: true, name: "idx_provider_name_unique" }
   
   // Query optimization for enabled providers
   { "isEnabled": 1, "isDeleted": 1, "priority": -1 }, { name: "idx_enabled_priority" }
   ```
4. **Test Coverage**:
   ```csharp
   [Fact]
   public async Task Indexes_AreCreatedOnStartup()
   {
       var indexes = await _collection.Indexes.ListAsync();
       var indexNames = indexes.ToList().Select(i => i["name"].AsString);
       indexNames.Should().Contain("idx_provider_name_unique");
       indexNames.Should().Contain("idx_enabled_priority");
   }
   ```
