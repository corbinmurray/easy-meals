# Research: Provider Configuration System

**Feature**: 001-provider-config  
**Date**: 2025-11-25

## Overview

This document captures research findings for implementing the provider configuration infrastructure. All technical decisions have been resolved from the feature spec clarifications.

---

## 1. Caching Strategy: IMemoryCache with TTL

### Decision
Use `Microsoft.Extensions.Caching.Memory.IMemoryCache` with time-based expiration (TTL).

### Rationale
- **Already available**: Part of .NET runtime, no additional package needed (version 10.0.0 aligns with project)
- **Simple API**: `GetOrCreateAsync` pattern provides cache-aside with minimal boilerplate
- **TTL support**: `MemoryCacheEntryOptions.AbsoluteExpirationRelativeToNow` provides exact behavior needed
- **Thread-safe**: Built-in thread safety for concurrent access
- **Configurable**: TTL can be bound to `IOptions<T>` pattern for appsettings.json + env var override

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| Distributed cache (Redis) | Over-engineering for single-instance recipe-engine; adds operational complexity |
| LazyCache | Additional dependency; IMemoryCache sufficient for simple TTL use case |
| Custom cache implementation | Reinventing the wheel; IMemoryCache is battle-tested |

### Implementation Pattern

```csharp
public class CachedProviderConfigurationRepository : IProviderConfigurationRepository
{
    private readonly IProviderConfigurationRepository _inner;
    private readonly IMemoryCache _cache;
    private readonly IOptions<ProviderConfigurationCacheOptions> _options;
    private const string CacheKey = "provider-configurations";

    public async Task<IReadOnlyList<ProviderConfiguration>> GetAllEnabledAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _options.Value.TimeToLive;
            return await _inner.GetAllEnabledAsync(ct);
        }) ?? [];
    }
}
```

### Configuration Pattern

```csharp
public class ProviderConfigurationCacheOptions
{
    public const string SectionName = "ProviderConfigurationCache";
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromSeconds(30);
}
```

```json
// appsettings.json
{
  "ProviderConfigurationCache": {
    "TimeToLive": "00:00:30"
  }
}
```

Environment variable override: `ProviderConfigurationCache__TimeToLive=00:01:00`

---

## 2. Domain Entity Design: Aggregate Root Pattern

### Decision
`ProviderConfiguration` as aggregate root inheriting from `AggregateRoot<string>`, with value objects for nested settings.

### Rationale
- **Consistency**: Follows existing `AggregateRoot<TKey>` pattern from `EasyMeals.Platform`
- **String ID**: MongoDB ObjectId as string aligns with existing `BaseDocument` pattern
- **Immutable value objects**: Extraction selectors, rate limit settings are naturally value objects (equality by value, no identity)
- **Rich domain model**: Business rules encapsulated in aggregate (validation, strategy-specific settings access)

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| Flat entity with all properties | Deep nesting (selectors, settings) would create a god class; loses semantic meaning |
| Separate aggregates per strategy | Over-decomposition; provider config is natural consistency boundary |
| Record types only | Records lack mutable audit fields needed for timestamps |

### Entity Hierarchy

```
ProviderConfiguration (Aggregate Root)
├── Id: string (ObjectId)
├── ProviderName: string (unique, immutable after creation)
├── DisplayName: string
├── BaseUrl: string
├── IsEnabled: bool
├── Priority: int
├── DiscoveryStrategy: DiscoveryStrategy (enum)
├── FetchingStrategy: FetchingStrategy (enum)
├── ExtractionSelectors: ExtractionSelectors (value object)
├── RateLimitSettings: RateLimitSettings (value object)
├── ApiSettings: ApiSettings? (value object, nullable)
├── CrawlSettings: CrawlSettings? (value object, nullable)
├── CreatedAt: DateTime (inherited)
└── UpdatedAt: DateTime (inherited)
```

---

## 3. MongoDB Document Pattern

### Decision
`ProviderConfigurationDocument` extending `BaseSoftDeletableDocument` with `[BsonCollection("provider_configurations")]` attribute.

### Rationale
- **Soft delete support**: Allows disabling providers without losing configuration history
- **Audit fields**: Inherits `CreatedAt`, `UpdatedAt`, `Version`, `ConcurrencyToken` from base
- **Collection naming**: Snake_case for MongoDB collection names (existing convention)
- **Embedded documents**: Value objects map to embedded BSON documents (no joins needed)

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| Separate collections for settings | Unnecessary complexity; embedded documents are idiomatic MongoDB |
| Hard delete only | Operators may want to restore disabled providers |
| GUID IDs | Project standardized on ObjectId strings |

### Document Schema

```csharp
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
    public string DiscoveryStrategy { get; set; } = string.Empty;

    [BsonElement("fetchingStrategy")]
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

### MongoDB Indexes

```csharp
// Unique index on provider name (cannot have duplicates)
{ "providerName": 1 }, { unique: true }

// Compound index for enabled provider queries (common read path)
{ "isEnabled": 1, "priority": -1 }
```

---

## 4. Repository Interface Design

### Decision
`IProviderConfigurationRepository` extending `IRepository<ProviderConfigurationDocument, string>` with domain-specific query methods.

### Rationale
- **Reuse base CRUD**: Inherits `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` from `IRepository`
- **Domain-specific queries**: `GetAllEnabledAsync`, `GetByProviderNameAsync` for common access patterns
- **Future-proofing**: CRUD operations available for future API/UI even though this phase only needs reads

### Interface Definition

```csharp
public interface IProviderConfigurationRepository : IRepository<ProviderConfigurationDocument, string>
{
    /// <summary>
    /// Retrieves all enabled provider configurations, ordered by priority descending.
    /// </summary>
    Task<IReadOnlyList<ProviderConfigurationDocument>> GetAllEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a provider configuration by its unique provider name.
    /// </summary>
    Task<ProviderConfigurationDocument?> GetByProviderNameAsync(string providerName, CancellationToken ct = default);
}
```

---

## 5. Package Structure: EasyMeals.Domain

### Decision
Create new `EasyMeals.Domain` package in `packages/easy-meals/` for shared domain entities.

### Rationale
- **Cross-app reuse**: Domain entities shared between recipe-engine (now) and API (future)
- **Prepares for migration**: Recipe domain entity can move here later, consolidating domain logic
- **Separation of concerns**: Domain layer has no persistence dependencies
- **Solution integration**: Added to `EasyMeals.Packages.sln`

### Package Dependencies

```xml
<!-- EasyMeals.Domain.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\EasyMeals.Platform\EasyMeals.Platform.csproj" />
  </ItemGroup>
</Project>
```

---

## 6. Value Object Implementation

### Decision
Implement value objects as C# records with equality by value.

### Rationale
- **Immutability**: Records are immutable by default (no accidental mutation)
- **Equality semantics**: Built-in structural equality (no manual `Equals`/`GetHashCode`)
- **With-expressions**: Easy creation of modified copies
- **Clean syntax**: Concise positional parameters for simple objects

### Example

```csharp
public sealed record ExtractionSelectors
{
    public required string TitleSelector { get; init; }
    public string? TitleFallbackSelector { get; init; }
    
    public required string DescriptionSelector { get; init; }
    public string? DescriptionFallbackSelector { get; init; }
    
    public required string IngredientsSelector { get; init; }
    public required string InstructionsSelector { get; init; }
    
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

public sealed record RateLimitSettings
{
    public int RequestsPerMinute { get; init; } = 60;
    public TimeSpan DelayBetweenRequests { get; init; } = TimeSpan.FromMilliseconds(100);
    public int MaxConcurrentRequests { get; init; } = 5;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}
```

---

## 7. Testing Strategy

### Unit Tests (EasyMeals.Domain.Tests)
- `ProviderConfiguration_Create_ValidatesRequiredFields`
- `ProviderConfiguration_Enable_SetsIsEnabledTrue`
- `ProviderConfiguration_Disable_SetsIsEnabledFalse`
- `ExtractionSelectors_Equality_ByValue`
- `RateLimitSettings_DefaultValues_Applied`

### Integration Tests (EasyMeals.RecipeEngine.Infrastructure.Tests)
- `ProviderConfigurationRepository_Add_PersistsToMongo` (Testcontainers)
- `ProviderConfigurationRepository_GetAllEnabled_ReturnsOnlyEnabled`
- `ProviderConfigurationRepository_GetByProviderName_FindsByUniqueName`
- `CachedRepository_GetAllEnabled_ReturnsCachedWithinTTL`
- `CachedRepository_GetAllEnabled_RefreshesAfterTTL`

### Test Naming Convention
Per dotnet-architecture-good-practices: `MethodName_Condition_ExpectedResult()`

---

## 8. CSS Selector Validation: AngleSharp

### Decision
Use AngleSharp for CSS selector validation and HTML parsing.

### Rationale
- **Already planned for extraction**: The recipe-engine will use AngleSharp for HTML parsing in the saga
- **Selector validation built-in**: `CssParser.ParseSelector()` validates CSS selector syntax
- **DOM API**: Familiar `QuerySelector`/`QuerySelectorAll` API for testing selectors against sample HTML
- **Active maintenance**: Well-maintained library with regular updates
- **No browser dependency**: Pure .NET implementation, no Playwright/Selenium needed for validation

### Alternatives Considered
| Alternative | Rejected Because |
|-------------|------------------|
| HtmlAgilityPack | XPath-focused; CSS selector support via extensions less robust |
| Regex validation | Cannot validate CSS selector grammar; would miss syntax errors |
| Browser-based (Playwright) | Over-engineering for validation; reserved for DynamicHtml fetching |

### Implementation Pattern

```csharp
public static class CssSelectorValidator
{
    private static readonly CssParser _parser = new();

    /// <summary>
    /// Validates that a CSS selector string is syntactically correct.
    /// </summary>
    public static bool IsValidSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return false;

        try
        {
            var parsed = _parser.ParseSelector(selector);
            return parsed is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tests a CSS selector against sample HTML to verify it matches elements.
    /// </summary>
    public static bool TestSelectorAgainstHtml(string selector, string sampleHtml)
    {
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(sampleHtml);
        var elements = document.QuerySelectorAll(selector);
        return elements.Length > 0;
    }
}
```

### Package Reference

```xml
<PackageReference Include="AngleSharp" Version="1.2.0" />
```

---

## 9. Secrets Management: Reference Pattern

### Decision
Store secret references (not raw secrets) in provider configurations; resolve at runtime.

### Rationale
- **Security**: Raw API keys in MongoDB documents would be exposed in backups, logs, monitoring
- **Rotation**: Changing secrets doesn't require config document updates
- **Compliance**: Follows OWASP guidelines for secret management
- **Flexibility**: Same pattern works for Azure Key Vault, AWS Secrets Manager, or env vars

### Reference Format
```
secret:<provider>-<credential-type>
```

Examples:
- `secret:hellofresh-apikey`
- `secret:allrecipes-bearer-token`

### Resolution Pattern

```csharp
public interface ISecretResolver
{
    Task<string?> ResolveAsync(string secretReference, CancellationToken ct = default);
}

public class EnvironmentSecretResolver : ISecretResolver
{
    public Task<string?> ResolveAsync(string secretReference, CancellationToken ct = default)
    {
        if (!secretReference.StartsWith("secret:"))
            return Task.FromResult<string?>(secretReference); // Not a reference, return as-is
            
        var key = secretReference["secret:".Length..].Replace("-", "_").ToUpperInvariant();
        return Task.FromResult(Environment.GetEnvironmentVariable(key));
    }
}
```

For production, implement `AzureKeyVaultSecretResolver` or `AwsSecretsManagerResolver`.

---

## Summary of Decisions

| Topic | Decision | Key Rationale |
|-------|----------|---------------|
| Caching | IMemoryCache with TTL | Built-in, simple, thread-safe |
| Cache TTL | 30s default, configurable | Balances freshness vs DB load |
| Domain design | Aggregate root + value objects | Follows existing Platform patterns |
| Storage | MongoDB with embedded documents | Matches RecipeDocument pattern |
| Package location | New EasyMeals.Domain | Cross-app reuse, future Recipe migration |
| Value objects | C# records | Immutability, equality by value |
| Repository | Extends IRepository + domain queries | CRUD for future, queries for now |
| CSS validation | AngleSharp | Already planned for extraction; validates selector syntax |
| Secrets | Reference pattern + runtime resolution | Security, rotation support, compliance |

All NEEDS CLARIFICATION items from Technical Context have been resolved.
