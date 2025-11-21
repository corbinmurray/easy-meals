namespace EasyMeals.Persistence.Abstractions.Repositories;

public interface IUnitOfWork : IAsyncDisposable
{
	Task BeginTransactionAsync(CancellationToken cancellationToken = default);
	Task CommitAsync(CancellationToken cancellationToken = default);
	Task RollbackAsync(CancellationToken cancellationToken = default);
	bool HasActiveTransaction { get; }
}