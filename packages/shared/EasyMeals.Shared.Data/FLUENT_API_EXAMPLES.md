# EasyMeals Repository Fluent API Usage Examples

The new EasyMeals repository system provides a modern, fluent API for configuring MongoDB repositories with clear separation of concerns and automatic health checks.

## Basic Setup

First, configure your MongoDB connection (this must be done before registering repositories):

```csharp
// Basic configuration
services.AddEasyMealsDataMongoDB(connectionString, databaseName);

// Or with options
services.AddEasyMealsDataWithOptions(options =>
{
    options.ConnectionString = connectionString;
    options.DatabaseName = databaseName;
    options.ApplicationName = "MyApp";
});
```

## Repository Registration Examples

### 1. Single Repository Registration

```csharp
// Add a full read-write repository for custom document type
await services
    .AddEasyMealsRepository<MyDocument>()
    .WithDefaultIndexes()
    .EnsureDatabaseAsync();

// Add a read-only repository
await services
    .AddReadOnlyEasyMealsRepository<MyDocument>()
    .WithDefaultIndexes()
    .EnsureDatabaseAsync();

// Specify permissions explicitly
await services
    .AddEasyMealsRepository<MyDocument>(RepositoryPermissions.Read)
    .WithDefaultIndexes()
    .EnsureDatabaseAsync();
```

### 2. Multiple Repository Registration

```csharp
await services
    .ConfigureEasyMealsRepositories()
    .AddRepository<Recipe>(RepositoryPermissions.ReadWrite)
    .AddRepository<User>(RepositoryPermissions.ReadWrite)
    .AddRepository<Category>(RepositoryPermissions.Read)
    .AddSharedRepository<IRecipeRepository>()
    .WithDefaultIndexes()
    .EnsureDatabaseAsync();
```

### 3. Advanced Configuration with Custom Indexes

```csharp
await services
    .ConfigureEasyMealsRepositories()
    .AddRepository<Recipe>()
    .AddRepository<User>()
    .ConfigureIndexes()
        .CreateCompoundIndex<Recipe>(
            Builders<Recipe>.IndexKeys
                .Ascending(r => r.Category)
                .Descending(r => r.CreatedAt),
            new CreateIndexOptions { Name = "category_created_idx" })
        .CreateTextIndex<Recipe>(
            Builders<Recipe>.IndexKeys.Text(r => r.Name).Text(r => r.Description),
            new CreateIndexOptions { Name = "recipe_search_idx" })
        .BuildIndexes()
    .WithCustomIndexes<User>(async collection =>
    {
        // Custom index creation logic
        var indexKeys = Builders<User>.IndexKeys.Ascending(u => u.Email);
        var indexOptions = new CreateIndexOptions
        {
            Unique = true,
            Name = "unique_email_idx"
        };
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<User>(indexKeys, indexOptions));
    })
    .EnsureDatabaseAsync();
```

## What Happens Automatically

### ✅ **Health Checks**

- Automatically registered whenever repositories are added
- No manual health check registration required
- Available at `/health` endpoint

### ✅ **Validation**

- Validates MongoDB is configured before allowing repository registration
- Clear error messages if configuration is missing
- Validates all configurations during `EnsureDatabaseAsync()`

### ✅ **Index Creation**

- Default indexes applied with `WithDefaultIndexes()`
- Custom indexes executed during `EnsureDatabaseAsync()`
- Indexes created in optimal order for performance

### ✅ **Dependency Injection**

- Proper repository interface registration based on permissions
- Read-only repositories only expose `IReadOnlyMongoRepository<T>`
- Full repositories expose both `IMongoRepository<T>` and `IReadOnlyMongoRepository<T>`

## Repository Permissions

```csharp
public enum RepositoryPermissions
{
    Read = 1,        // Read-only access (IReadOnlyMongoRepository<T>)
    ReadWrite = 2    // Full access (IMongoRepository<T> + IReadOnlyMongoRepository<T>)
}
```

## Error Handling

### Missing MongoDB Configuration

```csharp
// This will throw InvalidOperationException with clear message
services.AddEasyMealsRepository<Recipe>(); // ERROR: MongoDB not configured

// Correct way:
services.AddEasyMealsDataMongoDB(connectionString, databaseName);
services.AddEasyMealsRepository<Recipe>(); // OK
```

### Missing EnsureDatabaseAsync

```csharp
// Configuration is not applied until EnsureDatabaseAsync is called
services
    .AddEasyMealsRepository<Recipe>()
    .WithDefaultIndexes();
    // Repositories registered but indexes not created yet

// Must call:
await services
    .AddEasyMealsRepository<Recipe>()
    .WithDefaultIndexes()
    .EnsureDatabaseAsync(); // This applies all configurations
```

## Dependency Injection Usage

After configuration, inject repositories as normal:

```csharp
public class RecipeService
{
    private readonly IMongoRepository<Recipe> _recipeRepository;
    private readonly IReadOnlyMongoRepository<Category> _categoryRepository;

    public RecipeService(
        IMongoRepository<Recipe> recipeRepository,
        IReadOnlyMongoRepository<Category> categoryRepository)
    {
        _recipeRepository = recipeRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<Recipe> CreateRecipeAsync(Recipe recipe)
    {
        // Full repository allows write operations
        return await _recipeRepository.InsertOneAsync(recipe);
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync()
    {
        // Read-only repository only allows query operations
        return await _categoryRepository.GetAllAsync();
    }
}
```

## Migration from Old API

### Old Way:

```csharp
services.AddEasyMealsDataMongoDB(connectionString, databaseName);
services.AddSharedRepositories();
services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));
services.AddEasyMealsDataHealthChecks();
await services.EnsureEasyMealsDatabaseAsync();
```

### New Way:

```csharp
services.AddEasyMealsDataMongoDB(connectionString, databaseName);
await services
    .ConfigureEasyMealsRepositories()
    .AddSharedRepository<IRecipeRepository>()
    .WithDefaultIndexes()
    .EnsureDatabaseAsync();
```

## Benefits of the New API

- **🎯 Explicit Configuration**: Clear what repositories are registered and with what permissions
- **🔒 Security**: Read-only repositories prevent accidental writes
- **⚡ Performance**: Automatic health checks and optimized index creation
- **🧩 Fluent**: Chainable API for readable configuration
- **🛡️ Validation**: Early validation of configuration errors
- **📦 Minimal**: Only register what you need
