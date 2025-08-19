using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Shared.Data.Documents;

/// <summary>
/// Embedded document representing nutritional information for a recipe
/// Supports flexible nutrition data from various providers
/// </summary>
public class NutritionalInfoDocument
{
    /// <summary>
    /// Calories per serving
    /// </summary>
    [BsonElement("calories")]
    [BsonIgnoreIfNull]
    public int? Calories { get; set; }

    /// <summary>
    /// Total fat in grams per serving
    /// </summary>
    [BsonElement("fatGrams")]
    [BsonIgnoreIfNull]
    public decimal? FatGrams { get; set; }

    /// <summary>
    /// Saturated fat in grams per serving
    /// </summary>
    [BsonElement("saturatedFatGrams")]
    [BsonIgnoreIfNull]
    public decimal? SaturatedFatGrams { get; set; }

    /// <summary>
    /// Cholesterol in milligrams per serving
    /// </summary>
    [BsonElement("cholesterolMg")]
    [BsonIgnoreIfNull]
    public decimal? CholesterolMg { get; set; }

    /// <summary>
    /// Sodium in milligrams per serving
    /// </summary>
    [BsonElement("sodiumMg")]
    [BsonIgnoreIfNull]
    public decimal? SodiumMg { get; set; }

    /// <summary>
    /// Total carbohydrates in grams per serving
    /// </summary>
    [BsonElement("carbohydratesGrams")]
    [BsonIgnoreIfNull]
    public decimal? CarbohydratesGrams { get; set; }

    /// <summary>
    /// Dietary fiber in grams per serving
    /// </summary>
    [BsonElement("fiberGrams")]
    [BsonIgnoreIfNull]
    public decimal? FiberGrams { get; set; }

    /// <summary>
    /// Sugar in grams per serving
    /// </summary>
    [BsonElement("sugarGrams")]
    [BsonIgnoreIfNull]
    public decimal? SugarGrams { get; set; }

    /// <summary>
    /// Protein in grams per serving
    /// </summary>
    [BsonElement("proteinGrams")]
    [BsonIgnoreIfNull]
    public decimal? ProteinGrams { get; set; }

    /// <summary>
    /// Vitamin A percentage of daily value
    /// </summary>
    [BsonElement("vitaminAPercent")]
    [BsonIgnoreIfNull]
    public decimal? VitaminAPercent { get; set; }

    /// <summary>
    /// Vitamin C percentage of daily value
    /// </summary>
    [BsonElement("vitaminCPercent")]
    [BsonIgnoreIfNull]
    public decimal? VitaminCPercent { get; set; }

    /// <summary>
    /// Calcium percentage of daily value
    /// </summary>
    [BsonElement("calciumPercent")]
    [BsonIgnoreIfNull]
    public decimal? CalciumPercent { get; set; }

    /// <summary>
    /// Iron percentage of daily value
    /// </summary>
    [BsonElement("ironPercent")]
    [BsonIgnoreIfNull]
    public decimal? IronPercent { get; set; }

    /// <summary>
    /// Additional nutritional data as key-value pairs
    /// Supports provider-specific nutrition information
    /// </summary>
    [BsonElement("additionalNutrition")]
    [BsonIgnoreIfNull]
    public Dictionary<string, object>? AdditionalNutrition { get; set; }
}
