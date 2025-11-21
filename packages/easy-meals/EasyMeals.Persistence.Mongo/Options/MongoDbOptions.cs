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
    ///     Gets the configuration section name.
    /// </summary>
    public static string SectionName => "MongoDB";
}