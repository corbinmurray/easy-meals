# Usage Examples for EasyMeals.Shared.Data

## Example 1: Simple Usage in an API Project

```csharp
// Program.cs
using EasyMeals.Shared.Data.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Option 1: SQL Server
builder.Services.AddEasyMealsDataSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")!);

// Option 2: PostgreSQL
// builder.Services.AddEasyMealsDataPostgreSQL(connectionString);

// Option 3: In-Memory for testing
// builder.Services.AddEasyMealsDataInMemory();

// Add health checks
builder.Services.AddEasyMealsDataHealthChecks();

// Ensure database is created (for development)
builder.Services.EnsureEasyMealsDatabase();

var app = builder.Build();

// Use health checks
app.MapHealthChecks("/health");

app.Run();
```

## Example 2: Using Repositories in a Service

```csharp
using EasyMeals.Shared.Data.Entities;
using EasyMeals.Shared.Data.Repositories;

public class RecipeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecipeRepository _recipeRepository;

    public RecipeService(IUnitOfWork unitOfWork, IRecipeRepository recipeRepository)
    {
        _unitOfWork = unitOfWork;
        _recipeRepository = recipeRepository;
    }

    public async Task<IEnumerable<RecipeEntity>> GetActiveRecipesAsync()
    {
        return await _recipeRepository.GetAllAsync(r => r.IsActive);
    }

    public async Task<PagedResult<RecipeEntity>> SearchRecipesAsync(
        string searchTerm, int page = 1, int pageSize = 20)
    {
        return await _recipeRepository.SearchAsync(searchTerm, page, pageSize);
    }

    public async Task<RecipeEntity> CreateRecipeAsync(RecipeEntity recipe)
    {
        // Business validation
        if (await _recipeRepository.ExistsBySourceUrlAsync(recipe.SourceUrl))
        {
            throw new InvalidOperationException("Recipe with this URL already exists");
        }

        await _recipeRepository.AddAsync(recipe);
        await _unitOfWork.SaveChangesAsync();

        return recipe;
    }

    public async Task UpdateRecipeAsync(RecipeEntity recipe)
    {
        _recipeRepository.Update(recipe);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteRecipeAsync(string id)
    {
        var recipe = await _recipeRepository.GetByIdAsync(id);
        if (recipe is not null)
        {
            recipe.SoftDelete(); // Uses soft delete from BaseSoftDeletableEntity
            _recipeRepository.Update(recipe);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
```

## Example 3: Extending with Application-Specific Entities

```csharp
// 1. Create your application-specific entity
public class MealPlanEntity : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string RecipeIdsJson { get; set; } = "[]"; // JSON array of recipe IDs
}

// 2. Create your application-specific DbContext
public class MealPlannerDbContext : EasyMealsAppDbContext<MealPlannerDbContext>
{
    public MealPlannerDbContext(DbContextOptions<MealPlannerDbContext> options) : base(options) { }

    public DbSet<MealPlanEntity> MealPlans { get; set; } = null!;

    protected override void ConfigureApplicationEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MealPlanEntity>(entity =>
        {
            entity.ToTable("MealPlans");

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.RecipeIdsJson)
                .HasColumnType("text")
                .IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_MealPlans_UserId");

            entity.HasIndex(e => new { e.UserId, e.StartDate })
                .HasDatabaseName("IX_MealPlans_UserId_StartDate");
        });
    }
}

// 3. Create repository interface
public interface IMealPlanRepository : IRepository<MealPlanEntity>
{
    Task<IEnumerable<MealPlanEntity>> GetUserMealPlansAsync(string userId, CancellationToken cancellationToken = default);
    Task<MealPlanEntity?> GetUserMealPlanForDateAsync(string userId, DateTime date, CancellationToken cancellationToken = default);
}

// 4. Implement repository
public class MealPlanRepository : Repository<MealPlanEntity>, IMealPlanRepository
{
    public MealPlanRepository(EasyMealsDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<MealPlanEntity>> GetUserMealPlansAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(mp => mp.UserId == userId, cancellationToken);
    }

    public async Task<MealPlanEntity?> GetUserMealPlanForDateAsync(string userId, DateTime date, CancellationToken cancellationToken = default)
    {
        return await FirstOrDefaultAsync(mp =>
            mp.UserId == userId &&
            mp.StartDate <= date &&
            mp.EndDate >= date, cancellationToken);
    }
}

// 5. Register in DI
// Program.cs
builder.Services.AddApplicationData<MealPlannerDbContext>(options =>
{
    options.UseSqlServer(connectionString);
}, services =>
{
    services.AddScoped<IMealPlanRepository, MealPlanRepository>();
});
```

## Example 4: Transaction Management

```csharp
public class ComplexBusinessService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ICrawlStateRepository _crawlStateRepository;

    public ComplexBusinessService(
        IUnitOfWork unitOfWork,
        IRecipeRepository recipeRepository,
        ICrawlStateRepository crawlStateRepository)
    {
        _unitOfWork = unitOfWork;
        _recipeRepository = recipeRepository;
        _crawlStateRepository = crawlStateRepository;
    }

    public async Task ProcessCrawlResultsAsync(
        IEnumerable<RecipeEntity> newRecipes,
        CrawlStateEntity crawlState)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Add all new recipes
            await _recipeRepository.AddRangeAsync(newRecipes);

            // Update crawl state
            await _crawlStateRepository.SaveStateAsync(crawlState);

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
}
```

## Example 5: Testing with In-Memory Database

```csharp
public class RecipeServiceTests
{
    private ServiceProvider GetServiceProvider()
    {
        var services = new ServiceCollection();

        // Use in-memory database for testing
        services.AddEasyMealsDataInMemory(Guid.NewGuid().ToString());

        return services.BuildServiceProvider();
    }

    [Test]
    public async Task CreateRecipe_ValidRecipe_ReturnsCreatedRecipe()
    {
        // Arrange
        using var serviceProvider = GetServiceProvider();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        var recipeRepository = serviceProvider.GetRequiredService<IRecipeRepository>();

        var recipe = new RecipeEntity
        {
            Id = "test-recipe-1",
            Title = "Test Recipe",
            SourceUrl = "https://example.com/recipe/1",
            SourceProvider = "TestProvider"
        };

        // Act
        await recipeRepository.AddAsync(recipe);
        await unitOfWork.SaveChangesAsync();

        // Assert
        var savedRecipe = await recipeRepository.GetByIdAsync("test-recipe-1");
        Assert.IsNotNull(savedRecipe);
        Assert.AreEqual("Test Recipe", savedRecipe.Title);
    }
}
```
