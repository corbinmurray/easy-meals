namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
///     Base interface for all domain events
///     Domain events represent business-significant occurrences within aggregates
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    ///     Unique identifier for the event
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    ///     Timestamp when the event occurred
    /// </summary>
    DateTime OccurredOn { get; }

    /// <summary>
    ///     Version of the event for schema evolution
    /// </summary>
    int Version { get; }
}

/// <summary>
///     Base implementation of domain events with common properties
/// </summary>
public abstract record BaseDomainEvent : IDomainEvent
{
    protected BaseDomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        Version = 1;
    }

    public Guid EventId { get; init; }
    public DateTime OccurredOn { get; init; }
    public virtual int Version { get; init; }
}