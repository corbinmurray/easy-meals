namespace EasyMeals.Persistence.Mongo.Options;

/// <summary>
///     Configuration options for MongoDB connection and behavior.
/// </summary>
public sealed class MongoDbOptions
{
    /// <summary>
    ///     Gets or sets the MongoDB connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    /// <summary>
    ///     Gets or sets the name of the database.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether to run MongoDB index creation on application startup.
    /// </summary>
    public bool RunMongoIndexesOnStartup { get; set; } = true;

    /// <summary>
    ///     Maximum number of connections allowed in the MongoDB connection pool.
    /// </summary>
    public int MaxConnectionPoolSize { get; set; } = 100;

    /// <summary>
    ///     Minimum number of connections maintained in the MongoDB connection pool.
    /// </summary>
    public int MinConnectionPoolSize { get; set; } = 10;

    /// <summary>
    ///     How long the driver waits to select a server before timing out.
    /// </summary>
    public TimeSpan ServerSelectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     How long the driver waits when establishing a connection before timing out.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Gets the configuration section name.
    /// </summary>
    public static string SectionName => "MongoDB";
}