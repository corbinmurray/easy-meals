namespace EasyMeals.RecipeEngine.Domain.Events;

/// <summary>
///     Domain events for SagaState
/// </summary>
public record SagaStateCreatedEvent(Guid SagaId, string SagaType, Guid CorrelationId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}

public record SagaStateStartedEvent(Guid SagaId, Guid CorrelationId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}

public record SagaProgressUpdatedEvent(Guid SagaId, Guid CorrelationId, string Phase, int Progress) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}

public record SagaCheckpointCreatedEvent(Guid SagaId, Guid CorrelationId, string CheckpointName) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}

public record SagaStatePausedEvent(Guid SagaId, Guid CorrelationId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}

public record SagaStateResumedEvent(Guid SagaId, Guid CorrelationId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}

public record SagaStateCompletedEvent(Guid SagaId, Guid CorrelationId, TimeSpan? TotalExecutionTime) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}

public record SagaStateFailedEvent(Guid SagaId, Guid CorrelationId, string ErrorMessage) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}