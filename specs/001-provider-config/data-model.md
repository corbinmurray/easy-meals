# Data Model: Provider Configuration System

**Feature**: 001-provider-config  
**Date**: 2025-11-25

## Overview

This document defines the domain model for provider configurations, including the aggregate root, value objects, enums, and their MongoDB document representations.

---

## Domain Model Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                     ProviderConfiguration                           │
│                      (Aggregate Root)                               │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: string (ObjectId)                                             │
│ + ProviderName: string (unique, immutable)                          │
│ + DisplayName: string                                               │
│ + BaseUrl: string                                                   │
│ + IsEnabled: bool                                                   │
│ + Priority: int                                                     │
│ + DiscoveryStrategy: DiscoveryStrategy                              │
│ + FetchingStrategy: FetchingStrategy                                │
│ + ExtractionSelectors: ExtractionSelectors                          │
│ + RateLimitSettings: RateLimitSettings                              │
│ + ApiSettings: ApiSettings?                                         │
│ + CrawlSettings: CrawlSettings?                                     │
│ + CreatedAt: DateTime (inherited)                                   │
│ + UpdatedAt: DateTime (inherited)                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Enable(): void                                                    │
│ + Disable(): void                                                   │
│ + UpdateSelectors(ExtractionSelectors): void                        │
│ + UpdateRateLimits(RateLimitSettings): void                         │
│ + Validate(): ValidationResult                                      │
└─────────────────────────────────────────────────────────────────────┘
           │
           │ contains
           ▼
┌───────────────────────────┐  ┌───────────────────────────┐
│   ExtractionSelectors     │  │    RateLimitSettings      │
│      (Value Object)       │  │      (Value Object)       │
├───────────────────────────┤  ├───────────────────────────┤
│ + TitleSelector           │  │ + RequestsPerMinute: int  │
│ + TitleFallbackSelector?  │  │ + DelayBetweenRequests    │
│ + DescriptionSelector     │  │ + MaxConcurrentRequests   │
│ + IngredientsSelector     │  │ + MaxRetries: int         │
│ + InstructionsSelector    │  │ + RetryDelay: TimeSpan    │
│ + PrepTimeSelector?       │  └───────────────────────────┘
│ + CookTimeSelector?       │
│ + TotalTimeSelector?      │  ┌───────────────────────────┐
│ + ServingsSelector?       │  │      ApiSettings          │
│ + ImageUrlSelector?       │  │     (Value Object)        │
│ + AuthorSelector?         │  ├───────────────────────────┤
│ + CuisineSelector?        │  │ + Endpoint: string        │
│ + DifficultySelector?     │  │ + AuthMethod: AuthMethod  │
│ + NutritionSelector?      │  │ + Headers: Dictionary     │
└───────────────────────────┘  │ + PageSizeParam?: string  │
                               │ + PageNumberParam?: string│
┌───────────────────────────┐  └───────────────────────────┘
│      CrawlSettings        │
│     (Value Object)        │  ┌───────────────────────────┐
├───────────────────────────┤  │    DiscoveryStrategy      │
│ + SeedUrls: List<string>  │  │        (Enum)             │
│ + IncludePatterns: List   │  ├───────────────────────────┤
│ + ExcludePatterns: List   │  │ Api = 0                   │
│ + MaxDepth: int           │  │ Crawl = 1                 │
│ + LinkSelector: string    │  └───────────────────────────┘
└───────────────────────────┘
                               ┌───────────────────────────┐
                               │    FetchingStrategy       │
                               │        (Enum)             │
                               ├───────────────────────────┤
                               │ Api = 0                   │
                               │ StaticHtml = 1            │
                               │ DynamicHtml = 2           │
                               └───────────────────────────┘
```

---

## Domain Entities

### ProviderConfiguration (Aggregate Root)

The root entity representing a complete recipe provider configuration.

```csharp
namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Aggregate root representing a recipe provider's configuration settings.
/// Contains all settings needed for recipe discovery, fetching, and extraction.
/// </summary>
public class ProviderConfiguration : AggregateRoot<string>
{
    /// <summary>
    /// Unique identifier for the provider (e.g., "hellofresh", "allrecipes").
    /// Immutable after creation; used as a business key.
    /// </summary>
    public string ProviderName { get; private set; }

    /// <summary>
    /// Human-friendly display name (e.g., "HelloFresh", "AllRecipes").
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Base URL for the provider's website (e.g., "https://www.hellofresh.com").
    /// </summary>
    public string BaseUrl { get; private set; }

    /// <summary>
    /// Whether this provider is currently active for recipe discovery.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Processing priority (higher = processed first when multiple providers match).
    /// </summary>
    public int Priority { get; private set; }

    /// <summary>
    /// How recipe URLs are discovered (API or Crawl).
    /// </summary>
    public DiscoveryStrategy DiscoveryStrategy { get; private set; }

    /// <summary>
    /// How recipe content is fetched (API, StaticHtml, or DynamicHtml).
    /// </summary>
    public FetchingStrategy FetchingStrategy { get; private set; }

    /// <summary>
    /// CSS selectors for extracting recipe properties from HTML.
    /// </summary>
    public ExtractionSelectors ExtractionSelectors { get; private set; }

    /// <summary>
    /// Rate limiting configuration for this provider.
    /// </summary>
    public RateLimitSettings RateLimitSettings { get; private set; }

    /// <summary>
    /// API-specific settings (required when DiscoveryStrategy or FetchingStrategy is Api).
    /// </summary>
    public ApiSettings? ApiSettings { get; private set; }

    /// <summary>
    /// Crawl-specific settings (required when DiscoveryStrategy is Crawl).
    /// </summary>
    public CrawlSettings? CrawlSettings { get; private set; }
}
```

### Invariants & Validation Rules

| Rule | Description |
|------|-------------|
| `ProviderName` required | Cannot be null, empty, or whitespace |
| `ProviderName` format | Lowercase alphanumeric with hyphens only |
| `BaseUrl` required | Must be valid absolute URL |
| `ApiSettings` required | When `DiscoveryStrategy == Api` or `FetchingStrategy == Api` |
| `CrawlSettings` required | When `DiscoveryStrategy == Crawl` |
| `ExtractionSelectors` required | Always required; core selectors must be non-empty |
| `Priority` range | Must be >= 0 |

---

## Value Objects

### ExtractionSelectors

CSS selectors for extracting recipe data from HTML pages.

> **VALIDATION**: All selector strings are validated via `CssSelectorValidator` using AngleSharp.
> Invalid selectors will cause validation to fail on save. Run selectors against sample HTML
> to verify they extract expected content before persisting configurations.

```csharp
/// <summary>
/// CSS selectors for extracting recipe properties from HTML.
/// Primary selectors are required; fallback selectors provide resilience.
/// </summary>
public sealed record ExtractionSelectors
{
    // Required selectors (core recipe data)
    public required string TitleSelector { get; init; }
    public required string DescriptionSelector { get; init; }
    public required string IngredientsSelector { get; init; }
    public required string InstructionsSelector { get; init; }

    // Fallback selectors for resilience
    public string? TitleFallbackSelector { get; init; }
    public string? DescriptionFallbackSelector { get; init; }

    // Optional selectors (may not exist on all recipe pages)
    public string? PrepTimeSelector { get; init; }
    public string? CookTimeSelector { get; init; }
    public string? TotalTimeSelector { get; init; }
    public string? ServingsSelector { get; init; }
    public string? ImageUrlSelector { get; init; }
    public string? AuthorSelector { get; init; }
    public string? CuisineSelector { get; init; }
    public string? DifficultySelector { get; init; }
    public string? NutritionSelector { get; init; }
}
```

### RateLimitSettings

Rate limiting and retry configuration.

```csharp
/// <summary>
/// Rate limiting configuration for provider requests.
/// </summary>
public sealed record RateLimitSettings
{
    /// <summary>Maximum requests per minute to this provider.</summary>
    public int RequestsPerMinute { get; init; } = 60;

    /// <summary>Minimum delay between consecutive requests.</summary>
    public TimeSpan DelayBetweenRequests { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Maximum concurrent requests to this provider.</summary>
    public int MaxConcurrentRequests { get; init; } = 5;

    /// <summary>Maximum retry attempts on transient failures.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Base delay between retry attempts (may be multiplied for backoff).</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}
```

### ApiSettings

Configuration for API-based discovery and fetching.

> **SECURITY NOTE**: API keys and secrets MUST NOT be stored directly in the `Headers` dictionary.
> Store a **secret reference** (e.g., `"secret:hellofresh-apikey"`) and resolve at runtime
> from a secure secret store (Azure Key Vault, AWS Secrets Manager, or environment variables for development).

```csharp
/// <summary>
/// Settings for API-based recipe discovery and fetching.
/// </summary>
public sealed record ApiSettings
{
    /// <summary>API endpoint URL for recipe data.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Authentication method (None, ApiKey, Bearer, Basic).</summary>
    public AuthMethod AuthMethod { get; init; } = AuthMethod.None;

    /// <summary>
    /// Custom headers to include with API requests.
    /// For sensitive values (API keys, tokens), store secret references only.
    /// Example: "X-Api-Key": "secret:provider-apikey"
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>Query parameter name for page size (pagination).</summary>
    public string? PageSizeParam { get; init; }

    /// <summary>Query parameter name for page number (pagination).</summary>
    public string? PageNumberParam { get; init; }

    /// <summary>Default page size for paginated requests.</summary>
    public int DefaultPageSize { get; init; } = 20;
}

/// <summary>
/// Authentication methods for API access.
/// </summary>
public enum AuthMethod
{
    None = 0,
    ApiKey = 1,
    Bearer = 2,
    Basic = 3
}
```

### CrawlSettings

Configuration for crawl-based discovery.

```csharp
/// <summary>
/// Settings for HTML crawl-based recipe discovery.
/// </summary>
public sealed record CrawlSettings
{
    /// <summary>Initial URLs to start crawling from.</summary>
    public required IReadOnlyList<string> SeedUrls { get; init; }

    /// <summary>URL patterns to include (regex). Only matching URLs are processed.</summary>
    public IReadOnlyList<string> IncludePatterns { get; init; } = [];

    /// <summary>URL patterns to exclude (regex). Matching URLs are skipped.</summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = [];

    /// <summary>Maximum crawl depth from seed URLs.</summary>
    public int MaxDepth { get; init; } = 3;

    /// <summary>CSS selector for finding links to follow.</summary>
    public string LinkSelector { get; init; } = "a[href]";
}
```

---

## Enumerations

### DiscoveryStrategy

```csharp
/// <summary>
/// How recipe URLs are discovered from a provider.
/// </summary>
public enum DiscoveryStrategy
{
    /// <summary>Discover recipes via the provider's API.</summary>
    Api = 0,

    /// <summary>Discover recipes by crawling HTML pages.</summary>
    Crawl = 1
}
```

### FetchingStrategy

```csharp
/// <summary>
/// How recipe content is fetched from a provider.
/// </summary>
public enum FetchingStrategy
{
    /// <summary>Fetch structured data from the provider's API.</summary>
    Api = 0,

    /// <summary>Fetch HTML via simple HTTP GET (no JavaScript rendering).</summary>
    StaticHtml = 1,

    /// <summary>Fetch HTML using browser automation (JavaScript rendering required).</summary>
    DynamicHtml = 2
}
```

---

## MongoDB Document Schema

> **VERSIONING**: All documents include a `version` field (inherited from `BaseSoftDeletableDocument`).
> - Initial version: `1`
> - On schema changes: increment version and add migration logic in `ProviderConfigurationMigrator`
> - Migration runs at startup to upgrade older documents to current schema
> - See spec.md "Schema Versioning & Migration" section for patterns

### ProviderConfigurationDocument

```csharp
namespace EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;

/// <summary>
/// MongoDB document for provider configuration data.
/// </summary>
[BsonCollection("provider_configurations")]
public class ProviderConfigurationDocument : BaseSoftDeletableDocument
{
    [BsonElement("providerName")]
    [BsonRequired]
    public string ProviderName { get; set; } = string.Empty;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("baseUrl")]
    [BsonRequired]
    public string BaseUrl { get; set; } = string.Empty;

    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [BsonElement("priority")]
    public int Priority { get; set; }

    [BsonElement("discoveryStrategy")]
    [BsonRepresentation(BsonType.String)]
    public string DiscoveryStrategy { get; set; } = string.Empty;

    [BsonElement("fetchingStrategy")]
    [BsonRepresentation(BsonType.String)]
    public string FetchingStrategy { get; set; } = string.Empty;

    [BsonElement("extractionSelectors")]
    public ExtractionSelectorsDocument ExtractionSelectors { get; set; } = new();

    [BsonElement("rateLimitSettings")]
    public RateLimitSettingsDocument RateLimitSettings { get; set; } = new();

    [BsonElement("apiSettings")]
    [BsonIgnoreIfNull]
    public ApiSettingsDocument? ApiSettings { get; set; }

    [BsonElement("crawlSettings")]
    [BsonIgnoreIfNull]
    public CrawlSettingsDocument? CrawlSettings { get; set; }
}
```

### Embedded Documents

```csharp
public class ExtractionSelectorsDocument
{
    [BsonElement("titleSelector")]
    public string TitleSelector { get; set; } = string.Empty;

    [BsonElement("titleFallbackSelector")]
    [BsonIgnoreIfNull]
    public string? TitleFallbackSelector { get; set; }

    [BsonElement("descriptionSelector")]
    public string DescriptionSelector { get; set; } = string.Empty;

    [BsonElement("descriptionFallbackSelector")]
    [BsonIgnoreIfNull]
    public string? DescriptionFallbackSelector { get; set; }

    [BsonElement("ingredientsSelector")]
    public string IngredientsSelector { get; set; } = string.Empty;

    [BsonElement("instructionsSelector")]
    public string InstructionsSelector { get; set; } = string.Empty;

    [BsonElement("prepTimeSelector")]
    [BsonIgnoreIfNull]
    public string? PrepTimeSelector { get; set; }

    [BsonElement("cookTimeSelector")]
    [BsonIgnoreIfNull]
    public string? CookTimeSelector { get; set; }

    [BsonElement("totalTimeSelector")]
    [BsonIgnoreIfNull]
    public string? TotalTimeSelector { get; set; }

    [BsonElement("servingsSelector")]
    [BsonIgnoreIfNull]
    public string? ServingsSelector { get; set; }

    [BsonElement("imageUrlSelector")]
    [BsonIgnoreIfNull]
    public string? ImageUrlSelector { get; set; }

    [BsonElement("authorSelector")]
    [BsonIgnoreIfNull]
    public string? AuthorSelector { get; set; }

    [BsonElement("cuisineSelector")]
    [BsonIgnoreIfNull]
    public string? CuisineSelector { get; set; }

    [BsonElement("difficultySelector")]
    [BsonIgnoreIfNull]
    public string? DifficultySelector { get; set; }

    [BsonElement("nutritionSelector")]
    [BsonIgnoreIfNull]
    public string? NutritionSelector { get; set; }
}

public class RateLimitSettingsDocument
{
    [BsonElement("requestsPerMinute")]
    public int RequestsPerMinute { get; set; } = 60;

    [BsonElement("delayBetweenRequestsMs")]
    public int DelayBetweenRequestsMs { get; set; } = 100;

    [BsonElement("maxConcurrentRequests")]
    public int MaxConcurrentRequests { get; set; } = 5;

    [BsonElement("maxRetries")]
    public int MaxRetries { get; set; } = 3;

    [BsonElement("retryDelayMs")]
    public int RetryDelayMs { get; set; } = 1000;
}

public class ApiSettingsDocument
{
    [BsonElement("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [BsonElement("authMethod")]
    [BsonRepresentation(BsonType.String)]
    public string AuthMethod { get; set; } = "None";

    [BsonElement("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [BsonElement("pageSizeParam")]
    [BsonIgnoreIfNull]
    public string? PageSizeParam { get; set; }

    [BsonElement("pageNumberParam")]
    [BsonIgnoreIfNull]
    public string? PageNumberParam { get; set; }

    [BsonElement("defaultPageSize")]
    public int DefaultPageSize { get; set; } = 20;
}

public class CrawlSettingsDocument
{
    [BsonElement("seedUrls")]
    public List<string> SeedUrls { get; set; } = [];

    [BsonElement("includePatterns")]
    public List<string> IncludePatterns { get; set; } = [];

    [BsonElement("excludePatterns")]
    public List<string> ExcludePatterns { get; set; } = [];

    [BsonElement("maxDepth")]
    public int MaxDepth { get; set; } = 3;

    [BsonElement("linkSelector")]
    public string LinkSelector { get; set; } = "a[href]";
}
```

---

## MongoDB Indexes

```javascript
// Unique index on provider name (business key)
db.provider_configurations.createIndex(
  { "providerName": 1 },
  { unique: true, name: "idx_provider_name_unique" }
)

// Compound index for enabled provider queries (common read path)
db.provider_configurations.createIndex(
  { "isEnabled": 1, "isDeleted": 1, "priority": -1 },
  { name: "idx_enabled_priority" }
)

// Index for soft delete filtering
db.provider_configurations.createIndex(
  { "isDeleted": 1 },
  { name: "idx_is_deleted" }
)
```

---

## Example Document (MongoDB)

```json
{
  "_id": "683362d1a4b5c6d7e8f90123",
  "providerName": "hellofresh",
  "displayName": "HelloFresh",
  "baseUrl": "https://www.hellofresh.com",
  "isEnabled": true,
  "priority": 100,
  "discoveryStrategy": "Crawl",
  "fetchingStrategy": "StaticHtml",
  "extractionSelectors": {
    "titleSelector": "h1[data-test-id='recipe-title']",
    "descriptionSelector": ".recipe-description p",
    "ingredientsSelector": ".recipe-ingredients li",
    "instructionsSelector": ".recipe-steps li",
    "prepTimeSelector": "[data-test-id='prep-time']",
    "cookTimeSelector": "[data-test-id='cook-time']",
    "servingsSelector": "[data-test-id='servings']",
    "imageUrlSelector": ".recipe-hero-image img"
  },
  "rateLimitSettings": {
    "requestsPerMinute": 30,
    "delayBetweenRequestsMs": 500,
    "maxConcurrentRequests": 2,
    "maxRetries": 3,
    "retryDelayMs": 2000
  },
  "crawlSettings": {
    "seedUrls": [
      "https://www.hellofresh.com/recipes/quick-recipes",
      "https://www.hellofresh.com/recipes/family-friendly-recipes"
    ],
    "includePatterns": [
      "^https://www\\.hellofresh\\.com/recipes/[a-z0-9-]+-[a-f0-9]+$"
    ],
    "excludePatterns": [
      "/recipes/search",
      "/recipes/collections"
    ],
    "maxDepth": 2,
    "linkSelector": "a.recipe-card-link"
  },
  "apiSettings": null,
  "createdAt": "2025-11-25T10:00:00.000Z",
  "updatedAt": "2025-11-25T10:00:00.000Z",
  "version": 1,
  "__concurrencyToken": 0,
  "isDeleted": false,
  "deletedAt": null
}
```

---

## Domain-to-Document Mapping

Mapping will be handled by explicit mapper classes in the Infrastructure layer (not AutoMapper to avoid magic):

```csharp
public static class ProviderConfigurationMapper
{
    public static ProviderConfiguration ToDomain(ProviderConfigurationDocument doc) => ...;
    public static ProviderConfigurationDocument ToDocument(ProviderConfiguration entity) => ...;
}
```

This ensures:
- Domain entities remain persistence-ignorant
- Mapping logic is explicit and testable
- No dependency on mapping libraries in domain layer
