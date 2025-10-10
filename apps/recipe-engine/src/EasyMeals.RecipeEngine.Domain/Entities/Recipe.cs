using EasyMeals.RecipeEngine.Domain.Events;
using EasyMeals.RecipeEngine.Domain.ValueObjects.Recipe;

namespace EasyMeals.RecipeEngine.Domain.Entities;

/// <summary>
///     Recipe aggregate root that encapsulates business rules and maintains consistency
///     Follows DDD principles with rich domain behavior and proper encapsulation
/// </summary>
public sealed class Recipe
{
	private readonly List<Ingredient> _ingredients;
	private readonly List<Instruction> _instructions;
	private readonly List<string> _tags;
	private readonly List<IDomainEvent> _domainEvents;

	/// <summary>
	///     Creates a new Recipe aggregate root
	/// </summary>
	/// <param name="id">Unique identifier for the recipe</param>
	/// <param name="title">Recipe title (required, max 200 characters)</param>
	/// <param name="description">Recipe description</param>
	/// <param name="sourceUrl">Original source URL (required)</param>
	/// <param name="sourceProvider">Source provider name</param>
	public Recipe(
		Guid id,
		string title,
		string description,
		string sourceUrl,
		string sourceProvider)
	{
		Id = id;
		Title = ValidateTitle(title);
		Description = description ?? string.Empty;
		SourceUrl = ValidateSourceUrl(sourceUrl);
		SourceProvider = sourceProvider ?? string.Empty;

		_ingredients = new List<Ingredient>();
		_instructions = new List<Instruction>();
		_tags = new List<string>();
		_domainEvents = new List<IDomainEvent>();

		CreatedAt = DateTime.UtcNow;
		UpdatedAt = DateTime.UtcNow;
		IsActive = true;

		// Domain event: Recipe created
		AddDomainEvent(new RecipeCreatedEvent(this));
	}

	// Private constructor for reconstitution from persistence
	private Recipe()
	{
		_ingredients = new List<Ingredient>();
		_instructions = new List<Instruction>();
		_tags = new List<string>();
		_domainEvents = new List<IDomainEvent>();
	}

	#region Properties

	/// <summary>Unique identifier</summary>
	public Guid Id { get; private set; }

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
	public string SourceProvider { get; private set; } = string.Empty;

	/// <summary>Whether recipe is active/published</summary>
	public bool IsActive { get; private set; }

	/// <summary>Cuisine type</summary>
	public string? Cuisine { get; private set; }

	/// <summary>Difficulty level</summary>
	public string? Difficulty { get; private set; }

	/// <summary>Recipe rating (1-5 stars)</summary>
	public decimal? Rating { get; private set; }

	/// <summary>Number of reviews</summary>
	public int ReviewCount { get; private set; }

	/// <summary>Creation timestamp</summary>
	public DateTime CreatedAt { get; private set; }

	/// <summary>Last update timestamp</summary>
	public DateTime UpdatedAt { get; private set; }

	/// <summary>Read-only view of domain events</summary>
	public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

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

	#endregion

	#region Business Methods

	/// <summary>
	///     Updates recipe basic information
	/// </summary>
	public void UpdateBasicInfo(string title, string description, int servings)
	{
		string oldTitle = Title;

		Title = ValidateTitle(title);
		Description = description ?? string.Empty;
		Servings = ValidateServings(servings);
		UpdatedAt = DateTime.UtcNow;

		if (oldTitle != Title)
		{
			AddDomainEvent(new RecipeTitleChangedEvent(Id, oldTitle, Title));
		}

		AddDomainEvent(new RecipeUpdatedEvent(Id));
	}

	/// <summary>
	///     Sets recipe timing information
	/// </summary>
	public void SetTimingInfo(int prepTimeMinutes, int cookTimeMinutes)
	{
		PrepTimeMinutes = ValidateTime(prepTimeMinutes, nameof(prepTimeMinutes));
		CookTimeMinutes = ValidateTime(cookTimeMinutes, nameof(cookTimeMinutes));
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new RecipeUpdatedEvent(Id));
	}

	/// <summary>
	///     Sets recipe image URL
	/// </summary>
	public void SetImageUrl(string imageUrl)
	{
		ImageUrl = imageUrl ?? string.Empty;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Adds an ingredient to the recipe
	/// </summary>
	public void AddIngredient(Ingredient ingredient)
	{
		if (ingredient == null)
			throw new ArgumentNullException(nameof(ingredient));

		if (_ingredients.Any(i => i.Name.Equals(ingredient.Name, StringComparison.OrdinalIgnoreCase)))
			throw new InvalidOperationException($"Ingredient '{ingredient.Name}' already exists in recipe");

		_ingredients.Add(ingredient);
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new IngredientAddedEvent(Id, ingredient));
	}

	/// <summary>
	///     Removes an ingredient by name
	/// </summary>
	public void RemoveIngredient(string ingredientName)
	{
		if (string.IsNullOrWhiteSpace(ingredientName))
			throw new ArgumentException("Ingredient name cannot be empty", nameof(ingredientName));

		Ingredient? ingredient = _ingredients.FirstOrDefault(i =>
			i.Name.Equals(ingredientName, StringComparison.OrdinalIgnoreCase));

		if (ingredient == null)
			throw new InvalidOperationException($"Ingredient '{ingredientName}' not found in recipe");

		_ingredients.Remove(ingredient);
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new IngredientRemovedEvent(Id, ingredient));
	}

	/// <summary>
	///     Adds an instruction step to the recipe
	/// </summary>
	public void AddInstruction(Instruction instruction)
	{
		if (instruction == null)
			throw new ArgumentNullException(nameof(instruction));

		// Ensure step numbers are sequential
		if (instruction.StepNumber <= 0)
			throw new ArgumentException("Step number must be positive", nameof(instruction));

		if (_instructions.Any(i => i.StepNumber == instruction.StepNumber))
			throw new InvalidOperationException($"Step number {instruction.StepNumber} already exists");

		_instructions.Add(instruction);
		_instructions.Sort((a, b) => a.StepNumber.CompareTo(b.StepNumber));
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new InstructionAddedEvent(Id, instruction));
	}

	/// <summary>
	///     Sets nutritional information
	/// </summary>
	public void SetNutritionalInfo(NutritionalInfo nutritionalInfo)
	{
		NutritionalInfo = nutritionalInfo;
		UpdatedAt = DateTime.UtcNow;

		if (nutritionalInfo?.IsComprehensive == true)
		{
			AddDomainEvent(new NutritionalInfoCompletedEvent(Id));
		}
	}

	/// <summary>
	///     Adds a tag to the recipe
	/// </summary>
	public void AddTag(string tag)
	{
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
	public void SetCuisine(string cuisine)
	{
		Cuisine = string.IsNullOrWhiteSpace(cuisine) ? null : cuisine.Trim();
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Sets difficulty level
	/// </summary>
	public void SetDifficulty(string difficulty)
	{
		if (!string.IsNullOrWhiteSpace(difficulty))
		{
			var validDifficulties = new[] { "Easy", "Medium", "Hard" };
			if (!validDifficulties.Contains(difficulty))
				throw new ArgumentException("Difficulty must be Easy, Medium, or Hard", nameof(difficulty));
		}

		Difficulty = string.IsNullOrWhiteSpace(difficulty) ? null : difficulty;
		UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>
	///     Updates recipe rating
	/// </summary>
	public void UpdateRating(decimal newRating)
	{
		if (newRating < 1 || newRating > 5)
			throw new ArgumentOutOfRangeException(nameof(newRating), "Rating must be between 1 and 5");

		decimal? oldRating = Rating;
		Rating = Math.Round(newRating, 1);
		ReviewCount++;
		UpdatedAt = DateTime.UtcNow;

		AddDomainEvent(new RecipeRatedEvent(Id, oldRating, Rating.Value, ReviewCount));
	}

	/// <summary>
	///     Clears all domain events (typically called after persistence)
	/// </summary>
	public void ClearDomainEvents()
	{
		_domainEvents.Clear();
	}

	#endregion

	#region Private Methods

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
		if (servings <= 0)
			throw new ArgumentOutOfRangeException(nameof(servings), "Servings must be positive");

		if (servings > 100)
			throw new ArgumentOutOfRangeException(nameof(servings), "Servings cannot exceed 100");

		return servings;
	}

	private static int ValidateTime(int timeMinutes, string parameterName)
	{
		if (timeMinutes < 0)
			throw new ArgumentOutOfRangeException(parameterName, "Time cannot be negative");

		if (timeMinutes > 1440) // 24 hours
			throw new ArgumentOutOfRangeException(parameterName, "Time cannot exceed 24 hours");

		return timeMinutes;
	}

	private void AddDomainEvent(IDomainEvent domainEvent)
	{
		_domainEvents.Add(domainEvent);
	}

	#endregion
}