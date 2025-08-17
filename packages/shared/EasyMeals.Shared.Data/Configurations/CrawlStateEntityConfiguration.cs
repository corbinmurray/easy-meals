using EasyMeals.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMeals.Shared.Data.Configurations;

/// <summary>
/// Entity Framework configuration for CrawlStateEntity
/// Supports distributed crawling scenarios with proper constraints and indexes
/// </summary>
public class CrawlStateEntityConfiguration : IEntityTypeConfiguration<CrawlStateEntity>
{
    public void Configure(EntityTypeBuilder<CrawlStateEntity> builder)
    {
        // Primary key inherited from BaseEntity

        // Required properties
        builder.Property(c => c.SourceProvider)
            .IsRequired()
            .HasMaxLength(100);

        // JSON properties for flexible data storage
        builder.Property(c => c.PendingUrlsJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.CompletedRecipeIdsJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.FailedUrlsJson)
            .HasColumnType("text")
            .IsRequired();

        // Timestamp properties
        builder.Property(c => c.LastCrawlTime)
            .IsRequired();

        // Performance indexes for common queries
        builder.HasIndex(c => c.SourceProvider)
            .IsUnique()
            .HasDatabaseName("IX_CrawlStates_SourceProvider");

        builder.HasIndex(c => c.IsActive)
            .HasDatabaseName("IX_CrawlStates_IsActive");

        builder.HasIndex(c => c.LastCrawlTime)
            .HasDatabaseName("IX_CrawlStates_LastCrawlTime");

        // Business rule constraints for data integrity
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_CrawlState_TotalProcessed", "TotalProcessed >= 0");
            t.HasCheckConstraint("CK_CrawlState_TotalSuccessful", "TotalSuccessful >= 0");
            t.HasCheckConstraint("CK_CrawlState_TotalFailed", "TotalFailed >= 0");
            t.HasCheckConstraint("CK_CrawlState_SuccessfulFailed", "TotalSuccessful + TotalFailed <= TotalProcessed");
        });
    }
}
