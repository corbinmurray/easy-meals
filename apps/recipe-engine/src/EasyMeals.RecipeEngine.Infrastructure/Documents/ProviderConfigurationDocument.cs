using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
///     MongoDB document for provider-specific configuration settings.
///     Stored in database to keep sensitive URLs and rate limits private.
/// </summary>
[BsonCollection("provider_configurations")]
public class ProviderConfigurationDocument : BaseDocument
{
    [BsonElement("providerId")]
    [BsonRequired]
    public string ProviderId { get; set; } = string.Empty;

    [BsonElement("enabled")]
    [BsonRequired]
    public bool Enabled { get; set; }

    // Nested documents mapping to Domain.ValueObjects
    [BsonElement("endpoint")]
    public EndpointInfoDocument Endpoint { get; set; } = new EndpointInfoDocument();

    [BsonElement("discovery")]
    public DiscoveryConfigDocument Discovery { get; set; } = new DiscoveryConfigDocument();

    [BsonElement("batching")]
    public BatchingConfigDocument Batching { get; set; } = new BatchingConfigDocument();

    [BsonElement("rateLimit")]
    public RateLimitConfigDocument RateLimit { get; set; } = new RateLimitConfigDocument();

    [BsonElement("createdBy")] public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("updatedBy")] public string? UpdatedBy { get; set; }
}