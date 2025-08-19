namespace EasyMeals.Shared.Data.Documents;

/// <summary>
/// Interface for documents that support audit trails
/// Follows DDD patterns for tracking entity lifecycle events
/// </summary>
public interface IAuditableDocument
{
    /// <summary>
    /// When the document was first created
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the document was last updated
    /// </summary>
    DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Updates the UpdatedAt timestamp
    /// Called automatically before save operations
    /// </summary>
    void MarkAsModified();
}
