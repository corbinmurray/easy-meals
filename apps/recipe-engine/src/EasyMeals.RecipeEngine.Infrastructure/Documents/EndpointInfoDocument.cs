using EasyMeals.Shared.Data.Attributes;
using EasyMeals.Shared.Data.Documents;
using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.RecipeEngine.Infrastructure.Documents;

/// <summary>
///     Embedded document for provider endpoint information.
///     Mirrors Domain.ValueObjects.EndpointInfo which currently only contains RecipeRootUrl.
/// </summary>
public class EndpointInfoDocument
{
    [BsonElement("recipeRootUrl")]
    [BsonRequired]
    public string RecipeRootUrl { get; set; } = string.Empty;
}
