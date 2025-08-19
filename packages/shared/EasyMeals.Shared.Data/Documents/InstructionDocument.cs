using MongoDB.Bson.Serialization.Attributes;

namespace EasyMeals.Shared.Data.Documents;

/// <summary>
/// Embedded document representing a cooking instruction step
/// Supports rich instruction content with timing and media references
/// </summary>
public class InstructionDocument
{
    /// <summary>
    /// Step number in the cooking process
    /// </summary>
    [BsonElement("stepNumber")]
    public int StepNumber { get; set; }

    /// <summary>
    /// Detailed instruction text for this step
    /// </summary>
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Estimated time for this step in minutes
    /// </summary>
    [BsonElement("timeMinutes")]
    [BsonIgnoreIfNull]
    public int? TimeMinutes { get; set; }

    /// <summary>
    /// Temperature setting if applicable (e.g., "350°F", "medium heat")
    /// </summary>
    [BsonElement("temperature")]
    [BsonIgnoreIfNull]
    public string? Temperature { get; set; }

    /// <summary>
    /// Equipment needed for this step (e.g., "large skillet", "oven")
    /// </summary>
    [BsonElement("equipment")]
    [BsonIgnoreIfNull]
    public string? Equipment { get; set; }

    /// <summary>
    /// URL to instructional image or video for this step
    /// </summary>
    [BsonElement("mediaUrl")]
    [BsonIgnoreIfNull]
    public string? MediaUrl { get; set; }

    /// <summary>
    /// Additional tips or notes for this step
    /// </summary>
    [BsonElement("tips")]
    [BsonIgnoreIfNull]
    public string? Tips { get; set; }
}
