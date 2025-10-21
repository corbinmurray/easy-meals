namespace EasyMeals.RecipeEngine.Application.Options;

public sealed class SiteOptions
{
	public string Name { get; set; } = string.Empty;
	public SiteStrategy ProcessingStrategy { get; set; } = new();
}

public sealed class SiteStrategy
{
	public SourceType SourceType { get; set; } = SourceType.Web;
	public bool IsDynamicContent { get; set; } = false;
	public string ResourceUrl { get; set; } = string.Empty;
	public ResiliencyConfig Resiliency { get; set; } = new();
	public ProcessingConfig Processing { get; set; } = new();
	public RateLimitConfig RateLimit { get; set; } = new();
}

public enum SourceType
{
	Web,
	Api
}

public sealed class ResiliencyConfig
{
	public int MaxRetryAttempts { get; set; } = 3;
	public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
	public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(1);
	public bool UseExponentialBackoff { get; set; } = true;
	public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(5);
	public int CircuitBreakerFailureThreshold { get; set; } = 5;
}

public sealed class ProcessingConfig
{
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
	public int MaxConcurrentRequests { get; set; } = 10;
	public bool EnableCaching { get; set; } = true;
	public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(1);
	public string UserAgent { get; set; } = "EasyMeals-RecipeHarvester/1.0";
	public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

public sealed class RateLimitConfig
{
	public int RequestsPerMinute { get; set; } = 60;
	public TimeSpan BurstWindow { get; set; } = TimeSpan.FromSeconds(10);
	public int BurstLimit { get; set; } = 10;
}