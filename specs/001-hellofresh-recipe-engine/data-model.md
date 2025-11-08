# Data Model: Recipe Engine

**Feature**: Multi-Provider Recipe Engine with Database-Driven Configuration  
**Branch**: `001-hellofresh-recipe-engine`  
**Date**: November 2, 2025

## Overview

This document defines the data model for the Recipe Engine, including Domain entities, Value Objects, MongoDB document schemas, and database indexes. The model follows Domain-Driven Design principles with MongoDB as the persistence layer via EasyMeals.Shared.Data. Provider-specific configurations (URLs, rate limits) are stored in MongoDB for security and dynamic management.

---

## Domain Layer Entities

### 1. RecipeBatch

**Purpose**: Represents a collection of recipes processed within a time window for a specific provider.

**Properties**:

```csharp
public class RecipeBatch
{
    public Guid Id { get; private set; }
    public string ProviderId { get; private set; }      // e.g., "provider_001"
    public int BatchSize { get; private set; }          // Configured max recipes
    public TimeSpan TimeWindow { get; private set; }    // Configured max duration
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int ProcessedCount { get; private set; }
    public int SkippedCount { get; private set; }       // Duplicates or invalid
    public int FailedCount { get; private set; }        // Permanent failures
    public BatchStatus Status { get; private set; }     // Pending, InProgress, Completed, Failed

    private readonly List<string> _processedUrls;
    public IReadOnlyList<string> ProcessedUrls => _processedUrls.AsReadOnly();

    private readonly List<string> _failedUrls;
    public IReadOnlyList<string> FailedUrls => _failedUrls.AsReadOnly();
}

public enum BatchStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}
```

**Behavior** (aggregate root methods):

- `RecipeBatch CreateBatch(string providerId, int batchSize, TimeSpan timeWindow)` - Factory method
- `void MarkRecipeProcessed(string url)` - Increment processed count
- `void MarkRecipeSkipped(string url)` - Increment skipped count
- `void MarkRecipeFailed(string url)` - Increment failed count, add to failed URLs
- `void CompleteBatch()` - Set CompletedAt, Status = Completed
- `bool ShouldStopProcessing(DateTime now)` - Check if batch size or time window reached

**Domain Events**:

- `BatchStartedEvent(Guid batchId, string providerId, DateTime startedAt)`
- `BatchCompletedEvent(Guid batchId, int processedCount, int skippedCount, int failedCount, DateTime completedAt)`

---

### 2. Recipe (existing entity - extend if needed)

**Purpose**: Represents a recipe from a provider.

**Existing Properties** (from EasyMeals.RecipeEngine.Domain/Entities/Recipe.cs):

```csharp
public class Recipe
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string Url { get; private set; }
    // ... other properties
}
```

**New Properties to Add**:

```csharp
public string ProviderId { get; private set; }           // e.g., "provider_001"
public string ProviderRecipeId { get; private set; }     // Provider's internal ID (if available)
public string FingerprintHash { get; private set; }      // SHA256 hash for duplicate detection
public DateTime ScrapedAt { get; private set; }          // When scraped
public DateTime? LastUpdatedAt { get; private set; }     // If reprocessed

private readonly List<IngredientReference> _ingredients;
public IReadOnlyList<IngredientReference> Ingredients => _ingredients.AsReadOnly();
```

**IngredientReference** (value object):

```csharp
public class IngredientReference
{
    public string ProviderCode { get; }           // e.g., "BROCCOLI-FROZEN-012"
    public string? CanonicalForm { get; }         // e.g., "broccoli, frozen" (null if unmapped)
    public string Quantity { get; }               // e.g., "2 cups"
    public int DisplayOrder { get; }              // Position in recipe

    public IngredientReference(string providerCode, string? canonicalForm, string quantity, int displayOrder)
    {
        ProviderCode = providerCode ?? throw new ArgumentNullException(nameof(providerCode));
        CanonicalForm = canonicalForm;
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
        DisplayOrder = displayOrder;
    }
}
```

**Domain Events**:

- `RecipeProcessedEvent(Guid recipeId, string url, string providerId, DateTime processedAt)`
- `IngredientMappingMissingEvent(string providerId, string providerCode, string recipeUrl)`

---

### 3. IngredientMapping

**Purpose**: Maps provider-specific ingredient codes to canonical forms.

**Properties**:

```csharp
public class IngredientMapping
{
    public Guid Id { get; private set; }
    public string ProviderId { get; private set; }        // e.g., "provider_001"
    public string ProviderCode { get; private set; }      // e.g., "BROCCOLI-FROZEN-012"
    public string CanonicalForm { get; private set; }     // e.g., "broccoli, frozen"
    public string? Notes { get; private set; }            // Optional metadata
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static IngredientMapping Create(string providerId, string providerCode, string canonicalForm)
    {
        // Validation: ProviderId, ProviderCode, CanonicalForm required
        // ...
    }

    public void UpdateCanonicalForm(string newCanonicalForm)
    {
        CanonicalForm = newCanonicalForm;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**Domain Events**:

- N/A (static data, rarely changes)

---

### 4. RecipeFingerprint

**Purpose**: Tracks processed recipes for duplicate detection.

**Properties**:

```csharp
public class RecipeFingerprint
{
    public Guid Id { get; private set; }
    public string FingerprintHash { get; private set; }   // SHA256 hex string
    public string ProviderId { get; private set; }        // e.g., "provider_001"
    public string RecipeUrl { get; private set; }         // Original URL
    public Guid RecipeId { get; private set; }            // Reference to Recipe entity
    public DateTime ProcessedAt { get; private set; }

    public static RecipeFingerprint Create(string fingerprintHash, string providerId, string recipeUrl, Guid recipeId)
    {
        // Validation: FingerprintHash, ProviderId, RecipeUrl required
        // ...
    }
}
```

**Domain Events**:

- N/A (technical entity for duplicate detection)

---

## Domain Layer Value Objects

### 1. ProviderConfiguration

**Purpose**: Encapsulates provider-specific settings loaded from MongoDB (immutable once loaded).

**Note**: Configuration is stored in MongoDB (`provider_configurations` collection) for security and dynamic management. This value object is constructed from the MongoDB document at runtime.

**Properties**:

```csharp
public class ProviderConfiguration
{
    public string ProviderId { get; }                     // e.g., "provider_001"
    public bool Enabled { get; }                          // If false, skip provider
    public DiscoveryStrategy DiscoveryStrategy { get; }   // Static, Dynamic, Api
    public string RecipeRootUrl { get; }                  // Starting URL for discovery (from DB)
    public int BatchSize { get; }                         // Max recipes per batch
    public TimeSpan TimeWindow { get; }                   // Max duration per batch
    public TimeSpan MinDelay { get; }                     // Min delay between requests
    public int MaxRequestsPerMinute { get; }              // Rate limit
    public int RetryCount { get; }                        // Transient error retries
    public TimeSpan RequestTimeout { get; }               // Per-request timeout

    // Validation in constructor
    public ProviderConfiguration(string providerId, bool enabled, DiscoveryStrategy discoveryStrategy, ...)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("ProviderId required", nameof(providerId));
        if (batchSize <= 0)
            throw new ArgumentException("BatchSize must be positive", nameof(batchSize));
        // ... other validations

        ProviderId = providerId;
        Enabled = enabled;
        // ... assign other properties
    }
}

public enum DiscoveryStrategy
{
    Static,       // Static HTML parsing
    Dynamic,      // JavaScript rendering (Playwright)
    Api           // API-based discovery
}
```

---

### 2. RateLimitToken

**Purpose**: Tracks available request quota for rate limiting.

**Properties**:

```csharp
public class RateLimitToken
{
    public int AvailableTokens { get; private set; }
    public int MaxTokens { get; }
    public TimeSpan RefillRate { get; }
    public DateTime LastRefillAt { get; private set; }

    public RateLimitToken(int maxTokens, TimeSpan refillRate)
    {
        MaxTokens = maxTokens;
        RefillRate = refillRate;
        AvailableTokens = maxTokens;
        LastRefillAt = DateTime.UtcNow;
    }

    public void ConsumeToken()
    {
        if (AvailableTokens <= 0)
            throw new InvalidOperationException("No tokens available");
        AvailableTokens--;
    }

    public void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - LastRefillAt;
        var tokensToAdd = (int)(elapsed / RefillRate);

        if (tokensToAdd > 0)
        {
            AvailableTokens = Math.Min(AvailableTokens + tokensToAdd, MaxTokens);
            LastRefillAt = now;
        }
    }
}
```

---

## Application Layer: Saga State

### RecipeProcessingSagaState

**Purpose**: Persists the state of the multi-step processing workflow.

**Properties**:

```csharp
public class RecipeProcessingSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }               // MassTransit saga correlation
    public string CurrentState { get; set; }              // State machine state

    public Guid BatchId { get; set; }                     // RecipeBatch.Id
    public string ProviderId { get; set; }                // e.g., "provider_001"
    public int BatchSize { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public DateTime StartedAt { get; set; }

    public List<string> DiscoveredUrls { get; set; }      // URLs found during discovery
    public List<string> FingerprintedUrls { get; set; }   // URLs fingerprinted (not duplicates)
    public List<string> ProcessedUrls { get; set; }       // URLs successfully processed
    public List<string> FailedUrls { get; set; }          // URLs permanently failed

    public int CurrentIndex { get; set; }                 // Resume index (for crash recovery)
}
```

**State Transitions**:

- `Idle` → `Discovering` (batch started)
- `Discovering` → `Fingerprinting` (URLs discovered)
- `Fingerprinting` → `Processing` (duplicates filtered)
- `Processing` → `Persisting` (recipes parsed and normalized)
- `Persisting` → `Completed` (recipes persisted to MongoDB)
- Any state → `Failed` (non-recoverable error)

**Compensation Logic**:

- **Transient errors** (network, timeout): Retry with exponential backoff (up to `RetryCount`)
- **Permanent errors** (invalid data): Log, add to `FailedUrls`, continue processing
- **Timeout errors** (time window exceeded): Save state, exit gracefully (resume next run)

---

## MongoDB Document Schemas

All entities persisted to MongoDB using EasyMeals.Shared.Data repository pattern.

### 1. ProviderConfigurationDocument

**Collection**: `provider_configurations`

**Purpose**: Store provider-specific settings privately in database (not committed to GitHub).

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
    public string RecipeRootUrl { get; set; }         // Sensitive: stored privately in DB
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

**Indexes**:

- Primary: `{ Id: 1 }` (unique)
- Query: `{ ProviderId: 1 }` (unique, lookup by provider ID)
- Query: `{ Enabled: 1, CreatedAt: -1 }` (find enabled providers)

**Security Notes**:

- `RecipeRootUrl` contains provider-specific URLs (TOS-sensitive)
- Never expose via public APIs without sanitization
- Access restricted to admin CLI tools or internal services

---

### 2. RecipeBatchDocument

**Collection**: `recipe_batches`

```csharp
[BsonCollection("recipe_batches")]
public class RecipeBatchDocument : BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string ProviderId { get; set; }
    public int BatchSize { get; set; }
    public int TimeWindowMinutes { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProcessedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public string Status { get; set; }                    // "Pending", "InProgress", "Completed", "Failed"
    public List<string> ProcessedUrls { get; set; }
    public List<string> FailedUrls { get; set; }
}
```

**Indexes**:

- Primary: `{ Id: 1 }` (unique)
- Query: `{ ProviderId: 1, StartedAt: -1 }` (find recent batches per provider)
- Query: `{ Status: 1, StartedAt: -1 }` (find in-progress batches)

---

### 3. RecipeDocument (extend existing)

**Collection**: `recipes`

```csharp
[BsonCollection("recipes")]
public class RecipeDocument : BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string ProviderId { get; set; }
    public string ProviderRecipeId { get; set; }
    public string FingerprintHash { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Url { get; set; }
    public DateTime ScrapedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }

    public List<IngredientReferenceDocument> Ingredients { get; set; }
    // ... other properties (instructions, nutrition, etc.)
}

public class IngredientReferenceDocument
{
    public string ProviderCode { get; set; }
    public string? CanonicalForm { get; set; }
    public string Quantity { get; set; }
    public int DisplayOrder { get; set; }
}
```

**Indexes**:

- Primary: `{ Id: 1 }` (unique)
- Query: `{ ProviderId: 1, Url: 1 }` (find recipe by provider URL, unique)
- Query: `{ FingerprintHash: 1 }` (duplicate detection, unique)
- Full-text search: `{ Title: "text", Description: "text" }` (future feature)

---

### 4. IngredientMappingDocument

**Collection**: `ingredient_mappings`

```csharp
[BsonCollection("ingredient_mappings")]
public class IngredientMappingDocument : BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string ProviderId { get; set; }
    public string ProviderCode { get; set; }
    public string CanonicalForm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Indexes**:

- Primary: `{ Id: 1 }` (unique)
- Query: `{ ProviderId: 1, ProviderCode: 1 }` (lookup mapping, unique compound index)
- Query: `{ CanonicalForm: 1 }` (reverse lookup: find provider codes for canonical form)

---

### 5. RecipeFingerprintDocument

**Collection**: `recipe_fingerprints`

```csharp
[BsonCollection("recipe_fingerprints")]
public class RecipeFingerprintDocument : BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string FingerprintHash { get; set; }
    public string ProviderId { get; set; }
    public string RecipeUrl { get; set; }
    public Guid RecipeId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

**Indexes**:

- Primary: `{ Id: 1 }` (unique)
- Query: `{ FingerprintHash: 1 }` (duplicate detection, unique)
- Query: `{ ProviderId: 1, ProcessedAt: -1 }` (find recent fingerprints per provider)

---

### 6. RecipeProcessingSagaStateDocument

**Collection**: `recipe_processing_saga_states` (MassTransit managed)

```csharp
[BsonCollection("recipe_processing_saga_states")]
public class RecipeProcessingSagaStateDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid CorrelationId { get; set; }               // MassTransit saga correlation

    public string CurrentState { get; set; }              // "Discovering", "Fingerprinting", etc.
    public Guid BatchId { get; set; }
    public string ProviderId { get; set; }
    public int BatchSize { get; set; }
    public int TimeWindowMinutes { get; set; }
    public DateTime StartedAt { get; set; }

    public List<string> DiscoveredUrls { get; set; }
    public List<string> FingerprintedUrls { get; set; }
    public List<string> ProcessedUrls { get; set; }
    public List<string> FailedUrls { get; set; }

    public int CurrentIndex { get; set; }
}
```

**Indexes**:

- Primary: `{ CorrelationId: 1 }` (unique, MassTransit managed)
- Query: `{ CurrentState: 1 }` (find sagas in specific state, for debugging)

---

## Database Initialization Strategy (via EasyMeals.Shared.Data)

**✅ Index Creation is Automatic** - No manual `IHostedService` or `Program.cs` initialization needed!

The `EasyMeals.Shared.Data` infrastructure handles all index creation via the fluent builder API:

```csharp
// Program.cs - Simple, declarative setup
var connectionString = builder.Configuration.GetConnectionString("MongoDB")
    ?? throw new InvalidOperationException("MongoDB connection string not found");
var databaseName = builder.Configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDB database name not found");

// MongoDB configuration + default indexes + custom indexes (all automatic!)
services.AddEasyMealsMongoDb(builder.Configuration);

await services
    .ConfigureEasyMealsDatabase()
    .AddRepository<RecipeBatchDocument>()
    .AddRepository<RecipeDocument>()
    .AddRepository<IngredientMappingDocument>()
    .AddRepository<RecipeFingerprintDocument>()
    .AddRepository<ProviderConfigurationDocument>()
    .WithDefaultIndexes()  // Creates base indexes on ALL collections (Id, CreatedAt, UpdatedAt)
    .WithCustomIndexes<RecipeBatchDocument>(async collection =>
    {
        // Recipe Engine-specific indexes for RecipeBatch
        var batchIndexes = new[]
        {
            new CreateIndexModel<RecipeBatchDocument>(
                Builders<RecipeBatchDocument>.IndexKeys
                    .Ascending(b => b.ProviderId)
                    .Descending(b => b.StartedAt),
                new CreateIndexOptions { Name = "idx_batch_provider_date", Background = true }),
            new CreateIndexModel<RecipeBatchDocument>(
                Builders<RecipeBatchDocument>.IndexKeys.Ascending(b => b.Status),
                new CreateIndexOptions { Name = "idx_batch_status", Background = true })
        };
        await collection.Indexes.CreateManyAsync(batchIndexes);
    })
    .WithCustomIndexes<RecipeDocument>(async collection =>
    {
        // Recipe Engine-specific indexes for Recipe
        var recipeIndexes = new[]
        {
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys
                    .Ascending(r => r.ProviderId)
                    .Ascending(r => r.Url),
                new CreateIndexOptions { Name = "idx_recipe_provider_url", Unique = true, Background = true }),
            new CreateIndexModel<RecipeDocument>(
                Builders<RecipeDocument>.IndexKeys.Ascending(r => r.FingerprintHash),
                new CreateIndexOptions { Name = "idx_recipe_fingerprint", Background = true })
        };
        await collection.Indexes.CreateManyAsync(recipeIndexes);
    })
    .WithCustomIndexes<IngredientMappingDocument>(async collection =>
    {
        // Ingredient Mapping indexes
        var ingredientIndexes = new[]
        {
            new CreateIndexModel<IngredientMappingDocument>(
                Builders<IngredientMappingDocument>.IndexKeys
                    .Ascending(i => i.ProviderId)
                    .Ascending(i => i.ProviderCode),
                new CreateIndexOptions { Name = "idx_ingredient_provider_code", Unique = true, Background = true }),
            new CreateIndexModel<IngredientMappingDocument>(
                Builders<IngredientMappingDocument>.IndexKeys.Ascending(i => i.CanonicalForm),
                new CreateIndexOptions { Name = "idx_ingredient_canonical", Background = true })
        };
        await collection.Indexes.CreateManyAsync(ingredientIndexes);
    })
    .WithCustomIndexes<RecipeFingerprintDocument>(async collection =>
    {
        // Fingerprint indexes
        var fingerprintIndexes = new[]
        {
            new CreateIndexModel<RecipeFingerprintDocument>(
                Builders<RecipeFingerprintDocument>.IndexKeys.Ascending(f => f.FingerprintHash),
                new CreateIndexOptions { Name = "idx_fingerprint_hash", Unique = true, Background = true }),
            new CreateIndexModel<RecipeFingerprintDocument>(
                Builders<RecipeFingerprintDocument>.IndexKeys
                    .Ascending(f => f.ProviderId)
                    .Descending(f => f.ProcessedAt),
                new CreateIndexOptions { Name = "idx_fingerprint_provider_date", Background = true })
        };
        await collection.Indexes.CreateManyAsync(fingerprintIndexes);
    })
    .WithCustomIndexes<ProviderConfigurationDocument>(async collection =>
    {
        // Provider Config indexes
        var configIndexes = new[]
        {
            new CreateIndexModel<ProviderConfigurationDocument>(
                Builders<ProviderConfigurationDocument>.IndexKeys.Ascending(p => p.ProviderId),
                new CreateIndexOptions { Name = "idx_provider_id", Unique = true, Background = true }),
            new CreateIndexModel<ProviderConfigurationDocument>(
                Builders<ProviderConfigurationDocument>.IndexKeys
                    .Ascending(p => p.Enabled)
                    .Descending(p => p.CreatedAt),
                new CreateIndexOptions { Name = "idx_provider_enabled_date", Background = true })
        };
        await collection.Indexes.CreateManyAsync(configIndexes);
    })
    .EnsureDatabaseAsync();  // This single call: creates collections, creates all indexes, validates everything!
```

**What `.EnsureDatabaseAsync()` Does Automatically**:

✅ Validates MongoDB configuration  
✅ Creates collections if they don't exist  
✅ Creates all indexes with `Background = true` (non-blocking)  
✅ Validates all configurations  
✅ Registers all repositories in DI  
✅ Performs health checks

**No manual initialization code needed!**

### Using the Fluent Repository API

**Document Registration Pattern**:

All documents must:

1. Inherit from `BaseDocument` (provides Id, CreatedAt, UpdatedAt, Version)
2. Use `[BsonCollection("collection_name")]` attribute
3. Be registered via `.AddRepository<TDocument>()`

**Repository Injection**:

```csharp
public class RecipeProcessingApplicationService
{
    private readonly IMongoRepository<RecipeBatchDocument> _batchRepository;
    private readonly IMongoRepository<RecipeDocument> _recipeRepository;
    private readonly IReadOnlyMongoRepository<IngredientMappingDocument> _ingredientRepository;

    public RecipeProcessingApplicationService(
        IMongoRepository<RecipeBatchDocument> batchRepository,
        IMongoRepository<RecipeDocument> recipeRepository,
        IReadOnlyMongoRepository<IngredientMappingDocument> ingredientRepository)
    {
        _batchRepository = batchRepository;
        _recipeRepository = recipeRepository;
        _ingredientRepository = ingredientRepository;
    }

    public async Task ProcessBatchAsync(string providerId, CancellationToken cancellationToken)
    {
        // Create batch (write permission)
        var batch = new RecipeBatchDocument { ProviderId = providerId };
        await _batchRepository.InsertOneAsync(batch, cancellationToken);

        // Query ingredients (read-only)
        var mappings = await _ingredientRepository.GetAllAsync(cancellationToken);
    }
}
```

**Health Checks**:

Automatically registered when repositories are added via `EasyMeals.Shared.Data`. Check at `/health` endpoint (in ASP.NET Core).

---

## Schema Evolution

- **MongoDB Schemaless**: Add fields without migration (existing documents will have them as null)
- **Version Field**: Use `BaseDocument.Version` and `IncrementVersion()` for tracking schema changes
- **Breaking Changes**: Use versioned collection names (`recipes_v2`) or migration scripts
- **Backward Compatibility**: Query logic should handle missing fields gracefully

---

---

## Domain Services (Cross-Aggregate Operations)

Domain Services handle operations that span multiple aggregates or contain complex domain logic that doesn't naturally belong to a single entity.

### 1. IRecipeDuplicationChecker

**Purpose**: Check if a recipe already exists based on URL and fingerprint.

```csharp
public interface IRecipeDuplicationChecker
{
    Task<bool> IsDuplicateAsync(
        string url,
        string fingerprintHash,
        string providerId,
        CancellationToken cancellationToken = default);

    Task<RecipeFingerprint?> GetExistingFingerprintAsync(
        string url,
        string providerId,
        CancellationToken cancellationToken = default);
}
```

**Implementation Notes** (Infrastructure layer):

- Queries `IRecipeFingerprintRepository` for existing fingerprints
- Returns `true` if URL + ProviderId already exists with matching fingerprint
- Returns `false` if new or fingerprint changed (indicates recipe updated)

---

### 2. IBatchCompletionPolicy

**Purpose**: Determine if a batch should complete based on business rules (time window + batch size).

```csharp
public interface IBatchCompletionPolicy
{
    bool ShouldCompleteBatch(RecipeBatch batch, DateTime currentTime);

    BatchCompletionReason GetCompletionReason(RecipeBatch batch, DateTime currentTime);
}

public enum BatchCompletionReason
{
    NotComplete,
    BatchSizeReached,
    TimeWindowExceeded,
    Both
}
```

**Implementation Notes** (Infrastructure layer):

- Checks `batch.ProcessedCount >= batch.BatchSize` (batch size limit)
- Checks `currentTime - batch.StartedAt >= batch.TimeWindow` (time window limit)
- Returns `true` if either condition met

---

## Repository Interfaces (Aggregate Persistence)

Repository contracts defined in Domain layer, implemented in Infrastructure layer using EasyMeals.Shared.Data.

### 1. IRecipeBatchRepository

```csharp
public interface IRecipeBatchRepository
{
    Task<RecipeBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RecipeBatch?> GetActiveAsync(
        string providerId,
        CancellationToken cancellationToken = default);

    Task<RecipeBatch> CreateAsync(
        string providerId,
        ProviderConfiguration config,
        CancellationToken cancellationToken = default);

    Task SaveAsync(RecipeBatch batch, CancellationToken cancellationToken = default);

    Task<IEnumerable<RecipeBatch>> GetRecentBatchesAsync(
        string providerId,
        int count,
        CancellationToken cancellationToken = default);
}
```

---

### 2. IRecipeRepository

```csharp
public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Recipe?> GetByUrlAsync(
        string url,
        string providerId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(Recipe recipe, CancellationToken cancellationToken = default);

    Task SaveBatchAsync(
        IEnumerable<Recipe> recipes,
        CancellationToken cancellationToken = default);

    Task<int> CountByProviderAsync(
        string providerId,
        CancellationToken cancellationToken = default);
}
```

---

### 3. IIngredientMappingRepository

```csharp
public interface IIngredientMappingRepository
{
    Task<IngredientMapping?> GetByCodeAsync(
        string providerId,
        string providerCode,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<IngredientMapping>> GetAllByProviderAsync(
        string providerId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IngredientMapping mapping,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<IngredientMapping>> GetUnmappedCodesAsync(
        string providerId,
        CancellationToken cancellationToken = default);
}
```

---

### 4. IRecipeFingerprintRepository

```csharp
public interface IRecipeFingerprintRepository
{
    Task<RecipeFingerprint?> GetByUrlAsync(
        string url,
        string providerId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string url,
        string providerId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        RecipeFingerprint fingerprint,
        CancellationToken cancellationToken = default);

    Task SaveBatchAsync(
        IEnumerable<RecipeFingerprint> fingerprints,
        CancellationToken cancellationToken = default);

    Task<int> CountByProviderAsync(
        string providerId,
        CancellationToken cancellationToken = default);
}
```

---

## Data Flow Summary

1. **Batch Started**:
   - Create `RecipeBatch` entity
   - Persist `RecipeBatchDocument` to MongoDB
   - Create `RecipeProcessingSagaState` (correlationId = batchId)
   - Emit `BatchStartedEvent`

2. **Discovery Phase**:
   - `IDiscoveryService` fetches recipe URLs from provider
   - Store URLs in `RecipeProcessingSagaState.DiscoveredUrls`
   - Persist saga state to MongoDB

3. **Fingerprinting Phase**:
   - For each URL, generate fingerprint (SHA256 hash)
   - Query `recipe_fingerprints` collection for duplicates
   - If duplicate, skip; if not, add to `FingerprintedUrls`
   - Persist saga state to MongoDB

4. **Processing Phase**:
   - For each non-duplicate URL, fetch recipe data (HTTP request)
   - Parse recipe (title, description, ingredients)
   - Normalize ingredients via `IIngredientNormalizer`
   - Create `Recipe` entity with `IngredientReference` list
   - Add to `ProcessedUrls`
   - Persist saga state to MongoDB

5. **Persistence Phase**:
   - Batch insert `RecipeDocument` list to MongoDB
   - Batch insert `RecipeFingerprintDocument` list to MongoDB
   - Update `RecipeBatch` with final counts (processed, skipped, failed)
   - Persist `RecipeBatchDocument` to MongoDB
   - Emit `BatchCompletedEvent`

6. **Crash Recovery**:
   - On restart, load `RecipeProcessingSagaState` from MongoDB
   - Resume from `CurrentIndex` in `FingerprintedUrls` (skip already processed)
   - Continue processing from saved state

---

## Validation Rules

### RecipeBatch

- `BatchSize` must be > 0
- `TimeWindow` must be > 0
- `ProviderId` must not be empty

### Recipe

- `Title` must not be empty
- `Url` must be valid HTTPS URL
- `FingerprintHash` must be 64-character hex string (SHA256)

### IngredientMapping

- `ProviderId` must not be empty
- `ProviderCode` must not be empty
- `CanonicalForm` must not be empty

### ProviderConfiguration

- `ProviderId` must not be empty
- `BatchSize` must be > 0
- `TimeWindow` must be > 0
- `MinDelay` must be >= 0
- `MaxRequestsPerMinute` must be > 0
- `RetryCount` must be >= 0
- `RequestTimeout` must be > 0

---

## Next Steps

Phase 1 (Data Model) complete. Continue with:

1. Generate `contracts/` with service interfaces
2. Generate `quickstart.md` with development setup
3. Update agent context (copilot instructions)
