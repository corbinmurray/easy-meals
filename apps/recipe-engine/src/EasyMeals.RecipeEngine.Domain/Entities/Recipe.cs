using EasyMeals.Platform;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Recipe aggregate root representing extracted recipe content
///     Part of the Content Acquisition bounded context
/// </summary>
public sealed class Recipe : AggregateRoot<Guid>
{
	private readonly List<Ingredient> _ingredients;
	private readonly List<Instruction> _instructions;
	private readonly List<string> _tags;

	/// <summary>
	///     Creates a new Recipe aggregate root
	/// </summary>
	/// <param name="id">Unique identifier for the recipe</param>
	/// <param name="title">Recipe title (required, max 200 characters)</param>
	/// <param name="description">Recipe description</param>
	/// <param name="sourceUrl">Original source URL (required)</param>
	/// <param name="providerName">Source provider name</param>
	/// <param name="sourceFingerprintId">ID of the fingerprint that created this recipe</param>
	public Recipe(
		Guid id,
		string title,
		string description,
		string sourceUrl,
		string providerName,
		Guid? sourceFingerprintId = null)
		: base(id)
	{
		Title = ValidateTitle(title);
		Description = description ?? string.Empty;
		SourceUrl = ValidateSourceUrl(sourceUrl);
		ProviderName = providerName ?? string.Empty;
		SourceFingerprintId = sourceFingerprintId;

		_ingredients = [];
		_instructions = [];
		_tags = [];

		Status = RecipeStatus.Draft;
	}

	// Private constructor for reconstitution from persistence
	private Recipe()
	{
		_ingredients = [];
		_instructions = [];
		_tags = [];
	}

	#region Properties

	/// <summary>Recipe title</summary>
	public string Title { get; private set; } = string.Empty;

	/// <summary>Recipe description</summary>
	public string Description { get; private set; } = string.Empty;

	/// <summary>Read-only view of ingredients</summary>
	public IReadOnlyList<Ingredient> Ingredients => _ingredients.AsReadOnly();

	/// <summary>Read-only view of instructions</summary>
	public IReadOnlyList<Instruction> Instructions => _instructions.AsReadOnly();

	/// <summary>Main recipe image URL</summary>
	public string ImageUrl { get; private set; } = string.Empty;

	/// <summary>Preparation time in minutes</summary>
	public int PrepTimeMinutes { get; private set; }

	/// <summary>Cooking time in minutes</summary>
	public int CookTimeMinutes { get; private set; }

	/// <summary>Number of servings</summary>
	public int Servings { get; private set; } = 1;

	/// <summary>Nutritional information</summary>
	public NutritionalInfo? NutritionalInfo { get; private set; }

	/// <summary>Read-only view of tags</summary>
	public IReadOnlyList<string> Tags => _tags.AsReadOnly();

	/// <summary>Original source URL</summary>
	public string SourceUrl { get; private set; } = string.Empty;

	/// <summary>Source provider name</summary>
	public string ProviderName { get; private set; } = string.Empty;

	/// <summary>Current lifecycle status of the recipe</summary>
	public RecipeStatus Status { get; private set; }

	/// <summary>Cuisine type (e.g., Italian, Mexican, Thai)</summary>
	public string? Cuisine { get; private set; }

	/// <summary>Difficulty level</summary>
	public string? Difficulty { get; private set; }

	/// <summary>ID of the fingerprint that created this recipe (audit trail)</summary>
	public Guid? SourceFingerprintId { get; private set; }

	/// <summary>When the source URL was last verified to be accessible</summary>
	public DateTime? SourceLastVerifiedAt { get; private set; }

	/// <summary>When the source content was last modified (if detectable)</summary>
	public DateTime? SourceLastModifiedAt { get; private set; }

	/// <summary>Recipe author as extracted from source</summary>
	public string? Author { get; private set; }

	/// <summary>Meal occasion this recipe is suited for</summary>
	public MealType? MealType { get; private set; }

	/// <summary>Course type within a meal</summary>
	public CourseType? CourseType { get; private set; }

	/// <summary>Dietary and allergen information</summary>
	public DietaryInfo? DietaryInfo { get; private set; }

	/// <summary>
	///     Hash of normalized recipe content for deduplication
	///     Computed from title + sorted ingredient names by the application layer
	/// </summary>
	public string? ContentHash { get; private set; }

	#endregion

	#region Computed Properties

	/// <summary>Total cooking time</summary>
	public int TotalTimeMinutes => PrepTimeMinutes + CookTimeMinutes;

	/// <summary>Whether recipe has complete nutritional data</summary>
	public bool HasNutritionInfo => NutritionalInfo?.IsComprehensive == true;

	/// <summary>Number of ingredients</summary>
	public int IngredientCount => _ingredients.Count;

	/// <summary>Number of instruction steps</summary>
	public int InstructionCount => _instructions.Count;

	/// <summary>Whether recipe is ready for publication</summary>
	public bool IsReadyForPublication =>
		!string.IsNullOrWhiteSpace(Title) &&
		_ingredients.Count > 0 &&
		_instructions.Count > 0 &&
		Servings > 0;

	/// <summary>Whether recipe is in a published state</summary>
	public bool IsPublished => Status == RecipeStatus.Published;

	/// <summary>Whether recipe can be edited (not archived)</summary>
	public bool IsEditable => Status != RecipeStatus.Archived;

	#endregion

	#region Business Methods

	/// <summary>
	///     Updates recipe basic information
	/// </summary>
	public void UpdateBasicInfo(string title, string description, int servings)
	{
		EnsureEditable();

		Title = ValidateTitle(title);
		Description = description ?? string.Empty;
		Servings = ValidateServings(servings);
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets recipe timing information
	/// </summary>
	public void SetTimingInfo(int prepTimeMinutes, int cookTimeMinutes)
	{
		EnsureEditable();

		PrepTimeMinutes = ValidateTime(prepTimeMinutes, nameof(prepTimeMinutes));
		CookTimeMinutes = ValidateTime(cookTimeMinutes, nameof(cookTimeMinutes));
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets recipe image URL
	/// </summary>
	public void SetImageUrl(string imageUrl)
	{
		EnsureEditable();

		ImageUrl = imageUrl ?? string.Empty;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Adds an ingredient to the recipe
	/// </summary>
	public void AddIngredient(Ingredient ingredient)
	{
		EnsureEditable();

		if (ingredient is null)
			throw new ArgumentNullException(nameof(ingredient));

		if (_ingredients.Any(i => i.Name.Equals(ingredient.Name, StringComparison.OrdinalIgnoreCase)))
			throw new InvalidOperationException($"Ingredient '{ingredient.Name}' already exists in recipe");

		_ingredients.Add(ingredient);
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Removes an ingredient by name
	/// </summary>
	public void RemoveIngredient(string ingredientName)
	{
		EnsureEditable();

		if (string.IsNullOrWhiteSpace(ingredientName))
			throw new ArgumentException("Ingredient name cannot be empty", nameof(ingredientName));

		Ingredient? ingredient = _ingredients.FirstOrDefault(i =>
			i.Name.Equals(ingredientName, StringComparison.OrdinalIgnoreCase));

		if (ingredient is null)
			throw new InvalidOperationException($"Ingredient '{ingredientName}' not found in recipe");

		_ingredients.Remove(ingredient);
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Adds an instruction step to the recipe
	/// </summary>
	public void AddInstruction(Instruction instruction)
	{
		EnsureEditable();

		if (instruction is null)
			throw new ArgumentNullException(nameof(instruction));

		if (instruction.StepNumber <= 0)
			throw new ArgumentException("Step number must be positive", nameof(instruction));

		if (_instructions.Any(i => i.StepNumber == instruction.StepNumber))
			throw new InvalidOperationException($"Step number {instruction.StepNumber} already exists");

		_instructions.Add(instruction);
		_instructions.Sort((a, b) => a.StepNumber.CompareTo(b.StepNumber));
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets nutritional information
	/// </summary>
	public void SetNutritionalInfo(NutritionalInfo nutritionalInfo)
	{
		EnsureEditable();

		NutritionalInfo = nutritionalInfo;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Adds a tag to the recipe
	/// </summary>
	public void AddTag(string tag)
	{
		EnsureEditable();

		if (string.IsNullOrWhiteSpace(tag))
			throw new ArgumentException("Tag cannot be empty", nameof(tag));

		string normalizedTag = tag.Trim().ToLowerInvariant();

		if (!_tags.Contains(normalizedTag))
		{
			_tags.Add(normalizedTag);
			UpdatedAt = DateTime.UtcNow;
		}
	}

	/// <summary>
	///     Sets cuisine type
	/// </summary>
	public void SetCuisine(string? cuisine)
	{
		EnsureEditable();

		Cuisine = string.IsNullOrWhiteSpace(cuisine) ? null : cuisine.Trim();
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets difficulty level
	/// </summary>
	public void SetDifficulty(string? difficulty)
	{
		EnsureEditable();

		if (!string.IsNullOrWhiteSpace(difficulty))
		{
			string[] validDifficulties = ["Easy", "Medium", "Hard"];
			if (!validDifficulties.Contains(difficulty))
				throw new ArgumentException("Difficulty must be Easy, Medium, or Hard", nameof(difficulty));
		}

		Difficulty = string.IsNullOrWhiteSpace(difficulty) ? null : difficulty;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets the recipe author as extracted from source
	/// </summary>
	public void SetAuthor(string? author)
	{
		EnsureEditable();

		Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim();
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets meal type classification
	/// </summary>
	public void SetMealType(MealType? mealType)
	{
		EnsureEditable();

		MealType = mealType;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets course type classification
	/// </summary>
	public void SetCourseType(CourseType? courseType)
	{
		EnsureEditable();

		CourseType = courseType;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets dietary and allergen information
	/// </summary>
	public void SetDietaryInfo(DietaryInfo? dietaryInfo)
	{
		EnsureEditable();

		DietaryInfo = dietaryInfo;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Records that the source URL was verified to be accessible
	/// </summary>
	public void MarkSourceVerified(DateTime? sourceLastModified = null)
	{
		SourceLastVerifiedAt = DateTime.UtcNow;
		SourceLastModifiedAt = sourceLastModified;
		UpdatedAt = DateTime.UtcNow;
	}

	#endregion

	#region Lifecycle Methods

	/// <summary>
	///     Sets the content hash for deduplication (computed by application layer)
	/// </summary>
	public void SetContentHash(string contentHash)
	{
		if (string.IsNullOrWhiteSpace(contentHash))
			throw new ArgumentException("Content hash cannot be empty", nameof(contentHash));

		ContentHash = contentHash;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Publishes the recipe, making it available in the shared collection
	/// </summary>
	public void Publish()
	{
		if (Status != RecipeStatus.Draft)
			throw new InvalidOperationException($"Cannot publish from status {Status}");

		if (!IsReadyForPublication)
			throw new InvalidOperationException("Recipe is not ready for publication. Ensure title, ingredients, instructions, and servings are set.");

		Status = RecipeStatus.Published;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Archives the recipe, removing it from active use
	/// </summary>
	public void Archive()
	{
		if (Status == RecipeStatus.Archived)
			throw new InvalidOperationException("Recipe is already archived");

		Status = RecipeStatus.Archived;
		UpdatedAt = DateTime.UtcNow;
	}

	#endregion

	#region Private Methods

	private void EnsureEditable()
	{
		if (!IsEditable)
			throw new InvalidOperationException($"Cannot modify recipe in {Status} status");
	}

	private static string ValidateTitle(string title)
	{
		if (string.IsNullOrWhiteSpace(title))
			throw new ArgumentException("Recipe title cannot be empty", nameof(title));

		if (title.Length > 200)
			throw new ArgumentException("Recipe title cannot exceed 200 characters", nameof(title));

		return title.Trim();
	}

	private static string ValidateSourceUrl(string sourceUrl)
	{
		if (string.IsNullOrWhiteSpace(sourceUrl))
			throw new ArgumentException("Source URL is required", nameof(sourceUrl));

		if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out _))
			throw new ArgumentException("Source URL must be a valid URL", nameof(sourceUrl));

		return sourceUrl;
	}

	private static int ValidateServings(int servings)
	{
		return servings switch
		{
			<= 0 => throw new ArgumentOutOfRangeException(nameof(servings), "Servings must be positive"),
			> 100 => throw new ArgumentOutOfRangeException(nameof(servings), "Servings cannot exceed 100"),
			_ => servings
		};
	}

	private static int ValidateTime(int timeMinutes, string parameterName)
	{
		return timeMinutes switch
		{
			< 0 => throw new ArgumentOutOfRangeException(parameterName, "Time cannot be negative"),
			> 1440 => throw new ArgumentOutOfRangeException(parameterName, "Time cannot exceed 24 hours"),
			_ => timeMinutes
		};
	}

	#endregion
}