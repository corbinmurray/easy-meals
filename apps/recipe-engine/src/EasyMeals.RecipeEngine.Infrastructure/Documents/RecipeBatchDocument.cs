using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using EasyMeals.RecipeEngine.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
/// MongoDB document representing a batch of recipes processed within a time window.
/// </summary>
[BsonCollection("recipe_batches")]
public class RecipeBatchDocument : BaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("providerId")]
    [BsonRequired]
    public string ProviderId { get; set; } = string.Empty;

    [BsonElement("batchSize")]
    [BsonRequired]
    public int BatchSize { get; set; }

    [BsonElement("timeWindowMinutes")]
    [BsonRequired]
    public int TimeWindowMinutes { get; set; }

    [BsonElement("startedAt")]
    [BsonRequired]
    public DateTime StartedAt { get; set; }

    [BsonElement("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("processedCount")]
    [BsonRequired]
    public int ProcessedCount { get; set; }

    [BsonElement("skippedCount")]
    [BsonRequired]
    public int SkippedCount { get; set; }

    [BsonElement("failedCount")]
    [BsonRequired]
    public int FailedCount { get; set; }

    [BsonElement("status")]
    [BsonRequired]
    public string Status { get; set; } = string.Empty;

    [BsonElement("processedUrls")]
    public List<string> ProcessedUrls { get; set; } = new();

    [BsonElement("failedUrls")]
    public List<string> FailedUrls { get; set; } = new();
}
