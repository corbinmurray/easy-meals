using System.ComponentModel.DataAnnotations;

namespace EasyMeals.Shared.Data.Configuration;

/// <summary>
///     MongoDB configuration options for EasyMeals applications
///     Shared kernel for consistent MongoDB configuration across the solution
///     Follows Microsoft Options Pattern for strongly-typed configuration
/// </summary>
public sealed class MongoDbOptions
{
	/// <summary>
	///     Configuration section name for binding
	/// </summary>
	public const string SectionName = "MongoDB";

	/// <summary>
	///     MongoDB connection string
	///     Supports standard MongoDB connection string format with all driver options
	/// </summary>
	[Required]
	[MinLength(1)]
	public string ConnectionString { get; set; } = "mongodb://localhost:27017";

	/// <summary>
	///     Database name for the EasyMeals application
	///     Single database with multiple collections for different bounded contexts
	/// </summary>
	[Required]
	[MinLength(1)]
	public string DatabaseName { get; set; } = "easymeals";

	/// <summary>
	///     Application name for MongoDB driver identification
	///     Useful for monitoring and connection tracking
	/// </summary>
	public string ApplicationName { get; set; } = "EasyMeals";

	/// <summary>
	///     Connection timeout in seconds
	///     Default optimized for most workloads
	/// </summary>
	[Range(1, 300)]
	public int ConnectionTimeoutSeconds { get; set; } = 30;

	/// <summary>
	///     Socket timeout in seconds for MongoDB operations
	///     Can be overridden per application based on workload characteristics
	/// </summary>
	[Range(1, 600)]
	public int SocketTimeoutSeconds { get; set; } = 60;

	/// <summary>
	///     Maximum connection pool size
	///     Can be tuned per application based on concurrency requirements
	/// </summary>
	[Range(1, 1000)]
	public int MaxConnectionPoolSize { get; set; } = 100;

	/// <summary>
	///     Minimum connection pool size
	///     Maintains a baseline of ready connections
	/// </summary>
	[Range(0, 100)]
	public int MinConnectionPoolSize { get; set; } = 0;

	/// <summary>
	///     Server selection timeout in seconds
	///     Time to wait for server selection in replica set scenarios
	/// </summary>
	[Range(1, 120)]
	public int ServerSelectionTimeoutSeconds { get; set; } = 30;

	/// <summary>
	///     Enable retry writes for transient failures
	///     Recommended for production workloads
	/// </summary>
	public bool RetryWrites { get; set; } = true;

	/// <summary>
	///     Enable retry reads for transient failures
	///     Recommended for production workloads
	/// </summary>
	public bool RetryReads { get; set; } = true;

	/// <summary>
	///     Read preference for MongoDB operations
	///     Options: Primary, Secondary, SecondaryPreferred, PrimaryPreferred, Nearest
	/// </summary>
	public string ReadPreference { get; set; } = "Primary";

	/// <summary>
	///     Enable health checks for the MongoDB connection
	///     Essential for production monitoring
	/// </summary>
	public bool EnableHealthChecks { get; set; } = true;

	/// <summary>
	///     Health check tags for categorization
	///     Used by health check endpoints and monitoring
	/// </summary>
	public string[] HealthCheckTags { get; set; } = ["database", "mongodb"];

	/// <summary>
	///     Enable detailed logging for MongoDB operations
	///     Useful for development and troubleshooting
	/// </summary>
	public bool EnableDetailedLogging { get; set; } = false;

	/// <summary>
	///     Maximum idle time for connections in seconds
	///     Connections idle longer than this will be closed
	/// </summary>
	[Range(0, 3600)]
	public int MaxIdleTimeSeconds { get; set; } = 600; // 10 minutes

	/// <summary>
	///     Enable compression for network traffic
	///     Can improve performance over slower networks
	/// </summary>
	public bool EnableCompression { get; set; } = false;

	/// <summary>
	///     Compression algorithm to use when compression is enabled
	///     Options: zlib, snappy, zstd
	/// </summary>
	public string CompressionAlgorithm { get; set; } = "zlib";
}