using EasyMeals.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMeals.Data.Configurations;

/// <summary>
/// Entity Framework configuration for CrawlStateEntity
/// </summary>
public class CrawlStateEntityConfiguration : IEntityTypeConfiguration<CrawlStateEntity>
{
    public void Configure(EntityTypeBuilder<CrawlStateEntity> builder)
    {
        // Primary key
        builder.HasKey(c => c.Id);
        
        // Properties
        builder.Property(c => c.Id)
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(c => c.PendingUrlsJson)
            .HasColumnType("text")
            .IsRequired();
            
        builder.Property(c => c.CompletedRecipeIdsJson)
            .HasColumnType("text")
            .IsRequired();
            
        builder.Property(c => c.FailedUrlsJson)
            .HasColumnType("text")
            .IsRequired();
            
        builder.Property(c => c.LastCrawlTime)
            .IsRequired();
            
        builder.Property(c => c.SourceProvider)
            .HasMaxLength(100)
            .IsRequired();
            
        builder.Property(c => c.CreatedAt)
            .IsRequired();
            
        builder.Property(c => c.UpdatedAt)
            .IsRequired();
        
        // Constraints
        builder.HasCheckConstraint("CK_CrawlState_TotalProcessed", "TotalProcessed >= 0");
        builder.HasCheckConstraint("CK_CrawlState_TotalSuccessful", "TotalSuccessful >= 0");
        builder.HasCheckConstraint("CK_CrawlState_TotalFailed", "TotalFailed >= 0");
    }
}
