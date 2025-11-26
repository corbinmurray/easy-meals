using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;

/// <summary>
/// Embedded document for API-specific settings.
/// </summary>
public class ApiSettingsDocument
{
    [BsonElement("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [BsonElement("authMethod")]
    [BsonRepresentation(BsonType.String)]
    public string AuthMethod { get; set; } = "None";

    [BsonElement("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [BsonElement("pageSizeParam")]
    [BsonIgnoreIfNull]
    public string? PageSizeParam { get; set; }

    [BsonElement("pageNumberParam")]
    [BsonIgnoreIfNull]
    public string? PageNumberParam { get; set; }

    [BsonElement("defaultPageSize")]
    public int DefaultPageSize { get; set; } = 20;
}
