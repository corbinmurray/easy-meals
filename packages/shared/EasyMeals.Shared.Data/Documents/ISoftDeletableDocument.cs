namespace EasyMeals.Shared.Data.Documents;

/// <summary>
/// Interface for documents that support soft deletion
/// Implements soft delete pattern from DDD for data retention requirements
/// </summary>
public interface ISoftDeletableDocument
{
    /// <summary>
    /// Indicates if the document has been soft deleted
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// When the document was soft deleted (null if not deleted)
    /// </summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Soft deletes the document
    /// Maintains data integrity while supporting business requirements for data retention
    /// </summary>
    void SoftDelete();

    /// <summary>
    /// Restores a soft deleted document
    /// Supports business scenarios where deletion needs to be reversed
    /// </summary>
    void Restore();
}
