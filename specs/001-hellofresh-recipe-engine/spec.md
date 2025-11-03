# Feature Specification: Complete Recipe Engine for HelloFresh

**Feature Branch**: `001-hellofresh-recipe-engine`  
**Created**: November 2, 2025  
**Status**: Draft  
**Input**: User description: "Help me finish Recipe Engine for HelloFresh implementation. I want it to be extendible as we'll have multiple recipe providers. Things such as ingredient normalization (i.e., HelloFresh proprietary ingredients can be normalized), robust configuration (should be a base setup already for this), and finishing our processing Saga in a complete manner. I want our Recipe Engine to process ~ 100 recipes or 1 hour at a time for each provider whichever comes first (Coolify will handle the scheduling). We should be very stealthly and not get our IP rejected/banned from these sites. We should be curteous and not overwhelem or DDoS these sites, etc."

## User Scenarios & Testing _(mandatory)_

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.

  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Process HelloFresh Recipes Within Rate & Time Limits (Priority: P1)

**Context**: The meal planning platform needs to regularly scrape HelloFresh recipes in batches, respecting both time constraints and volume limits to avoid overwhelming the provider's infrastructure or triggering IP bans.

**Why this priority**: This is the core capability that enables the recipe engine to function reliably in production. Without proper throttling, the system risks being blocked from the provider entirely, which breaks the entire meal planning feature.

**Independent Test**: Can be fully tested by mocking HelloFresh endpoints and verifying that a batch of 100 recipes (or fewer if time limit reached) is processed, all within a configured time window (e.g., 1 hour), with appropriate delays between requests.

**Acceptance Scenarios**:

1. **Given** a configured batch size of 100 recipes and 1-hour time window, **When** the recipe engine starts processing, **Then** it processes up to 100 recipes within the 1-hour window regardless of how many URLs are available
2. **Given** a batch size of 100 recipes and 1-hour time window, **When** only 50 recipes are available for processing, **Then** the engine processes all 50 and completes successfully before the 1-hour timeout
3. **Given** processing has taken 59 minutes with 80 recipes processed, **When** only 2 minutes remain in the window, **Then** the engine completes the current recipe and stops processing gracefully (does not start new requests)
4. **Given** a configured delay between requests (e.g., 2 seconds), **When** processing 100 recipes, **Then** the engine respects the delay and does not send requests in rapid succession
5. **Given** the recipe engine is processing during peak hours, **When** it exceeds the time window, **Then** it saves its state and can resume on the next scheduled run

---

### User Story 2 - Normalize HelloFresh Proprietary Ingredients (Priority: P1)

**Context**: HelloFresh uses proprietary ingredient codes and names (e.g., "HF-BROCCOLI-FROZEN-012") that differ from standard grocery ingredient terminology. The system must normalize these to canonical forms for consistency across multiple recipe providers.

**Why this priority**: Without normalization, ingredient data is incompatible across different providers, breaking recipe comparison and meal planning features. Users see inconsistent ingredient names depending on provider source.

**Independent Test**: Can be fully tested by providing a set of HelloFresh proprietary ingredient identifiers and verifying that each is mapped to a standardized canonical ingredient form (e.g., "HF-BROCCOLI-FROZEN-012" → "broccoli, frozen"). The mapping should be queryable and auditable.

**Acceptance Scenarios**:

1. **Given** a HelloFresh ingredient with proprietary code (e.g., "HF-BROCCOLI-FROZEN-012"), **When** the recipe is processed, **Then** the ingredient is normalized to a canonical form (e.g., "broccoli, frozen") and stored with both the provider code and canonical form
2. **Given** multiple HelloFresh recipes with the same proprietary ingredient, **When** normalized, **Then** all recipes reference the same canonical ingredient (deduplication)
3. **Given** an unmapped proprietary ingredient code, **When** encountered during processing, **Then** the engine logs a warning, stores the raw code for manual review, and continues processing (non-blocking)
4. **Given** new ingredient mappings are added to the normalization database, **When** recipes are reprocessed, **Then** the new mappings are applied without data corruption
5. **Given** a canonical ingredient mapping exists, **When** a user queries recipes by ingredient, **Then** they can find recipes regardless of whether the ingredient was scraped from HelloFresh or another provider

---

### User Story 4 - Robust Configuration for Multiple Providers (Priority: P2)

**Context**: The recipe engine must support multiple recipe providers (HelloFresh, Blue Apron, etc.) with provider-specific settings (delays, timeouts, retry policies, rate limits). Configuration should be flexible, environment-aware, and easy to extend for new providers.

**Why this priority**: This enables the system to scale to additional providers without code changes, and allows operational teams to tune behavior per provider without developer intervention. Reduces time-to-market for new providers.

**Independent Test**: Can be fully tested by loading provider configurations from multiple sources (appsettings.json, environment variables, config server), verifying that provider-specific settings are correctly applied, and that adding a new provider requires only configuration changes.

**Acceptance Scenarios**:

1. **Given** separate configurations for HelloFresh and Blue Apron providers, **When** the application loads settings, **Then** each provider uses its own delay, timeout, and retry settings without conflict
2. **Given** an environment variable `RECIPE_ENGINE_DELAY_SECONDS=3` set at runtime, **When** the saga processes, **Then** this environment variable overrides the appsettings.json value
3. **Given** a new provider configuration is added to appsettings.json, **When** the application restarts, **Then** the new provider is available without code changes
4. **Given** provider settings include batch size and time window, **When** the saga processes, **Then** these settings determine the actual processing behavior (e.g., 100 recipes or 1 hour)
5. **Given** a provider configuration is marked as "enabled: false", **When** the engine starts, **Then** that provider is skipped during processing

---

### User Story 5 - Stealth & Courtesy: IP Ban Avoidance & Rate Limiting (Priority: P2)

**Context**: The recipe engine must implement practices to avoid IP bans and provider blocking: variable delays between requests, randomized user agents, respectful crawl headers, connection pooling, and configurable rate limits to ensure the system is perceived as a well-behaved crawler, not a DDoS attack.

**Why this priority**: Provider sites actively block aggressive crawlers. Without these practices, the system risks being permanently banned, breaking the meal planning feature. Secondary priority because it enhances reliability rather than enabling core functionality.

**Independent Test**: Can be fully tested by intercepting HTTP requests and verifying that delays vary (not fixed), user agents rotate, crawl headers are present, connection pooling is used, and the request rate respects configured limits (e.g., max 5 requests per second).

**Acceptance Scenarios**:

1. **Given** the engine is configured with a 2-second delay between requests, **When** processing recipes, **Then** delays vary randomly between 1.5-2.5 seconds (not fixed) to appear more human-like
2. **Given** the engine is making HTTP requests to a provider, **When** each request is sent, **Then** it includes a user agent string that rotates between realistic values (not a static bot user agent)
3. **Given** the engine connects to a provider website, **When** HTTP requests are made, **Then** they include appropriate crawl headers (e.g., `Accept-Language`, `Accept-Encoding`) matching a real browser
4. **Given** the engine processes 1000 recipes across multiple batches, **When** HTTP connections are used, **Then** connections are reused via a connection pool (not recreated for each request)
5. **Given** a provider rate limit is configured to 10 requests per minute, **When** processing occurs, **Then** the engine never exceeds 10 requests per minute and queues excess requests (does not drop them)

### Edge Cases

- What happens when HelloFresh is unreachable for an entire batch window? (Engine logs and exits gracefully, state saved for retry)
- What happens when an ingredient cannot be normalized (not in mapping database)? (Log warning, store raw code, continue processing)
- What happens if the processing saga crashes mid-batch? (State is persisted, next run resumes from saved state)
- What happens if the configured batch size is larger than available recipes? (Engine processes all available recipes and completes)
- What happens if the configured time window is too short to process even one recipe? (Engine processes at least one recipe, then exits, time window extended on next run)
- What happens if an HTTP request is rate-limited (429 response)? (Engine backs off, queues request, retries with exponential backoff)
- What happens if a recipe URL returns invalid/malformed data? (Engine logs error, records URL for manual review, continues processing remaining URLs)

## Requirements _(mandatory)_

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST process up to 100 recipes per batch (configurable), whichever occurs first: batch size reached or configured time window elapsed (e.g., 1 hour)
- **FR-002**: System MUST implement configurable delays between HTTP requests (e.g., 2 seconds minimum) to respect provider rate limits
- **FR-003**: System MUST normalize HelloFresh proprietary ingredient identifiers to canonical forms using a configurable mapping database
- **FR-004**: System MUST support multiple recipe providers through provider-specific configuration (delays, timeouts, retry counts, batch size, time window)
- **FR-005**: System MUST persist processing saga state after each batch, enabling resumption from the last processed item on application restart
- **FR-006**: System MUST implement graceful error handling with compensation logic (retry failed items, skip permanently invalid items, log for manual review)
- **FR-007**: System MUST implement stealth measures: randomized delays between requests, rotating user agents, appropriate crawl headers, connection pooling
- **FR-008**: System MUST implement rate limiting to respect provider limits (e.g., max 10 requests per minute), queuing excess requests for later processing
- **FR-009**: System MUST provide comprehensive logging of processing progress, errors, and state transitions for operational visibility
- **FR-010**: System MUST prevent duplicate recipe ingestion by tracking processed recipe URLs or fingerprints
- **FR-011**: System MUST support adding new recipe providers without code changes (configuration-driven provider discovery)
- **FR-012**: System MUST store both the raw provider ingredient identifier and normalized canonical form for auditability

### Key Entities

- **Recipe Batch**: A collection of up to 100 recipes (or fewer) processed within a configurable time window; associated with a provider and processing saga
- **Ingredient Normalization Mapping**: Maps provider-specific ingredient codes (e.g., "HF-BROCCOLI-FROZEN-012") to canonical ingredient forms (e.g., "broccoli, frozen"); supports multiple providers
- **Processing Saga State**: Tracks the progress of a multi-step processing workflow (discovery → fingerprinting → processing → persistence); persisted for resumability
- **Provider Configuration**: Settings for a specific recipe provider including batch size, time window, delays, timeouts, retry counts, and enabled status
- **Recipe Fingerprint**: A content hash or identifier used to detect duplicate or previously processed recipes; prevents redundant processing
- **Rate Limit Token**: Tracks remaining request quota per provider to enforce rate limits (e.g., 10 requests per minute); automatically refills

## Success Criteria _(mandatory)_

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: Recipe engine processes 100 recipes (or available quantity) within a configured time window (default: 1 hour) with zero data loss even if application restarts mid-batch
- **SC-002**: At least 95% of HelloFresh ingredient identifiers are successfully normalized to canonical forms on first attempt; unmapped ingredients logged for manual review
- **SC-003**: Processing saga completes full workflow (discovery → fingerprinting → processing → persistence) with documented state transitions; saga can resume from any step after restart
- **SC-004**: New recipe provider can be added (e.g., Blue Apron) by only modifying configuration and provider-specific extractor; no changes to saga orchestration code
- **SC-005**: Recipe engine respects configured rate limits (e.g., max 10 req/min) with zero provider rate limit violations (no 429 responses after first backoff)
- **SC-006**: HTTP requests include randomized delays (±20% variance) and rotating user agents; connections reused via pooling; no fixed bot patterns detected by monitoring
- **SC-007**: Operational team can adjust batch size, time window, delays, and retry counts via configuration without code deployment
- **SC-008**: No duplicate recipes are ingested; duplicate detection rate is 100% (all previously processed URLs are skipped)
- **SC-009**: Comprehensive processing logs (JSON formatted) include timing, success rates, error details, and state transitions; log volume does not exceed 100 MB per 1-hour batch
- **SC-010**: Processing saga recovers from transient errors (network, timeouts) with exponential backoff; permanent errors are recorded for manual review and do not halt processing

---

## Assumptions & Dependencies

### Assumptions

- HelloFresh website structure remains stable (no major layout changes that break existing extractors); if changes occur, provider-specific code is updated separately
- MongoDB is available and configured (shared infrastructure handles connection management)
- Coolify schedules recipe engine batch jobs (no scheduler built into engine itself)
- Provider rate limits are publicly available or can be determined empirically during development
- Team has access to test/development credentials for HelloFresh (if required for testing)
- Normalization mapping database is maintained separately (not in scope of this feature, but must be queryable by the engine)

### Technical Assumptions

- The existing DDD architecture (Domain, Application, Infrastructure layers) is sufficient for implementation
- The existing `IRecipeProcessingSaga` interface can be completed without breaking changes to other systems
- MongoDB indexing strategy supports efficient querying of processed URLs/fingerprints for duplicate detection
- HTTP client library (HttpClient pool) is available via dependency injection

### Operational Assumptions

- Coolify (external scheduler) will invoke the recipe engine on a regular schedule (e.g., hourly)
- Provider authentication (if required) is handled via environment variables or secrets management
- Monitoring/observability infrastructure is available to capture logs and metrics

### External Dependencies

- HelloFresh website (or test endpoints) for scraping
- MongoDB for persistence and state management
- .NET 8 SDK for C# development and testing
- HTTP client libraries (.NET standard)

---

## Notes

- Ingredient normalization mapping should be extensible to support future providers (Blue Apron, EveryPlate, etc.) with different ingredient codes
- Rate limiting strategy should be configurable per provider to support different backend capacities
- The processing saga should emit domain events at key milestones (e.g., BatchStarted, BatchCompleted, ItemProcessed) for event-driven architecture patterns
- Configuration validation should occur at startup to fail fast if provider settings are invalid
