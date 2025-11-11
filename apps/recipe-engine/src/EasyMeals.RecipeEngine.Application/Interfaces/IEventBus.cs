using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using EasyMeals.RecipeEngine.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EasyMeals.RecipeEngine.Application.Interfaces;

public interface IEventBus
{
    IObservable<TEvent> Events<TEvent>() where TEvent : IDomainEvent;

    void Publish(IDomainEvent @event);

    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IDomainEvent;
}

public sealed class EasyMealsEventBus(ILogger<EasyMealsEventBus> logger) : IEventBus, IDisposable
{
    private readonly Subject<IDomainEvent> _events = new();
    private ImmutableList<IDisposable> _subscriptions = ImmutableList<IDisposable>.Empty;
    private bool _disposed;

    public IObservable<TEvent> Events<TEvent>() where TEvent : IDomainEvent
        => _events.OfType<TEvent>();

    public void Publish(IDomainEvent @event)
        => _events.OnNext(@event);

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IDomainEvent
    {
        IDisposable subscription = Events<TEvent>().Subscribe(async @event =>
        {
            try
            {
                await handler(@event);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error handling event {EventType}", typeof(TEvent).Name);
            }
        });

        ImmutableInterlocked.Update(ref _subscriptions, set => set.Add(subscription));

        return subscription;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (IDisposable subscription in _subscriptions)
        {
            subscription?.Dispose();
        }

        _ = _subscriptions?.Clear();
        _events?.Dispose();
    }
}