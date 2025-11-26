using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Persistence.Mongo.Documents.ProviderConfiguration;

/// <summary>
/// Embedded document for CSS extraction selectors.
/// </summary>
public class ExtractionSelectorsDocument
{
    [BsonElement("titleSelector")]
    public string TitleSelector { get; set; } = string.Empty;

    [BsonElement("titleFallbackSelector")]
    [BsonIgnoreIfNull]
    public string? TitleFallbackSelector { get; set; }

    [BsonElement("descriptionSelector")]
    public string DescriptionSelector { get; set; } = string.Empty;

    [BsonElement("descriptionFallbackSelector")]
    [BsonIgnoreIfNull]
    public string? DescriptionFallbackSelector { get; set; }

    [BsonElement("ingredientsSelector")]
    public string IngredientsSelector { get; set; } = string.Empty;

    [BsonElement("instructionsSelector")]
    public string InstructionsSelector { get; set; } = string.Empty;

    [BsonElement("prepTimeSelector")]
    [BsonIgnoreIfNull]
    public string? PrepTimeSelector { get; set; }

    [BsonElement("cookTimeSelector")]
    [BsonIgnoreIfNull]
    public string? CookTimeSelector { get; set; }

    [BsonElement("totalTimeSelector")]
    [BsonIgnoreIfNull]
    public string? TotalTimeSelector { get; set; }

    [BsonElement("servingsSelector")]
    [BsonIgnoreIfNull]
    public string? ServingsSelector { get; set; }

    [BsonElement("imageUrlSelector")]
    [BsonIgnoreIfNull]
    public string? ImageUrlSelector { get; set; }

    [BsonElement("authorSelector")]
    [BsonIgnoreIfNull]
    public string? AuthorSelector { get; set; }

    [BsonElement("cuisineSelector")]
    [BsonIgnoreIfNull]
    public string? CuisineSelector { get; set; }

    [BsonElement("difficultySelector")]
    [BsonIgnoreIfNull]
    public string? DifficultySelector { get; set; }

    [BsonElement("nutritionSelector")]
    [BsonIgnoreIfNull]
    public string? NutritionSelector { get; set; }
}
