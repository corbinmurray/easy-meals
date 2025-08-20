namespace EasyMeals.Shared.Data.Configuration;

/// <summary>
///     Repository permission configuration for bounded context isolation
///     Implements the Principle of Least Privilege for data access
/// </summary>
public class RepositoryPermissions
{
	/// <summary>
	///     Collection of read-only repository registrations
	/// </summary>
	public HashSet<Type> ReadOnlyRepositories { get; } = new();

	/// <summary>
	///     Collection of read-write repository registrations
	/// </summary>
	public HashSet<Type> ReadWriteRepositories { get; } = new();

	/// <summary>
	///     Adds a repository with read-only permissions
	/// </summary>
	/// <typeparam name="TEntity">The entity type</typeparam>
	/// <returns>Permission configuration for chaining</returns>
	public RepositoryPermissions AddReadOnly<TEntity>() where TEntity : class
	{
		ReadOnlyRepositories.Add(typeof(TEntity));
		return this;
	}

	/// <summary>
	///     Adds a repository with read-write permissions
	/// </summary>
	/// <typeparam name="TEntity">The entity type</typeparam>
	/// <returns>Permission configuration for chaining</returns>
	public RepositoryPermissions AddReadWrite<TEntity>() where TEntity : class
	{
		ReadWriteRepositories.Add(typeof(TEntity));
		return this;
	}

	/// <summary>
	///     Adds Recipe repository with read-only permissions
	///     Common pattern for applications that only need to read recipe data
	/// </summary>
	/// <returns>Permission configuration for chaining</returns>
	public RepositoryPermissions AddRecipeReadOnly() => AddReadOnly<Entities.RecipeEntity>();

	/// <summary>
	///     Adds Recipe repository with read-write permissions
	///     For applications that need to create/update recipe data
	/// </summary>
	/// <returns>Permission configuration for chaining</returns>
	public RepositoryPermissions AddRecipeReadWrite() => AddReadWrite<Entities.RecipeEntity>();

	/// <summary>
	///     Adds CrawlState repository with read-write permissions
	///     Typically used by crawler applications
	/// </summary>
	/// <returns>Permission configuration for chaining</returns>
	public RepositoryPermissions AddCrawlStateReadWrite() => AddReadWrite<Entities.CrawlStateEntity>();
}