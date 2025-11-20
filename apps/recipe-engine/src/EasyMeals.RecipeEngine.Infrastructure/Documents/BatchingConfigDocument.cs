using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
///     Embedded document for batching configuration per provider.
///     Maps to Domain.ValueObjects.BatchingConfig
/// </summary>
public class BatchingConfigDocument
{
    [BsonElement("batchSize")]
    [BsonRequired]
    public int BatchSize { get; set; }

    // Time window persisted in minutes for simplicity
    [BsonElement("timeWindowMinutes")]
    [BsonRequired]
    public int TimeWindowMinutes { get; set; }
}
