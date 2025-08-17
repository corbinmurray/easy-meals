using EasyMeals.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMeals.Shared.Data.Configurations;

/// <summary>
/// Entity Framework configuration for RecipeEntity
/// Follows DDD principles with proper constraints and indexes for performance
/// </summary>
public class RecipeEntityConfiguration : IEntityTypeConfiguration<RecipeEntity>
{
    public void Configure(EntityTypeBuilder<RecipeEntity> builder)
    {
        // Primary key inherited from BaseEntity

        // Required properties
        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.SourceUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.SourceProvider)
            .IsRequired()
            .HasMaxLength(100);

        // Optional properties with constraints
        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.Property(r => r.ImageUrl)
            .HasMaxLength(1000);

        // JSON properties - use appropriate column types for your database
        builder.Property(r => r.IngredientsJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.InstructionsJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.NutritionInfoJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.TagsJson)
            .HasColumnType("text")
            .IsRequired();

        // Performance indexes - critical for query performance
        builder.HasIndex(r => r.SourceProvider)
            .HasDatabaseName("IX_Recipes_SourceProvider");

        builder.HasIndex(r => r.IsActive)
            .HasDatabaseName("IX_Recipes_IsActive");

        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Recipes_CreatedAt");

        builder.HasIndex(r => r.IsDeleted)
            .HasDatabaseName("IX_Recipes_IsDeleted");

        // Composite indexes for common query patterns
        builder.HasIndex(r => new { r.SourceProvider, r.IsActive, r.IsDeleted })
            .HasDatabaseName("IX_Recipes_SourceProvider_IsActive_IsDeleted");

        builder.HasIndex(r => new { r.IsActive, r.IsDeleted, r.CreatedAt })
            .HasDatabaseName("IX_Recipes_IsActive_IsDeleted_CreatedAt");

        // Business rule constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Recipe_PrepTimeMinutes", "PrepTimeMinutes >= 0");
            t.HasCheckConstraint("CK_Recipe_CookTimeMinutes", "CookTimeMinutes >= 0");
            t.HasCheckConstraint("CK_Recipe_Servings", "Servings > 0");
        });

        // Global query filter for soft delete support
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
