namespace EasyMeals.Domain.ProviderConfiguration;

/// <summary>
/// Settings for API-based recipe discovery and fetching.
/// </summary>
/// <remarks>
/// <para>
/// <strong>SECURITY NOTE</strong>: API keys and secrets MUST NOT be stored directly in the <see cref="Headers"/> dictionary.
/// Store a <strong>secret reference</strong> (e.g., <c>"secret:hellofresh-apikey"</c>) and resolve at runtime
/// from a secure secret store (Azure Key Vault, AWS Secrets Manager, or environment variables for development).
/// </para>
/// </remarks>
public sealed class ApiSettings
{
    /// <summary>API endpoint URL for recipe data.</summary>
    public string Endpoint { get; private set; }

    /// <summary>Authentication method (None, ApiKey, Bearer, Basic).</summary>
    public AuthMethod AuthMethod { get; private set; }

    /// <summary>
    /// Custom headers to include with API requests.
    /// For sensitive values (API keys, tokens), store secret references only.
    /// Example: "X-Api-Key": "secret:provider-apikey"
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; private set; }

    /// <summary>Query parameter name for page size (pagination).</summary>
    public string? PageSizeParam { get; private set; }

    /// <summary>Query parameter name for page number (pagination).</summary>
    public string? PageNumberParam { get; private set; }

    /// <summary>Default page size for paginated requests.</summary>
    public int DefaultPageSize { get; private set; }

    /// <summary>
    /// Creates a new instance of ApiSettings.
    /// </summary>
    /// <param name="endpoint">API endpoint URL for recipe data.</param>
    /// <param name="authMethod">Authentication method (default: None).</param>
    /// <param name="headers">Custom headers to include with requests.</param>
    /// <param name="pageSizeParam">Query parameter name for page size.</param>
    /// <param name="pageNumberParam">Query parameter name for page number.</param>
    /// <param name="defaultPageSize">Default page size (default: 20).</param>
    public ApiSettings(
        string endpoint,
        AuthMethod authMethod = AuthMethod.None,
        IReadOnlyDictionary<string, string>? headers = null,
        string? pageSizeParam = null,
        string? pageNumberParam = null,
        int defaultPageSize = 20)
    {
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        AuthMethod = authMethod;
        Headers = headers ?? new Dictionary<string, string>();
        PageSizeParam = pageSizeParam;
        PageNumberParam = pageNumberParam;
        DefaultPageSize = defaultPageSize > 0 ? defaultPageSize : throw new ArgumentOutOfRangeException(nameof(defaultPageSize), "Must be greater than 0.");
    }

    /// <summary>
    /// Validates that any secret references in headers follow the expected pattern.
    /// </summary>
    /// <returns>True if all secret references are valid; otherwise, false.</returns>
    public bool HasValidSecretReferences()
    {
        foreach (var value in Headers.Values)
        {
            if (value.StartsWith("secret:", StringComparison.OrdinalIgnoreCase))
            {
                var secretName = value[7..];
                if (string.IsNullOrWhiteSpace(secretName))
                    return false;
            }
        }
        return true;
    }
}
