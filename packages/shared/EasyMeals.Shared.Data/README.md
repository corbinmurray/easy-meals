# EasyMeals.Shared.Data

A comprehensive shared data infrastructure package for EasyMeals applications, built following Domain-Driven Design (DDD) principles and SOLID design patterns.

## Features

- 🏗️ **Shared Database Context** - Common way for multiple .NET apps to connect to the EasyMeals database
- 🔧 **Extensible Entity Framework** - Applications can declare their own entities while sharing infrastructure
- 📦 **Repository Pattern** - Generic and specific repositories with Unit of Work support
- 🔌 **Dependency Injection** - Seamless DI integration with multiple database provider support
- 📊 **Health Checks** - Built-in database connectivity monitoring
- 🎯 **Domain-Driven Design** - Proper aggregate boundaries, audit trails, and business rule enforcement

## Quick Start

### 1. Basic Setup (SQL Server)

```csharp
// In Program.cs
services.AddEasyMealsDataSqlServer(connectionString);

// Optional: Ensure database is created
services.EnsureEasyMealsDatabase();

// Optional: Add health checks
services.AddEasyMealsDataHealthChecks();
```

### 2. Alternative Database Providers

```csharp
// PostgreSQL
services.AddEasyMealsDataPostgreSQL(connectionString);

// In-Memory (for testing)
services.AddEasyMealsDataInMemory();

// Custom provider
services.AddEasyMealsDataCore(options => {
    options.UseSqlServer(connectionString);
    // Add custom configurations
});
```

### 3. Using Repositories

```csharp
public class RecipeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecipeRepository _recipeRepository;

    public RecipeService(IUnitOfWork unitOfWork, IRecipeRepository recipeRepository)
    {
        _unitOfWork = unitOfWork;
        _recipeRepository = recipeRepository;
    }

    public async Task<RecipeEntity?> GetRecipeAsync(string id)
    {
        return await _recipeRepository.GetByIdAsync(id);
    }

    public async Task<PagedResult<RecipeEntity>> SearchRecipesAsync(
        string searchTerm, int pageNumber, int pageSize)
    {
        return await _recipeRepository.SearchAsync(searchTerm, pageNumber, pageSize);
    }

    public async Task CreateRecipeAsync(RecipeEntity recipe)
    {
        await _recipeRepository.AddAsync(recipe);
        await _unitOfWork.SaveChangesAsync();
    }
}
```

### 4. Using Generic Repository Pattern

```csharp
public class GenericService<TEntity> where TEntity : class
{
    private readonly IRepository<TEntity> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public GenericService(IRepository<TEntity> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<TEntity>> GetPagedAsync(int pageNumber, int pageSize)
    {
        return await _repository.GetPagedAsync(pageNumber, pageSize);
    }
}
```

## Advanced Usage

### Application-Specific Entity Extensions

Your applications can extend the shared infrastructure by adding their own entities:

```csharp
// 1. Create your application-specific DbContext
public class MyAppDbContext : EasyMealsAppDbContext<MyAppDbContext>
{
    public MyAppDbContext(DbContextOptions<MyAppDbContext> options) : base(options) { }

    // Add your application-specific DbSets
    public DbSet<MyCustomEntity> MyEntities { get; set; } = null!;

    protected override void ConfigureApplicationEntities(ModelBuilder modelBuilder)
    {
        // Configure your application-specific entities
        modelBuilder.Entity<MyCustomEntity>().ToTable("MyEntities");

        // Add indexes, relationships, constraints, etc.
        modelBuilder.Entity<MyCustomEntity>()
            .HasIndex(e => e.SomeProperty)
            .HasDatabaseName("IX_MyEntities_SomeProperty");
    }
}

// 2. Register in your DI container
services.AddApplicationData<MyAppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
}, services =>
{
    // Register your application-specific repositories
    services.AddScoped<IMyEntityRepository, MyEntityRepository>();
});
```

### Custom Entity Base Classes

Extend the base entity classes for consistent behavior:

```csharp
// For entities that need audit trails
public class MyEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// For entities that support soft delete
public class MySoftDeletableEntity : BaseSoftDeletableEntity
{
    public string Title { get; set; } = string.Empty;

    // Automatically gets audit trail and soft delete functionality
}
```

### Transaction Management

```csharp
public async Task ComplexBusinessOperationAsync()
{
    await _unitOfWork.BeginTransactionAsync();

    try
    {
        // Multiple operations within transaction
        await _recipeRepository.AddAsync(recipe);
        await _crawlStateRepository.SaveStateAsync(state);

        // Commit all changes atomically
        await _unitOfWork.CommitTransactionAsync();
    }
    catch
    {
        // Rollback on any error
        await _unitOfWork.RollbackTransactionAsync();
        throw;
    }
}
```

## Architecture

### Entities

- **BaseEntity** - Provides Id and audit trail (CreatedAt, UpdatedAt)
- **BaseSoftDeletableEntity** - Adds soft delete capability (IsDeleted, DeletedAt)
- **RecipeEntity** - Core recipe data with full business functionality
- **CrawlStateEntity** - Manages distributed crawling operations

### Repositories

- **IRepository<T>** - Generic repository interface with CRUD operations
- **IRecipeRepository** - Recipe-specific operations (search, filtering, etc.)
- **ICrawlStateRepository** - Crawl state management operations
- **IUnitOfWork** - Transaction management and repository coordination

### Design Patterns

- **Repository Pattern** - Abstracts data access logic
- **Unit of Work Pattern** - Manages transactions and entity state
- **Template Method Pattern** - Allows applications to extend shared functionality
- **Dependency Inversion Principle** - Depends on abstractions, not implementations

## Configuration Options

### Database Providers

| Provider   | Method                         | Use Case                               |
| ---------- | ------------------------------ | -------------------------------------- |
| SQL Server | `AddEasyMealsDataSqlServer()`  | Production, enterprise scenarios       |
| PostgreSQL | `AddEasyMealsDataPostgreSQL()` | High-performance, JSON-heavy workloads |
| In-Memory  | `AddEasyMealsDataInMemory()`   | Testing, development                   |
| Custom     | `AddEasyMealsDataCore()`       | Any EF Core provider                   |

### Health Checks

```csharp
services.AddEasyMealsDataHealthChecks(
    name: "database",
    tags: ["database", "critical"]
);
```

### Connection Resilience

The package automatically configures connection resilience:

- **Retry Logic** - 3 retry attempts with exponential backoff
- **Connection Pooling** - Optimized for high-throughput scenarios
- **Timeout Handling** - Proper timeout configuration for different operations

## Best Practices

1. **Use Unit of Work** for transaction management
2. **Leverage Repository Interfaces** for testability
3. **Follow DDD Principles** - keep business logic in domain, not repositories
4. **Use Pagination** for large datasets
5. **Implement Health Checks** for production monitoring
6. **Extend Thoughtfully** - only add entities that truly belong in shared context

## Performance Considerations

- **Indexes** - All critical query paths have optimized indexes
- **Soft Delete Filtering** - Global query filters prevent loading deleted entities
- **Audit Trail Automation** - Automatic timestamp management
- **Connection Pooling** - Configured for optimal performance
- **Async Operations** - All data operations are async for scalability

## Testing

Use the in-memory provider for unit and integration tests:

```csharp
// In test setup
services.AddEasyMealsDataInMemory("TestDb");

// Tests automatically get isolated database instances
```

## Migration from Legacy

If migrating from the old `apps/shared/EasyMeals.Data`:

1. Update package references to `EasyMeals.Shared.Data`
2. Update namespace imports
3. Replace direct DbContext usage with Repository pattern
4. Update DI registration calls

## Contributing

When adding new shared entities:

1. Extend `BaseEntity` or `BaseSoftDeletableEntity`
2. Create entity configuration in `Configurations/`
3. Add specific repository interface if needed
4. Update `EasyMealsDbContext` with new DbSet
5. Add appropriate indexes and constraints
6. Include comprehensive unit tests following `MethodName_Condition_ExpectedResult()` pattern

## License

This package is part of the EasyMeals monorepo and follows the same licensing terms.
