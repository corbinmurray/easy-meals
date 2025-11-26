# Quickstart: Provider Configuration System

**Feature**: 001-provider-config  
**Date**: 2025-11-25

## Overview

This guide shows developers how to use the provider configuration infrastructure in the recipe-engine application. Provider configurations control how recipes are discovered, fetched, and extracted from different sources.

---

## Quick Reference

### Getting Provider Configurations

```csharp
// Inject the repository (caching decorator handles TTL automatically)
public class RecipeDiscoveryService(IProviderConfigurationRepository repository)
{
    public async Task ProcessProvidersAsync(CancellationToken ct)
    {
        // Returns enabled providers, sorted by priority (highest first)
        var providers = await repository.GetAllEnabledAsync(ct);
        
        foreach (var provider in providers)
        {
            Console.WriteLine($"Processing: {provider.DisplayName}");
            // Use provider.DiscoveryStrategy, provider.FetchingStrategy, etc.
        }
    }
}
```

### Configuration Options (appsettings.json)

```json
{
  "ProviderConfigurationCache": {
    "TimeToLive": "00:00:30"
  }
}
```

Override via environment variable:
```bash
export ProviderConfigurationCache__TimeToLive=00:01:00
```

---

## Project Structure

```
packages/easy-meals/
├── EasyMeals.Domain/                         # Domain entities
│   └── ProviderConfiguration/
│       ├── ProviderConfiguration.cs          # Aggregate root
│       ├── DiscoveryStrategy.cs              # Enum
│       ├── FetchingStrategy.cs               # Enum
│       └── ... (value objects)
├── EasyMeals.Persistence.Abstractions/
│   └── Repositories/
│       └── IProviderConfigurationRepository.cs
└── EasyMeals.Persistence.Mongo/
    ├── Documents/ProviderConfiguration/
    │   └── ProviderConfigurationDocument.cs
    └── Repositories/
        └── ProviderConfigurationRepository.cs

apps/recipe-engine/src/
└── EasyMeals.RecipeEngine.Infrastructure/
    ├── Caching/
    │   ├── CachedProviderConfigurationRepository.cs
    │   └── ProviderConfigurationCacheOptions.cs
    └── ServiceCollectionExtensions.cs
```

---

## Adding to DI Container

### Recipe Engine Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddProviderConfigurationServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Register MongoDB repository
    services.AddScoped<ProviderConfigurationRepository>();
    
    // Register caching decorator
    services.AddMemoryCache();
    services.Configure<ProviderConfigurationCacheOptions>(
        configuration.GetSection(ProviderConfigurationCacheOptions.SectionName));
    
    // Register interface with caching decorator wrapping the repository
    services.AddScoped<IProviderConfigurationRepository>(sp =>
    {
        var inner = sp.GetRequiredService<ProviderConfigurationRepository>();
        var cache = sp.GetRequiredService<IMemoryCache>();
        var options = sp.GetRequiredService<IOptions<ProviderConfigurationCacheOptions>>();
        return new CachedProviderConfigurationRepository(inner, cache, options);
    });
    
    return services;
}
```

---

## Working with Strategies

### Determining Discovery Approach

```csharp
public async Task DiscoverRecipesAsync(ProviderConfigurationDocument config, CancellationToken ct)
{
    var discoveryStrategy = Enum.Parse<DiscoveryStrategy>(config.DiscoveryStrategy);
    
    switch (discoveryStrategy)
    {
        case DiscoveryStrategy.Api:
            // Use config.ApiSettings for API-based discovery
            var apiSettings = config.ApiSettings 
                ?? throw new InvalidOperationException("ApiSettings required for Api strategy");
            await DiscoverViaApiAsync(apiSettings, ct);
            break;
            
        case DiscoveryStrategy.Crawl:
            // Use config.CrawlSettings for crawl-based discovery
            var crawlSettings = config.CrawlSettings 
                ?? throw new InvalidOperationException("CrawlSettings required for Crawl strategy");
            await DiscoverViaCrawlAsync(crawlSettings, ct);
            break;
    }
}
```

### Determining Fetching Approach

```csharp
public async Task<string> FetchRecipeHtmlAsync(
    ProviderConfigurationDocument config, 
    string recipeUrl, 
    CancellationToken ct)
{
    var fetchingStrategy = Enum.Parse<FetchingStrategy>(config.FetchingStrategy);
    
    return fetchingStrategy switch
    {
        FetchingStrategy.Api => await FetchViaApiAsync(config.ApiSettings!, recipeUrl, ct),
        FetchingStrategy.StaticHtml => await FetchStaticHtmlAsync(recipeUrl, ct),
        FetchingStrategy.DynamicHtml => await FetchDynamicHtmlAsync(recipeUrl, ct),
        _ => throw new NotSupportedException($"Unknown strategy: {fetchingStrategy}")
    };
}
```

---

## Using Extraction Selectors

```csharp
// Example using a hypothetical HTML parser (e.g., AngleSharp, HtmlAgilityPack)
public Recipe ExtractRecipe(IHtmlDocument document, ExtractionSelectorsDocument selectors)
{
    return new Recipe
    {
        Title = ExtractText(document, selectors.TitleSelector, selectors.TitleFallbackSelector),
        Description = ExtractText(document, selectors.DescriptionSelector),
        Ingredients = ExtractList(document, selectors.IngredientsSelector),
        Instructions = ExtractList(document, selectors.InstructionsSelector),
        PrepTime = ExtractOptional(document, selectors.PrepTimeSelector),
        CookTime = ExtractOptional(document, selectors.CookTimeSelector),
        // ... etc
    };
}

private string ExtractText(IHtmlDocument doc, string selector, string? fallback = null)
{
    var element = doc.QuerySelector(selector);
    if (element is null && fallback is not null)
        element = doc.QuerySelector(fallback);
    return element?.TextContent?.Trim() ?? string.Empty;
}
```

---

## Rate Limiting

```csharp
// Apply rate limits from provider configuration
public class RateLimitedHttpClient
{
    public async Task<HttpResponseMessage> GetAsync(
        RateLimitSettingsDocument rateLimits,
        string url,
        CancellationToken ct)
    {
        // Respect delay between requests
        await Task.Delay(rateLimits.DelayBetweenRequestsMs, ct);
        
        // Use Polly or similar for retry with backoff
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                rateLimits.MaxRetries,
                attempt => TimeSpan.FromMilliseconds(
                    rateLimits.RetryDelayMs * Math.Pow(2, attempt - 1)));
        
        return await retryPolicy.ExecuteAsync(
            async () => await _httpClient.GetAsync(url, ct));
    }
}
```

---

## Managing Provider Configurations

### Adding a New Provider (via MongoDB)

> **SECURITY**: API keys and secrets MUST be stored as **secret references**, never raw values.
> Use format `secret:<key-name>` (e.g., `"secret:hellofresh-apikey"`). The application resolves
> these at runtime from the configured secret store.

```javascript
// Using MongoDB Compass or mongosh
db.provider_configurations.insertOne({
  "providerName": "example-recipes",
  "displayName": "Example Recipes",
  "baseUrl": "https://example-recipes.com",
  "isEnabled": true,
  "priority": 50,
  "discoveryStrategy": "Crawl",
  "fetchingStrategy": "StaticHtml",
  "extractionSelectors": {
    "titleSelector": "h1.recipe-title",
    "descriptionSelector": ".recipe-description",
    "ingredientsSelector": ".ingredients-list li",
    "instructionsSelector": ".instructions-list li"
  },
  "rateLimitSettings": {
    "requestsPerMinute": 30,
    "delayBetweenRequestsMs": 500,
    "maxConcurrentRequests": 2,
    "maxRetries": 3,
    "retryDelayMs": 1000
  },
  "crawlSettings": {
    "seedUrls": ["https://example-recipes.com/recipes"],
    "includePatterns": ["^https://example-recipes\\.com/recipe/.*$"],
    "excludePatterns": [],
    "maxDepth": 2,
    "linkSelector": "a.recipe-link"
  },
  "createdAt": new Date(),
  "updatedAt": new Date(),
  "version": 1,
  "__concurrencyToken": 0,
  "isDeleted": false
})
```

### Disabling a Provider

```javascript
db.provider_configurations.updateOne(
  { "providerName": "example-recipes" },
  { 
    "$set": { 
      "isEnabled": false, 
      "updatedAt": new Date() 
    },
    "$inc": { "__concurrencyToken": 1 }
  }
)
```

### Updating CSS Selectors

```javascript
db.provider_configurations.updateOne(
  { "providerName": "example-recipes" },
  { 
    "$set": { 
      "extractionSelectors.titleSelector": "h1.new-title-class",
      "updatedAt": new Date() 
    },
    "$inc": { "__concurrencyToken": 1 }
  }
)
```

---

## Testing

### Unit Test Example

```csharp
[Fact]
public void ExtractionSelectors_RequiredFields_ThrowsOnMissing()
{
    // Arrange & Act
    var createWithMissingTitle = () => new ExtractionSelectors
    {
        TitleSelector = "", // Invalid - required
        DescriptionSelector = ".desc",
        IngredientsSelector = ".ing",
        InstructionsSelector = ".inst"
    };
    
    // Assert
    createWithMissingTitle.Should().Throw<ValidationException>();
}
```

### Integration Test Example

```csharp
[Fact]
public async Task GetAllEnabledAsync_ReturnsOnlyEnabledProviders()
{
    // Arrange - using Testcontainers for MongoDB
    await _repository.AddAsync(CreateProvider("enabled-1", isEnabled: true));
    await _repository.AddAsync(CreateProvider("disabled-1", isEnabled: false));
    await _repository.AddAsync(CreateProvider("enabled-2", isEnabled: true));
    
    // Act
    var result = await _repository.GetAllEnabledAsync();
    
    // Assert
    result.Should().HaveCount(2);
    result.Should().OnlyContain(p => p.IsEnabled);
}
```

---

## Troubleshooting

### Cache Not Updating

**Symptom**: Configuration changes in MongoDB not reflected in application.

**Solutions**:
1. Wait for cache TTL to expire (default 30 seconds)
2. Use `ClearCacheAsync()` to force immediate refresh (admin/testing only):

```csharp
// For admin operations or testing
public class ProviderAdminService(IProviderConfigurationRepository repository)
{
    public async Task ForceRefreshAfterBulkUpdateAsync(CancellationToken ct)
    {
        // Clear cache to pick up bulk changes immediately
        await repository.ClearCacheAsync(ct);
        
        // Next read will load fresh from database
        var configs = await repository.GetAllEnabledAsync(ct);
    }
}
```

3. Restart the application

### Provider Not Being Processed

**Checklist**:
1. Is `isEnabled` set to `true`?
2. Is `isDeleted` set to `false`?
3. Are required settings present (e.g., `ApiSettings` for `Api` strategy)?
4. Check application logs for validation errors.

### CSS Selectors Not Matching

**Tips**:
1. Test selectors in browser DevTools first
2. Check for dynamic content (may need `DynamicHtml` strategy)
3. Use fallback selectors for resilience
4. Verify site hasn't changed structure
5. **Validate selectors before saving** using `CssSelectorValidator`:

```csharp
// Validate CSS selectors using AngleSharp
public static class CssSelectorValidator
{
    public static bool IsValidSelector(string selector)
    {
        try
        {
            var parser = new CssParser();
            var parsed = parser.ParseSelector(selector);
            return parsed is not null && !string.IsNullOrWhiteSpace(selector);
        }
        catch
        {
            return false;
        }
    }
    
    public static bool TestSelectorAgainstHtml(string selector, string sampleHtml)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(sampleHtml);
        var elements = document.QuerySelectorAll(selector);
        return elements.Any();
    }
}
```

---

## Next Steps

This infrastructure phase provides the configuration system. Future work includes:

1. **Recipe Processing Saga** - Implements discovery, fetching, and extraction using these configurations
2. **API Endpoints** - REST API for CRUD operations on provider configurations
3. **Web UI** - React/Next.js interface for managing configurations
