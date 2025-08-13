using EasyMeals.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMeals.Data.Configurations;

/// <summary>
/// Entity Framework configuration for RecipeEntity
/// </summary>
public class RecipeEntityConfiguration : IEntityTypeConfiguration<RecipeEntity>
{
    public void Configure(EntityTypeBuilder<RecipeEntity> builder)
    {
        // Primary key
        builder.HasKey(r => r.Id);
        
        // Properties
        builder.Property(r => r.Id)
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(r => r.Title)
            .HasMaxLength(500)
            .IsRequired();
            
        builder.Property(r => r.Description)
            .HasMaxLength(2000);
            
        builder.Property(r => r.IngredientsJson)
            .HasColumnType("text")
            .IsRequired();
            
        builder.Property(r => r.InstructionsJson)
            .HasColumnType("text")
            .IsRequired();
            
        builder.Property(r => r.ImageUrl)
            .HasMaxLength(1000);
            
        builder.Property(r => r.NutritionInfoJson)
            .HasColumnType("text")
            .IsRequired();
            
        builder.Property(r => r.TagsJson)
            .HasColumnType("text")
            .IsRequired();
            
        builder.Property(r => r.SourceUrl)
            .HasMaxLength(1000)
            .IsRequired();
            
        builder.Property(r => r.SourceProvider)
            .HasMaxLength(100)
            .IsRequired();
            
        builder.Property(r => r.CreatedAt)
            .IsRequired();
            
        builder.Property(r => r.UpdatedAt)
            .IsRequired();
            
        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
        
        // Constraints
        builder.HasCheckConstraint("CK_Recipe_PrepTime", "PrepTimeMinutes >= 0");
        builder.HasCheckConstraint("CK_Recipe_CookTime", "CookTimeMinutes >= 0");
        builder.HasCheckConstraint("CK_Recipe_Servings", "Servings > 0");
    }
}
