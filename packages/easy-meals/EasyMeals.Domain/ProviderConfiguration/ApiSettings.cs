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
public sealed record ApiSettings
{
    /// <summary>API endpoint URL for recipe data.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Authentication method (None, ApiKey, Bearer, Basic).</summary>
    public AuthMethod AuthMethod { get; init; } = AuthMethod.None;

    /// <summary>
    /// Custom headers to include with API requests.
    /// For sensitive values (API keys, tokens), store secret references only.
    /// Example: "X-Api-Key": "secret:provider-apikey"
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Query parameter name for page size (pagination).</summary>
    public string? PageSizeParam { get; init; }

    /// <summary>Query parameter name for page number (pagination).</summary>
    public string? PageNumberParam { get; init; }

    /// <summary>Default page size for paginated requests.</summary>
    public int DefaultPageSize { get; init; } = 20;

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
