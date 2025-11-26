namespace EasyMeals.Domain.ProviderConfiguration;

public sealed class ExtractionSelectors
{
    // Minimal initial shape — will be expanded during Phase 2
    public string Title { get; init; }
    public string[] Ingredients { get; init; } = Array.Empty<string>();
    public string[] Instructions { get; init; } = Array.Empty<string>();

    public ExtractionSelectors(string title)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
    }
}
