namespace EasyMeals.Shared.Data.Attributes;

/// <summary>
///     Attribute to specify the MongoDB collection name for a document type
///     Provides explicit collection naming control following MongoDB conventions
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class BsonCollectionAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the BsonCollectionAttribute
    /// </summary>
    /// <param name="collectionName">The MongoDB collection name</param>
    public BsonCollectionAttribute(string collectionName) =>
        CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));

    /// <summary>
    ///     The name of the MongoDB collection
    /// </summary>
    public string CollectionName { get; }
}