using EasyMeals.Persistence.Abstractions.Repositories;
using MongoDB.Driver;

namespace EasyMeals.Persistence.Mongo;

/// <summary>
///     Marker interface for MongoDB Unit of Work implementations.
/// </summary>
public interface IMongoUnitOfWork : IUnitOfWork
{
}

/// <summary>
///     MongoDB implementation of the Unit of Work pattern for transaction management.
/// </summary>
public sealed class MongoUnitOfWork(IMongoContext context) : IMongoUnitOfWork
{
	private readonly IMongoContext _context = context ?? throw new ArgumentNullException(nameof(context));
	private bool _disposed;

	/// <inheritdoc />
	public bool HasActiveTransaction => Session?.IsInTransaction ?? false;

	/// <summary>
	///     Gets the current session handle, if any.
	/// </summary>
	public IClientSessionHandle? Session { get; private set; }

	/// <inheritdoc />
	public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (Session != null) throw new InvalidOperationException("A transaction is already active.");

		Session = await _context.StartSessionAsync(cancellationToken);
		Session.StartTransaction();
	}

	/// <inheritdoc />
	public async Task CommitAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (Session?.IsInTransaction != true) throw new InvalidOperationException("No active transaction to commit.");

		await Session.CommitTransactionAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task RollbackAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (Session?.IsInTransaction == true) await Session.AbortTransactionAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;

		if (Session != null)
		{
			// Rollback any uncommitted transaction
			if (Session.IsInTransaction) await Session.AbortTransactionAsync();

			Session.Dispose();
			Session = null;
		}

		_disposed = true;
	}
}