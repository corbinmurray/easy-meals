using System.Text.RegularExpressions;

namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Aggregate root representing a recipe provider's configuration settings.
/// Contains all settings needed for recipe discovery, fetching, and extraction.
/// </summary>
public sealed partial class ProviderConfiguration
{
    /// <summary>
    /// Unique identifier for the provider configuration (MongoDB ObjectId as string).
    /// </summary>
    public string Id { get; private set; } = string.Empty;

    /// <summary>
    /// Unique identifier for the provider (e.g., "hellofresh", "allrecipes").
    /// Immutable after creation; used as a business key.
    /// </summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>
    /// Human-friendly display name (e.g., "HelloFresh", "AllRecipes").
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Base URL for the provider's website (e.g., "https://www.hellofresh.com").
    /// </summary>
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this provider is currently active for recipe discovery.
    /// </summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Processing priority (higher = processed first when multiple providers match).
    /// </summary>
    public int Priority { get; private set; }

    /// <summary>
    /// How recipe URLs are discovered (API or Crawl).
    /// </summary>
    public DiscoveryStrategy DiscoveryStrategy { get; private set; }

    /// <summary>
    /// How recipe content is fetched (API, StaticHtml, or DynamicHtml).
    /// </summary>
    public FetchingStrategy FetchingStrategy { get; private set; }

    /// <summary>
    /// CSS selectors for extracting recipe properties from HTML.
    /// </summary>
    public ExtractionSelectors ExtractionSelectors { get; private set; } = null!;

    /// <summary>
    /// Rate limiting configuration for this provider.
    /// </summary>
    public RateLimitSettings RateLimitSettings { get; private set; } = null!;

    /// <summary>
    /// API-specific settings (required when DiscoveryStrategy or FetchingStrategy is Api).
    /// </summary>
    public ApiSettings? ApiSettings { get; private set; }

    /// <summary>
    /// Crawl-specific settings (required when DiscoveryStrategy is Crawl).
    /// </summary>
    public CrawlSettings? CrawlSettings { get; private set; }

    /// <summary>
    /// Concurrency token for optimistic concurrency control.
    /// </summary>
    public long ConcurrencyToken { get; private set; }

    /// <summary>
    /// When the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When the configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Whether the configuration has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// When the configuration was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    // Private constructor for ORM/factory use
    private ProviderConfiguration() { }

    /// <summary>
    /// Creates a new provider configuration with validation.
    /// </summary>
    /// <param name="providerName">Unique provider identifier (lowercase alphanumeric with hyphens).</param>
    /// <param name="displayName">Human-readable display name.</param>
    /// <param name="baseUrl">Provider's base URL.</param>
    /// <param name="discoveryStrategy">How to discover recipes.</param>
    /// <param name="fetchingStrategy">How to fetch recipe content.</param>
    /// <param name="extractionSelectors">CSS selectors for extraction.</param>
    /// <param name="rateLimitSettings">Rate limiting configuration.</param>
    /// <param name="priority">Processing priority (default 0).</param>
    /// <param name="apiSettings">API settings (required for Api strategy).</param>
    /// <param name="crawlSettings">Crawl settings (required for Crawl strategy).</param>
    /// <returns>A valid provider configuration.</returns>
    /// <exception cref="ArgumentException">If validation fails.</exception>
    public static ProviderConfiguration Create(
        string providerName,
        string displayName,
        string baseUrl,
        DiscoveryStrategy discoveryStrategy,
        FetchingStrategy fetchingStrategy,
        ExtractionSelectors extractionSelectors,
        RateLimitSettings rateLimitSettings,
        int priority = 0,
        ApiSettings? apiSettings = null,
        CrawlSettings? crawlSettings = null)
    {
        var config = new ProviderConfiguration
        {
            ProviderName = NormalizeProviderName(providerName),
            DisplayName = displayName,
            BaseUrl = baseUrl,
            DiscoveryStrategy = discoveryStrategy,
            FetchingStrategy = fetchingStrategy,
            ExtractionSelectors = extractionSelectors,
            RateLimitSettings = rateLimitSettings,
            Priority = priority,
            ApiSettings = apiSettings,
            CrawlSettings = crawlSettings,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var validation = config.Validate();
        if (!validation.IsValid)
            throw new ArgumentException(string.Join("; ", validation.Errors));

        return config;
    }

    /// <summary>
    /// Reconstructs a provider configuration from persistence (bypasses validation for existing data).
    /// </summary>
    internal static ProviderConfiguration Reconstitute(
        string id,
        string providerName,
        string displayName,
        string baseUrl,
        bool isEnabled,
        int priority,
        DiscoveryStrategy discoveryStrategy,
        FetchingStrategy fetchingStrategy,
        ExtractionSelectors extractionSelectors,
        RateLimitSettings rateLimitSettings,
        ApiSettings? apiSettings,
        CrawlSettings? crawlSettings,
        long concurrencyToken,
        DateTime createdAt,
        DateTime updatedAt,
        bool isDeleted,
        DateTime? deletedAt)
    {
        return new ProviderConfiguration
        {
            Id = id,
            ProviderName = providerName,
            DisplayName = displayName,
            BaseUrl = baseUrl,
            IsEnabled = isEnabled,
            Priority = priority,
            DiscoveryStrategy = discoveryStrategy,
            FetchingStrategy = fetchingStrategy,
            ExtractionSelectors = extractionSelectors,
            RateLimitSettings = rateLimitSettings,
            ApiSettings = apiSettings,
            CrawlSettings = crawlSettings,
            ConcurrencyToken = concurrencyToken,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt
        };
    }

    /// <summary>
    /// Enables the provider for recipe discovery.
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;
        MarkAsModified();
    }

    /// <summary>
    /// Disables the provider from recipe discovery without deleting it.
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        MarkAsModified();
    }

    /// <summary>
    /// Updates the CSS extraction selectors.
    /// </summary>
    /// <param name="selectors">The new extraction selectors.</param>
    /// <exception cref="ArgumentNullException">If selectors is null.</exception>
    /// <exception cref="ArgumentException">If any selector is invalid.</exception>
    public void UpdateSelectors(ExtractionSelectors selectors)
    {
        ArgumentNullException.ThrowIfNull(selectors);

        foreach (var selector in selectors.GetAllSelectors())
        {
            if (!CssSelectorValidator.IsValid(selector))
                throw new ArgumentException($"Invalid CSS selector: {selector}");
        }

        ExtractionSelectors = selectors;
        MarkAsModified();
    }

    /// <summary>
    /// Updates the rate limiting settings.
    /// </summary>
    /// <param name="settings">The new rate limit settings.</param>
    /// <exception cref="ArgumentNullException">If settings is null.</exception>
    /// <exception cref="ArgumentException">If settings are invalid.</exception>
    public void UpdateRateLimits(RateLimitSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.RequestsPerMinute <= 0)
            throw new ArgumentException("RequestsPerMinute must be greater than 0.");
        if (settings.MaxConcurrentRequests <= 0)
            throw new ArgumentException("MaxConcurrentRequests must be greater than 0.");

        RateLimitSettings = settings;
        MarkAsModified();
    }

    /// <summary>
    /// Validates the provider configuration.
    /// </summary>
    /// <returns>A validation result with any errors.</returns>
    public ProviderConfigurationValidationResult Validate()
    {
        var errors = new List<string>();

        // Provider name validation
        if (string.IsNullOrWhiteSpace(ProviderName))
            errors.Add("ProviderName is required.");
        else if (!ProviderNamePattern().IsMatch(ProviderName))
            errors.Add("ProviderName must be lowercase alphanumeric with hyphens only.");

        // Display name validation
        if (string.IsNullOrWhiteSpace(DisplayName))
            errors.Add("DisplayName is required.");

        // Base URL validation
        if (string.IsNullOrWhiteSpace(BaseUrl))
            errors.Add("BaseUrl is required.");
        else if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            errors.Add("BaseUrl must be a valid HTTP or HTTPS URL.");

        // Priority validation
        if (Priority < 0)
            errors.Add("Priority must be >= 0.");

        // Extraction selectors validation
        if (ExtractionSelectors is null)
            errors.Add("ExtractionSelectors is required.");
        else
        {
            foreach (var selector in ExtractionSelectors.GetAllSelectors())
            {
                if (!CssSelectorValidator.IsValid(selector))
                    errors.Add($"Invalid CSS selector: {selector}");
            }
        }

        // Rate limit settings validation
        if (RateLimitSettings is null)
            errors.Add("RateLimitSettings is required.");
        else
        {
            if (RateLimitSettings.RequestsPerMinute <= 0)
                errors.Add("RateLimitSettings.RequestsPerMinute must be > 0.");
            if (RateLimitSettings.MaxConcurrentRequests <= 0)
                errors.Add("RateLimitSettings.MaxConcurrentRequests must be > 0.");
        }

        // Strategy-specific validation
        if (DiscoveryStrategy == DiscoveryStrategy.Api || FetchingStrategy == FetchingStrategy.Api)
        {
            if (ApiSettings is null)
                errors.Add("ApiSettings is required when using Api strategy.");
            else
            {
                if (string.IsNullOrWhiteSpace(ApiSettings.Endpoint))
                    errors.Add("ApiSettings.Endpoint is required.");
                if (!ApiSettings.HasValidSecretReferences())
                    errors.Add("ApiSettings contains invalid secret references.");
            }
        }

        if (DiscoveryStrategy == DiscoveryStrategy.Crawl)
        {
            if (CrawlSettings is null)
                errors.Add("CrawlSettings is required when using Crawl discovery strategy.");
            else if (!CrawlSettings.HasValidSeedUrls())
                errors.Add("CrawlSettings.SeedUrls must contain valid URLs.");
        }

        return new ProviderConfigurationValidationResult(errors);
    }

    private void MarkAsModified()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeProviderName(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("ProviderName cannot be null or whitespace.", nameof(providerName));
        return providerName.ToLowerInvariant().Trim();
    }

    [GeneratedRegex(@"^[a-z0-9-]+$")]
    private static partial Regex ProviderNamePattern();
}

/// <summary>
/// Result of provider configuration validation.
/// </summary>
public sealed class ProviderConfigurationValidationResult
{
    /// <summary>Whether the configuration is valid.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>List of validation errors.</summary>
    public IReadOnlyList<string> Errors { get; }

    internal ProviderConfigurationValidationResult(List<string> errors)
    {
        Errors = errors.AsReadOnly();
    }
}

