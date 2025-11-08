# Technical Research: Recipe Engine Architecture

**Feature**: Multi-Provider Recipe Engine with Dynamic Discovery  
**Branch**: `001-hellofresh-recipe-engine`  
**Date**: November 2, 2025

## Overview

This document captures the technical research for implementing a production-ready recipe processing engine using Domain-Driven Design with Saga orchestration, Strategy pattern for discovery, and MongoDB persistence via existing shared infrastructure. The engine supports multiple recipe providers through database-driven configuration, keeping sensitive provider data (URLs, rate limits, TOS-related settings) private and secure.

## Architecture Pattern Research

### 1. Saga Pattern for Workflow Orchestration

**Decision**: Use **MassTransit Saga** for orchestrating the multi-step recipe processing workflow.

**Rationale**:

- Recipe processing has 4 distinct stages: Discovery → Fingerprinting → Processing → Persistence
- Each stage can fail independently and requires compensating transactions (e.g., retry, skip, log)
- State must persist across application restarts (Coolify can restart containers at any time)
- MassTransit provides durable saga state management with MongoDB persistence
- Built-in correlation ID tracking for distributed workflows

**Trade-offs**:

- **Pro**: Crash recovery without data loss; explicit state transitions; testable state machine
- **Pro**: Saga state persisted in MongoDB automatically; resume from any stage
- **Pro**: MassTransit supports domain events (BatchStarted, BatchCompleted, etc.)
- **Con**: Adds dependency on MassTransit (but already used in shared infrastructure)
- **Con**: Learning curve for saga state machine syntax (mitigated by existing IRecipeProcessingSaga interface)

**Implementation Notes**:

- `RecipeProcessingSaga` implements `MassTransitStateMachine<RecipeProcessingSagaState>`
- Saga state includes: `CurrentStage`, `ProcessedCount`, `FailedCount`, `StartedAt`, `LastProcessedUrl`
- Transitions: `Idle → Discovering → Fingerprinting → Processing → Persisting → Completed`
- Compensation: Retry transient errors (network), skip permanent errors (invalid data), log all failures

**References**:

- MassTransit Sagas: https://masstransit.io/documentation/patterns/saga
- Saga State Machine pattern: https://microservices.io/patterns/data/saga.html

---

### 2. Strategy Pattern for Discovery

**Decision**: Use **Strategy Pattern** with three concrete implementations for recipe URL discovery.

**Rationale**:

- Different providers use different technologies (static HTML, JavaScript-rendered sites, APIs)
- Discovery strategy must be configurable per provider without code changes
- Some providers require dynamic crawling (JavaScript-rendered, recursive link discovery)
- Other providers may use APIs or static sitemaps

**Strategy Implementations**:

1. **StaticCrawlDiscoveryService**:
   - For providers with static HTML sitemaps or recipe index pages
   - Uses HttpClient with HtmlAgilityPack for parsing
   - Configuration: `recipe-root` URL, CSS selectors for links
   - Example: Scrape `/recipes` page, extract all `<a href="/recipe/...">` links

2. **DynamicCrawlDiscoveryService**:
   - For JavaScript-rendered sites where content loads dynamically
   - Uses Playwright (headless Chromium) for browser automation
   - Recursive discovery: Start at `recipe-root`, follow pagination/category links
   - Configuration: `recipe-root` URL, wait selectors, max depth, pagination patterns
   - Example: Navigate to provider recipes page, wait for JS to load cards, extract URLs, follow pagination

3. **ApiDiscoveryService**:
   - For providers with public or partner APIs
   - Uses HttpClient with JSON deserialization
   - Configuration: API endpoint, authentication, pagination parameters
   - Example: Call `GET /api/recipes?page=1&limit=100`, extract URLs from JSON

**Trade-offs**:

- **Pro**: Extensible for new providers; no saga code changes
- **Pro**: Configuration-driven behavior (provider config specifies strategy)
- **Con**: Playwright adds ~200MB Docker image size (acceptable for dynamic crawling)
- **Con**: Dynamic crawling slower than static (mitigated by batch time window)

**Implementation Notes**:

- Interface: `IDiscoveryService { Task<IEnumerable<string>> DiscoverRecipeUrlsAsync(ProviderConfiguration config, CancellationToken cancellationToken); }`
- Configuration: `ProviderConfiguration.DiscoveryStrategy` enum (Static, Dynamic, Api)
- DI registration: Register all three implementations, resolve by strategy enum at runtime
- Playwright initialization: Lazy singleton per app lifetime (expensive to create)

**References**:

- Strategy Pattern: https://refactoring.guru/design-patterns/strategy
- Playwright .NET: https://playwright.dev/dotnet/

---

### 3. Rate Limiting with Token Bucket Algorithm

**Decision**: Use **Token Bucket** algorithm for rate limiting HTTP requests per provider.

**Rationale**:

- Providers have different rate limits (typically 5-15 requests per minute)
- Token bucket allows bursts (consume multiple tokens quickly) while enforcing average rate
- More flexible than fixed-interval rate limiting (e.g., "wait 6 seconds between requests")
- Standard algorithm used by cloud providers (AWS, Azure) for API throttling

**Token Bucket Implementation**:

- **Tokens**: Represent available request quota; bucket has max capacity (e.g., 10 tokens)
- **Refill Rate**: Tokens added at constant rate (e.g., 10 tokens/minute = 1 token/6 seconds)
- **Request Flow**: Each HTTP request consumes 1 token; if no tokens available, request waits
- **Burst Handling**: If 10 tokens available, can make 10 requests immediately (then wait for refills)

**Configuration**:

- `ProviderConfiguration.MaxRequestsPerMinute` (loaded from MongoDB per provider)
- `ProviderConfiguration.BurstSize` (optional; defaults to MaxRequestsPerMinute)

**Trade-offs**:

- **Pro**: Flexible burst handling; respects average rate limit without strict spacing
- **Pro**: Standard algorithm; well-understood and tested
- **Con**: Slightly more complex than fixed delay (mitigated by library or simple implementation)

**Implementation Notes**:

- Interface: `IRateLimiter { Task WaitForTokenAsync(string providerId, CancellationToken cancellationToken); }`
- Token bucket per provider (identified by `providerId`)
- Thread-safe (use `SemaphoreSlim` for token consumption)
- Background refill task (runs every second, adds tokens at configured rate)

**References**:

- Token Bucket Algorithm: https://en.wikipedia.org/wiki/Token_bucket
- Polly Rate Limiter: https://github.com/App-vNext/Polly (alternative: use Polly's built-in rate limiter)

---

### 4. Ingredient Normalization with Mapping Database

**Decision**: Use **MongoDB collection** for ingredient normalization mappings with provider-specific keys.

**Rationale**:

- Providers use proprietary ingredient codes (e.g., "PROVIDER-BROCCOLI-FROZEN-012")
- Normalization must map provider codes to canonical forms (e.g., "broccoli, frozen")
- Mappings must be queryable, auditable, and extensible for new providers
- MongoDB document model supports flexible schema (provider-specific fields)

**Mapping Schema**:

```csharp
public class IngredientMapping
{
    public ObjectId Id { get; set; }
    public string ProviderId { get; set; }        // e.g., "provider_001"
    public string ProviderCode { get; set; }      // e.g., "BROCCOLI-FROZEN-012"
    public string CanonicalForm { get; set; }     // e.g., "broccoli, frozen"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Normalization Flow**:

1. Recipe parser extracts ingredient codes from scraped HTML/JSON
2. `IIngredientNormalizer.NormalizeAsync(providerId, providerCode)` queries MongoDB
3. If mapping exists, return `CanonicalForm`; if not, log warning and return `null`
4. Store both `ProviderCode` and `CanonicalForm` in Recipe entity for auditability

**Unmapped Ingredient Handling**:

- Log warning: `"Unmapped ingredient: Provider={providerId}, Code={providerCode}, RecipeUrl={url}"`
- Store raw `ProviderCode` in Recipe entity (do not block processing)
- Emit domain event: `IngredientMappingMissingEvent` for operational visibility
- Manual review: Ops team adds mappings to MongoDB; reprocess recipes later

**Trade-offs**:

- **Pro**: Flexible schema; easy to add new providers and mappings
- **Pro**: Auditability (both provider code and canonical form stored)
- **Con**: MongoDB query per ingredient (mitigated by caching or batch lookups)

**Implementation Notes**:

- Interface: `IIngredientNormalizer { Task<string?> NormalizeAsync(string providerId, string providerCode, CancellationToken cancellationToken); }`
- MongoDB collection: `ingredient_mappings` with compound index on `(ProviderId, ProviderCode)`
- Caching: In-memory cache (LRU) for frequently used mappings (e.g., "broccoli" appears in 100+ recipes)

**References**:

- MongoDB Indexes: https://www.mongodb.com/docs/manual/indexes/
- EasyMeals.Shared.Data: Existing `MongoRepository<T>` pattern

---

### 5. Recipe Fingerprinting for Duplicate Detection

**Decision**: Use **content-based fingerprinting** (hash of recipe URL + title + description) for duplicate detection.

**Rationale**:

- Recipe URLs may change (provider site redesign, URL slug updates)
- Duplicate detection must be robust against minor URL variations (e.g., query params)
- Content-based fingerprint captures recipe identity beyond URL
- Fingerprints stored in MongoDB for fast duplicate lookups

**Fingerprint Algorithm**:

```csharp
SHA256(url.Normalize() + title.Trim().ToLower() + description.Substring(0, 200).Trim().ToLower())
```

**Fingerprint Schema**:

```csharp
public class RecipeFingerprint
{
    public ObjectId Id { get; set; }
    public string FingerprintHash { get; set; }  // SHA256 hex string
    public string ProviderId { get; set; }       // e.g., "provider_001"
    public string RecipeUrl { get; set; }        // Original URL
    public DateTime ProcessedAt { get; set; }
}
```

**Duplicate Detection Flow**:

1. Generate fingerprint from scraped recipe data (URL + title + description)
2. Query MongoDB: `fingerprints.find({ FingerprintHash: hash })`
3. If exists, skip processing (log: "Duplicate recipe detected: {url}")
4. If not exists, process recipe and store fingerprint after successful persistence

**Trade-offs**:

- **Pro**: Robust against URL changes (content-based identity)
- **Pro**: Fast duplicate lookups (indexed hash field)
- **Con**: Requires partial recipe parsing before fingerprinting (mitigated by lightweight parse)

**Implementation Notes**:

- Interface: `IRecipeFingerprinter { Task<string> GenerateFingerprintAsync(string url, string title, string description); Task<bool> IsDuplicateAsync(string fingerprint, CancellationToken cancellationToken); }`
- MongoDB collection: `recipe_fingerprints` with unique index on `FingerprintHash`
- SHA256 implementation: `System.Security.Cryptography.SHA256`

**References**:

- Content-based fingerprinting: https://en.wikipedia.org/wiki/Fingerprint_(computing)
- SHA256 in .NET: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256

---

### 6. Stealth Measures for IP Ban Avoidance

**Decision**: Implement **randomized delays**, **rotating user agents**, and **connection pooling** for stealth.

**Rationale**:

- Providers block aggressive crawlers (fixed delays, bot user agents, high request rates)
- Randomized delays (±20% variance) mimic human browsing patterns
- Rotating user agents avoid detection by user-agent blocking rules
- Connection pooling reduces overhead (avoid creating new TCP connections per request)

**Stealth Implementations**:

1. **Randomized Delays**:
   - Configuration: `ProviderConfiguration.MinDelaySeconds` (e.g., 2 seconds)
   - Algorithm: `Random delay = MinDelay * (0.8 + Random.NextDouble() * 0.4)` (±20% variance)
   - Example: If `MinDelay = 2s`, actual delays: 1.6s, 2.3s, 1.9s, 2.4s (not fixed 2s)

2. **Rotating User Agents**:
   - Maintain list of realistic browser user agents (Chrome, Firefox, Safari, Edge)
   - Rotate per request (round-robin or random)
   - Example user agents:
     ```
     "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36"
     "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 Chrome/120.0.0.0"
     ```

3. **Connection Pooling**:
   - Use `HttpClient` with `SocketsHttpHandler` (built-in connection pooling)
   - Configuration: `PooledConnectionLifetime = 5 minutes` (avoid stale connections)
   - Single `HttpClient` instance per provider (singleton via DI)

4. **Crawl Headers**:
   - Include realistic headers: `Accept-Language: en-US,en;q=0.9`, `Accept-Encoding: gzip, deflate, br`
   - Avoid bot-specific headers (e.g., `X-Crawler: MyBot`)

**Trade-offs**:

- **Pro**: Reduces risk of IP bans; system perceived as human-like traffic
- **Pro**: Connection pooling improves performance (fewer TCP handshakes)
- **Con**: Randomized delays increase total batch processing time (mitigated by time window)

**Implementation Notes**:

- Randomized delay: `await Task.Delay(TimeSpan.FromSeconds(GetRandomDelay()))`
- User agent rotation: Load from configuration file (`appsettings.json`), rotate in-memory
- HttpClient pooling: Register as singleton in DI (`services.AddSingleton<IHttpClientFactory, ...>`)

**References**:

- HttpClient best practices: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
- Polly resilience: https://github.com/App-vNext/Polly (use for retries, circuit breaker)

---

## Technology Decisions

### HTTP Client: HttpClient with Polly

**Chosen**: .NET `HttpClient` with Polly resilience policies.

**Justification**:

- Built-in connection pooling (SocketsHttpHandler)
- Polly provides retries with exponential backoff, circuit breaker, timeout policies
- Standard .NET library; no additional dependencies for basic HTTP

**Configuration**:

```csharp
services.AddHttpClient<IDiscoveryService, StaticCrawlDiscoveryService>()
    .AddPolicyHandler(GetRetryPolicy())      // Retry 3 times with exponential backoff
    .AddPolicyHandler(GetCircuitBreakerPolicy())  // Open circuit after 5 failures
    .AddPolicyHandler(GetTimeoutPolicy());   // 30s timeout per request
```

**Alternatives Considered**:

- **RestSharp**: Higher-level API, but adds dependency; HttpClient sufficient
- **Flurl**: Fluent API, but adds dependency; HttpClient sufficient

---

### HTML Parsing: HtmlAgilityPack

**Chosen**: HtmlAgilityPack for static HTML parsing.

**Justification**:

- Industry-standard .NET HTML parser
- XPath and CSS selector support
- Handles malformed HTML gracefully

**Alternatives Considered**:

- **AngleSharp**: More modern, but HtmlAgilityPack has larger ecosystem and proven stability

---

### Dynamic Crawling: Playwright

**Chosen**: Playwright for JavaScript-rendered sites (dynamic discovery providers).

**Justification**:

- Official Microsoft-supported browser automation framework
- Headless Chromium for realistic browser behavior
- Async/await API for .NET
- Handles JavaScript, AJAX, infinite scroll, pagination

**Docker Considerations**:

- Playwright requires browser binaries (~200MB)
- Use official Playwright Docker image: `mcr.microsoft.com/playwright/dotnet:v1.40.0-focal`
- Pre-installed Chromium, Webkit, Firefox (no runtime download)

**Alternatives Considered**:

- **Selenium**: Older, less performant; Playwright is modern replacement
- **Puppeteer Sharp**: C# port of Puppeteer; Playwright has better .NET support

---

### Logging: Serilog with Structured Logging

**Chosen**: Serilog for structured logging (JSON format).

**Justification**:

- Structured logs (key-value pairs) for operational visibility
- Easy integration with log aggregation (e.g., Seq, ELK, Azure Monitor)
- Enrichment support (correlation ID, provider ID, recipe URL)
- Sinks for MongoDB, file, console (development)

**Log Schema**:

```json
{
  "timestamp": "2025-11-02T14:32:15Z",
  "level": "Information",
  "message": "Recipe processed successfully",
  "properties": {
    "providerId": "provider_001",
    "recipeUrl": "{sanitized_url}",
    "processingTime": "450ms",
    "correlationId": "abc123"
  }
}
```

**Alternatives Considered**:

- **Microsoft.Extensions.Logging**: Built-in, but less powerful than Serilog for structured logs

---

### Configuration: Database-Driven (MongoDB)

**Chosen**: MongoDB-based configuration storage with cached in-memory access.

**Justification**:

- **Security**: Sensitive provider URLs, rate limits, and TOS-related settings never committed to GitHub
- **Auditability**: Track who changed what configuration and when (audit trail)
- **Dynamic Updates**: Modify provider settings without redeployment or code changes
- **Environment Isolation**: Different configurations per environment (dev/staging/prod) via database connection
- **Compliance**: Provider-specific URLs and proprietary codes stay private

**Configuration Collection Schema**:

```csharp
[BsonCollection("provider_configurations")]
public class ProviderConfigurationDocument : BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string ProviderId { get; set; }            // e.g., "provider_001"
    public bool Enabled { get; set; }
    public string DiscoveryStrategy { get; set; }     // "Static", "Dynamic", "Api"
    public string RecipeRootUrl { get; set; }         // Stored privately in DB
    public int BatchSize { get; set; }
    public int TimeWindowMinutes { get; set; }
    public double MinDelaySeconds { get; set; }
    public int MaxRequestsPerMinute { get; set; }
    public int RetryCount { get; set; }
    public int RequestTimeoutSeconds { get; set; }

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; }             // Admin user who added provider
    public string? UpdatedBy { get; set; }
}
```

**Loading Strategy**:

- Load all enabled providers from MongoDB at startup via `IHostedService`
- Cache configurations in-memory (invalidate on TTL or manual refresh)
- Provide `IProviderConfigurationService` for saga to query by ProviderId

**Management**:

- CLI tool: `dotnet run -- config add --provider-id=provider_001 --strategy=Dynamic ...`
- Admin API endpoint: `POST /api/admin/providers` (future enhancement)
- Direct MongoDB insertion via Compass or mongosh for initial seeding

**Alternatives Considered**:

- **appsettings.json**: Rejected because provider URLs and rate limits would be committed to GitHub (TOS/PII risk)
- **Environment variables**: Rejected because managing 10+ providers with 8+ settings each is unwieldy

---

## Performance Considerations

### Target Metrics

- **100 recipes per hour**: ~36 seconds per recipe (including HTTP, parsing, normalization, persistence)
- **<500ms per recipe processing**: After HTTP fetch (parsing + normalization + persistence)
- **<100MB memory**: Saga state + HttpClient pool + discovery cache

### Optimizations

1. **Connection Pooling**: Reuse TCP connections (HttpClient singleton)
2. **Parallel Discovery**: Discover URLs in parallel (up to 5 concurrent requests)
3. **Saga State Caching**: Cache current saga state in-memory during batch (reduce MongoDB queries)
4. **Ingredient Mapping Cache**: LRU cache for frequently used mappings (e.g., "broccoli" cached after first lookup)
5. **MongoDB Indexing**: Indexes on `FingerprintHash`, `(ProviderId, ProviderCode)`, `RecipeUrl`

### Bottlenecks

- **Dynamic Crawling**: Playwright slower than static parsing (~2-3s per page vs. ~200ms)
  - Mitigation: Batch time window (1 hour) allows for slower discovery
- **Rate Limiting**: Token bucket enforces delays (e.g., 10 req/min = 6s/request)
  - Mitigation: This is intentional (stealth); no mitigation needed
- **MongoDB Persistence**: Batch inserts for recipes (~100 inserts per batch)
  - Mitigation: Use `BulkWriteAsync` for batch inserts (10x faster than individual inserts)

---

## Testing Strategy

### Test Pyramid

1. **Contract Tests** (saga state machine):
   - Test all state transitions: `Idle → Discovering → Fingerprinting → Processing → Persisting → Completed`
   - Test compensating transactions: Retry on transient errors, skip on permanent errors
   - Test crash recovery: Save state mid-batch, restart, resume from saved state

2. **Integration Tests** (end-to-end workflow):
   - Use MongoDB Testcontainers for real database
   - Mock HTTP responses with WireMock or HttpClient mock
   - Test full workflow: Discovery → Fingerprinting → Processing → Persistence
   - Verify domain events emitted at each stage

3. **Unit Tests** (business logic):
   - Ingredient normalization (mapping lookups, unmapped handling)
   - Fingerprinting (hash generation, duplicate detection)
   - Rate limiting (token bucket algorithm, burst handling)
   - Configuration validation (invalid provider settings)

### Test Coverage Goals

- **80%+ coverage** for critical paths (saga, normalization, fingerprinting, rate limiting)
- **100% coverage** for state transitions (saga state machine)

---

## Security Considerations

### OWASP Top 10 Compliance

1. **A01: Broken Access Control**
   - N/A (no user-facing access control; batch processing only)

2. **A02: Cryptographic Failures**
   - **Secrets Management**: No hardcoded secrets; use environment variables or Azure Key Vault
   - **Data in Transit**: HTTPS for all HTTP requests (validate SSL certificates)

3. **A03: Injection**
   - **MongoDB Injection**: Use parameterized queries (LINQ, MongoDB Driver filters)
   - **Command Injection**: No OS command execution (Playwright runs in isolated container)

4. **A04: Insecure Design**
   - **Rate Limiting**: Enforced at application level (token bucket)
   - **Crash Recovery**: Saga state persisted; no data loss on restart

5. **A05: Security Misconfiguration**
   - **Error Messages**: Structured logs (no stack traces in production logs)
   - **Default Configuration**: Disabled providers by default (must explicitly enable)

6. **A06: Vulnerable Components**
   - **Dependency Scanning**: Run `dotnet list package --vulnerable` in CI/CD
   - **Regular Updates**: Update NuGet packages quarterly

7. **A07: Authentication Failures**
   - N/A (no user authentication; batch processing only)

8. **A08: Software and Data Integrity Failures**
   - **Configuration Validation**: Validate provider settings at startup (fail fast if invalid)

9. **A09: Logging Failures**
   - **Comprehensive Logging**: Log all state transitions, errors, processing metrics
   - **Sensitive Data**: Do not log recipe content (only URLs, titles, metadata)

10. **A10: Server-Side Request Forgery (SSRF)**
    - **URL Validation**: Validate provider URLs at startup (must be HTTPS, known domains)
    - **No User Input**: Recipe URLs discovered from trusted providers (not user-provided)

---

## Next Steps

Phase 0 (Research) complete. Proceed to **Phase 1: Design**:

1. Generate `data-model.md` with entity schemas (RecipeBatch, IngredientMapping, RecipeFingerprint, etc.)
2. Generate `contracts/` with service interfaces (IDiscoveryService, IIngredientNormalizer, etc.)
3. Generate `quickstart.md` with local development setup instructions
4. Update agent context (copilot instructions) with architecture decisions
