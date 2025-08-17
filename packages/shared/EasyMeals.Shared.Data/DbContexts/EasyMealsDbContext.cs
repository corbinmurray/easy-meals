using EasyMeals.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EasyMeals.Shared.Data.DbContexts;

/// <summary>
/// Main database context for the EasyMeals application
/// Supports multi-application scenarios with shared infrastructure
/// Follows DDD principles with proper aggregate boundaries and audit trail support
/// </summary>
public class EasyMealsDbContext(DbContextOptions<EasyMealsDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Recipes collection - core domain aggregate
    /// </summary>
    public DbSet<RecipeEntity> Recipes { get; set; } = null!;

    /// <summary>
    /// Crawl state collection - supports distributed crawling operations
    /// </summary>
    public DbSet<CrawlStateEntity> CrawlStates { get; set; } = null!;

    /// <summary>
    /// Override to add or modify entities dynamically
    /// Allows applications to register their own entities while sharing the same context
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EasyMealsDbContext).Assembly);

        // Configure table names following .NET conventions
        modelBuilder.Entity<RecipeEntity>().ToTable("Recipes");
        modelBuilder.Entity<CrawlStateEntity>().ToTable("CrawlStates");

        // Allow derived contexts to add additional configurations
        OnModelCreatingPartial(modelBuilder);
    }

    /// <summary>
    /// Partial method for derived contexts to add their own entity configurations
    /// Supports the Open/Closed Principle - open for extension, closed for modification
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    protected virtual void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Empty to allow for derived contexts to add additional configurations
    }

    /// <summary>
    /// Override SaveChanges to add audit trail support
    /// Automatically updates audit fields for entities implementing IAuditableEntity
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities written to the database</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Updates audit fields for entities that implement IAuditableEntity
    /// Ensures consistent audit trail across all entity operations
    /// </summary>
    private void UpdateAuditFields()
    {
        IEnumerable<EntityEntry> entries = ChangeTracker.Entries()
            .Where(e => e is { Entity: IAuditableEntity, State: EntityState.Added or EntityState.Modified });

        foreach (EntityEntry entry in entries)
        {
            var auditableEntity = (IAuditableEntity)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                auditableEntity.CreatedAt = DateTime.UtcNow;
            }

            auditableEntity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
