namespace EasyMeals.Shared.Data.DependencyInjection;

/// <summary>
///     Defines the access permissions for repository registration
///     Follows the Principle of Least Privilege for data access control
/// </summary>
public enum RepositoryPermissions
{
    /// <summary>
    ///     Read-only access to the repository
    ///     Suitable for query-only bounded contexts
    /// </summary>
    Read = 1,

    /// <summary>
    ///     Full read and write access to the repository
    ///     Required for command and query bounded contexts
    /// </summary>
    ReadWrite = 2
}