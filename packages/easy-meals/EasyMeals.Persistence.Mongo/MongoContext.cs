using System.Reflection;
using EasyMeals.Persistence.Mongo.Attributes;
using EasyMeals.Persistence.Mongo.Options;
using MongoDB.Driver;

namespace EasyMeals.Persistence.Mongo;

/// <summary>
///     MongoDB context implementation providing database and collection access.
/// </summary>
public sealed class MongoContext : IMongoContext
{
	private readonly IMongoClient _client;

	public MongoContext(IMongoClient client, MongoDbOptions options)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.DatabaseName))
			throw new ArgumentException("Database name must be provided in MongoDbOptions.", nameof(options));

		Database = _client.GetDatabase(options.DatabaseName);
	}

	/// <inheritdoc />
	public IMongoDatabase Database { get; }

	/// <inheritdoc />
	public IMongoCollection<T> GetCollection<T>(string? name = null) where T : class
	{
		string collectionName = name ?? GetCollectionName<T>();
		return Database.GetCollection<T>(collectionName);
	}

	/// <inheritdoc />
	public Task<IClientSessionHandle> StartSessionAsync(CancellationToken ct = default) => _client.StartSessionAsync(cancellationToken: ct);

	/// <summary>
	///     Gets the collection name for a type, checking BsonCollectionAttribute first,
	///     then falling back to pluralization convention.
	/// </summary>
	private static string GetCollectionName<T>()
	{
		var attribute = typeof(T).GetCustomAttribute<BsonCollectionAttribute>();
		if (attribute is not null)
			return attribute.CollectionName;

		string typeName = typeof(T).Name;

		return typeName.EndsWith("s", StringComparison.OrdinalIgnoreCase)
			? typeName.ToLowerInvariant()
			: $"{typeName.ToLowerInvariant()}s";
	}
}