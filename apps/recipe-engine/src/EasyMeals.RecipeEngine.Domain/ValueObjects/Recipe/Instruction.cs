namespace EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

/// <summary>
///     Cooking instruction step value object with validation and business rules
///     Immutable value object following DDD principles
/// </summary>
public sealed record Instruction
{
    /// <summary>
    ///     Creates a new instruction step with validation
    /// </summary>
    /// <param name="stepNumber">Step number in the cooking process (must be positive)</param>
    /// <param name="description">Detailed instruction text for this step</param>
    /// <param name="timeMinutes">Estimated time for this step in minutes</param>
    /// <param name="temperature">Temperature setting if applicable (e.g., "350°F", "medium heat")</param>
    /// <param name="equipment">Equipment needed for this step (e.g., "large skillet", "oven")</param>
    /// <param name="mediaUrl">URL to instructional image or video for this step</param>
    /// <param name="tips">Additional tips or notes for this step</param>
    public Instruction(
        int stepNumber,
        string description,
        int? timeMinutes = null,
        string? temperature = null,
        string? equipment = null,
        string? mediaUrl = null,
        string? tips = null)
    {
        StepNumber = ValidateStepNumber(stepNumber);
        Description = ValidateDescription(description);
        TimeMinutes = ValidateTimeMinutes(timeMinutes);
        Temperature = string.IsNullOrWhiteSpace(temperature) ? null : temperature.Trim();
        Equipment = string.IsNullOrWhiteSpace(equipment) ? null : equipment.Trim();
        MediaUrl = ValidateMediaUrl(mediaUrl);
        Tips = string.IsNullOrWhiteSpace(tips) ? null : tips.Trim();
    }

    /// <summary>Step number in the cooking process</summary>
    public int StepNumber { get; init; }

    /// <summary>Detailed instruction text for this step</summary>
    public string Description { get; init; }

    /// <summary>Estimated time for this step in minutes</summary>
    public int? TimeMinutes { get; init; }

    /// <summary>Temperature setting if applicable</summary>
    public string? Temperature { get; init; }

    /// <summary>Equipment needed for this step</summary>
    public string? Equipment { get; init; }

    /// <summary>URL to instructional image or video for this step</summary>
    public string? MediaUrl { get; init; }

    /// <summary>Additional tips or notes for this step</summary>
    public string? Tips { get; init; }

    /// <summary>
    ///     Gets formatted display text for the instruction
    /// </summary>
    public string DisplayText
    {
        get
        {
            var text = $"{StepNumber}. {Description}";

            if (TimeMinutes.HasValue)
                text += $" ({TimeMinutes} min)";

            if (!string.IsNullOrEmpty(Temperature))
                text += $" at {Temperature}";

            return text;
        }
    }

    /// <summary>
    ///     Gets a summary for this instruction step
    /// </summary>
    public string Summary
    {
        get
        {
            string summary = Description.Length > 50
                ? Description[..47] + "..."
                : Description;

            if (TimeMinutes.HasValue)
                summary += $" ({TimeMinutes}min)";

            return summary;
        }
    }

    /// <summary>
    ///     Indicates if this step has timing information
    /// </summary>
    public bool HasTiming => TimeMinutes.HasValue;

    /// <summary>
    ///     Indicates if this step has temperature information
    /// </summary>
    public bool HasTemperature => !string.IsNullOrWhiteSpace(Temperature);

    /// <summary>
    ///     Indicates if this step requires specific equipment
    /// </summary>
    public bool RequiresEquipment => !string.IsNullOrWhiteSpace(Equipment);

    /// <summary>
    ///     Indicates if this step has visual/media guidance
    /// </summary>
    public bool HasMedia => !string.IsNullOrWhiteSpace(MediaUrl);

    /// <summary>
    ///     Indicates if this step has additional tips
    /// </summary>
    public bool HasTips => !string.IsNullOrWhiteSpace(Tips);

    /// <summary>
    ///     Indicates if this is a complex step (has multiple additional attributes)
    /// </summary>
    public bool IsComplexStep
    {
        get
        {
            var attributeCount = 0;
            if (HasTiming) attributeCount++;
            if (HasTemperature) attributeCount++;
            if (RequiresEquipment) attributeCount++;
            if (HasMedia) attributeCount++;
            if (HasTips) attributeCount++;

            return attributeCount >= 2;
        }
    }

    /// <summary>
    ///     Creates a copy of this instruction with new timing
    /// </summary>
    public Instruction WithTiming(int? timeMinutes) => this with { TimeMinutes = ValidateTimeMinutes(timeMinutes) };

    /// <summary>
    ///     Creates a copy of this instruction with new temperature
    /// </summary>
    public Instruction WithTemperature(string? temperature) =>
        this with { Temperature = string.IsNullOrWhiteSpace(temperature) ? null : temperature.Trim() };

    /// <summary>
    ///     Creates a copy of this instruction with new equipment
    /// </summary>
    public Instruction WithEquipment(string? equipment) => this with { Equipment = string.IsNullOrWhiteSpace(equipment) ? null : equipment.Trim() };

    /// <summary>
    ///     Creates a copy of this instruction with new tips
    /// </summary>
    public Instruction WithTips(string? tips) => this with { Tips = string.IsNullOrWhiteSpace(tips) ? null : tips.Trim() };

    /// <summary>
    ///     Common cooking temperatures for validation and assistance
    /// </summary>
    public static class CommonTemperatures
    {
        public static readonly Dictionary<string, string> OvenTemperatures = new()
        {
            { "low", "300°F (150°C)" },
            { "medium", "350°F (175°C)" },
            { "medium-high", "375°F (190°C)" },
            { "high", "425°F (220°C)" },
            { "very high", "450°F (230°C)" }
        };

        public static readonly Dictionary<string, string> StovetopTemperatures = new()
        {
            { "low heat", "Low flame/setting 2-3" },
            { "medium-low heat", "Medium-low flame/setting 3-4" },
            { "medium heat", "Medium flame/setting 5-6" },
            { "medium-high heat", "Medium-high flame/setting 7-8" },
            { "high heat", "High flame/setting 9-10" }
        };

        /// <summary>
        ///     Gets suggested temperature based on cooking method
        /// </summary>
        public static string? GetSuggestedTemperature(string cookingMethod)
        {
            return cookingMethod.ToLowerInvariant() switch
            {
                var method when method.Contains("bake") || method.Contains("roast") => "350°F (175°C)",
                var method when method.Contains("broil") => "High broil",
                var method when method.Contains("sauté") || method.Contains("fry") => "Medium-high heat",
                var method when method.Contains("simmer") => "Medium-low heat",
                var method when method.Contains("boil") => "High heat",
                _ => null
            };
        }
    }

    #region Validation Methods

    private static int ValidateStepNumber(int stepNumber)
    {
        if (stepNumber <= 0)
            throw new ArgumentException("Step number must be positive", nameof(stepNumber));

        if (stepNumber > 100)
            throw new ArgumentException("Step number cannot exceed 100", nameof(stepNumber));

        return stepNumber;
    }

    private static string ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Instruction description cannot be empty", nameof(description));

        if (description.Length > 1000)
            throw new ArgumentException("Instruction description cannot exceed 1000 characters", nameof(description));

        return description.Trim();
    }

    private static int? ValidateTimeMinutes(int? timeMinutes)
    {
        if (!timeMinutes.HasValue)
            return null;

        if (timeMinutes.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(timeMinutes), "Time cannot be negative");

        if (timeMinutes.Value > 480) // 8 hours
            throw new ArgumentOutOfRangeException(nameof(timeMinutes), "Single step time cannot exceed 8 hours");

        return timeMinutes.Value;
    }

    private static string? ValidateMediaUrl(string? mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
            return null;

        string trimmed = mediaUrl.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException("Media URL must be a valid URL", nameof(mediaUrl));

        // Ensure it's HTTP/HTTPS
        if (uri.Scheme != "http" && uri.Scheme != "https")
            throw new ArgumentException("Media URL must use HTTP or HTTPS protocol", nameof(mediaUrl));

        return trimmed;
    }

    #endregion
}