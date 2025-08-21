namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Cooking instruction step in a recipe
/// </summary>
/// <param name="StepNumber">Step number in the cooking process</param>
/// <param name="Description">Detailed instruction text for this step</param>
/// <param name="TimeMinutes">Estimated time for this step in minutes</param>
/// <param name="Temperature">Temperature setting if applicable (e.g., "350°F", "medium heat")</param>
/// <param name="Equipment">Equipment needed for this step (e.g., "large skillet", "oven")</param>
/// <param name="MediaUrl">URL to instructional image or video for this step</param>
/// <param name="Tips">Additional tips or notes for this step</param>
public record Instruction(
	int StepNumber,
	string Description,
	int? TimeMinutes,
	string? Temperature,
	string? Equipment,
	string? MediaUrl,
	string? Tips);