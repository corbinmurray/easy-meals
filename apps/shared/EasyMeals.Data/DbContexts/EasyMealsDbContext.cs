using EasyMeals.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyMeals.Data.DbContexts;

/// <summary>
/// Main database context for the EasyMeals application
/// Supports multiple providers: In-Memory, PostgreSQL, MongoDB (via EF Core provider)
/// </summary>
public class EasyMealsDbContext : DbContext
{
    public EasyMealsDbContext(DbContextOptions<EasyMealsDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Recipes collection
    /// </summary>
    public DbSet<RecipeEntity> Recipes { get; set; } = null!;

    /// <summary>
    /// Crawl state collection (for crawler service)
    /// </summary>
    public DbSet<CrawlStateEntity> CrawlStates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EasyMealsDbContext).Assembly);
        
        // Configure table names (following .NET conventions)
        modelBuilder.Entity<RecipeEntity>().ToTable("Recipes");
        modelBuilder.Entity<CrawlStateEntity>().ToTable("CrawlStates");
        
        // Configure indexes for performance
        modelBuilder.Entity<RecipeEntity>()
            .HasIndex(r => r.SourceProvider)
            .HasDatabaseName("IX_Recipes_SourceProvider");
            
        modelBuilder.Entity<RecipeEntity>()
            .HasIndex(r => r.IsActive)
            .HasDatabaseName("IX_Recipes_IsActive");
            
        modelBuilder.Entity<RecipeEntity>()
            .HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Recipes_CreatedAt");
            
        modelBuilder.Entity<CrawlStateEntity>()
            .HasIndex(c => c.SourceProvider)
            .HasDatabaseName("IX_CrawlStates_SourceProvider");
    }
}
