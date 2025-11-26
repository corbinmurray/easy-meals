namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Authentication methods for API-based provider access.
/// </summary>
public enum AuthMethod
{
    /// <summary>No authentication required.</summary>
    None = 0,

    /// <summary>API key authentication via header or query parameter.</summary>
    ApiKey = 1,

    /// <summary>Bearer token authentication.</summary>
    Bearer = 2,

    /// <summary>Basic authentication with username and password.</summary>
    Basic = 3
}
