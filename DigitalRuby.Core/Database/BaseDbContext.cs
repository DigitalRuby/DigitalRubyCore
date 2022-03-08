using Innofactor.EfCoreJsonValueConverter;

namespace DigitalRuby.Core.Database;

/// <summary>
/// Base class for db contexts
/// </summary>
public abstract class BaseDbContext : DbContext
{
	private readonly bool inMemory;

	private bool readOnly;

	internal void MakeReadOnly()
	{
		if (!readOnly)
		{
			readOnly = true;

			// turn off change tracking if not in-mem db
			if (!inMemory)
			{
				ClearChangeTracker();
				ChangeTracker.AutoDetectChangesEnabled = false;
				ChangeTracker.LazyLoadingEnabled = false;
				ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
			}
		}
	}

	internal void MakeReadWrite()
	{
		if (readOnly)
		{
			readOnly = false;

			// turn on change tracking if not in-mem db
			if (!inMemory)
			{
				ChangeTracker.AutoDetectChangesEnabled = true;
				ChangeTracker.LazyLoadingEnabled = true;
				ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
			}
		}
	}

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="options">Options</param>
	protected BaseDbContext(DbContextOptions options) : base(options)
	{
		string providerName = Database.ProviderName ?? string.Empty;
		inMemory = providerName.Contains("memory", StringComparison.OrdinalIgnoreCase) ||
			(providerName.Contains("sqlite", StringComparison.OrdinalIgnoreCase) &&
			(Database.GetConnectionString() ?? string.Empty).Contains(":memory:", StringComparison.OrdinalIgnoreCase));
	}

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// needed for unit tests
		if ((Database.ProviderName ?? string.Empty).Contains("sqlite", StringComparison.OrdinalIgnoreCase))
		{
			modelBuilder.AddJsonFields();
		}
	}

	/// <inheritdoc />
	public override void Dispose()
	{
		GC.SuppressFinalize(this);

		// restore full read/write functions in case this is a read-only context
		MakeReadWrite();

		// if not in mem db, dispose, in mem db is a singleton
		if (!inMemory)
		{
			// for pooled connections this context can be re-used
			base.Dispose();
		}
	}

	/// <inheritdoc />
	public override ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		if (!inMemory)
		{
			return base.DisposeAsync();
		}
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Begin a transaction
	/// </summary>
	/// <returns>Transaction</returns>
	/// <exception cref="InvalidOperationException">Db context is read-only</exception>
	public IDbContextTransaction BeginTransaction()
	{
		if (readOnly)
		{
			throw new InvalidOperationException("Cannot begin a transaction on a read-only database");
		}
		return Database.BeginTransaction();
	}

	/// <summary>
	/// Begin a transaction
	/// </summary>
	/// <param name="cancellationToken">Cancel token</param>
	/// <returns>Task</returns>
	/// <exception cref="InvalidOperationException">Db context is read-only</exception>
	public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
	{
		if (readOnly)
		{
			throw new InvalidOperationException("Cannot begin a transaction on a read-only database");
		}
		return Database.BeginTransactionAsync(cancellationToken);
	}

	/// <summary>
	/// Commit the current transaction. If there is no transaction, nothing happens.
	/// </summary>
	/// <exception cref="InvalidOperationException">Db context is read-only</exception>
	public void CommitTransaction()
	{
		if (readOnly)
		{
			throw new InvalidOperationException("Cannot commit a transaction on a read-only database");
		}
		if (Database.CurrentTransaction != null)
		{
			Database.CommitTransaction();
		}
	}

	/// <summary>
	/// Commit the current transaction (async). If there is no transaction, nothing happens.
	/// </summary>
	/// <exception cref="InvalidOperationException">Db context is read-only</exception>
	public Task CommitTransactionAsync()
	{
		if (readOnly)
		{
			throw new InvalidOperationException("Cannot commit a transaction on a read-only database");
		}
		if (Database.CurrentTransaction != null)
		{
			return Database.CommitTransactionAsync();
		}
		return Task.CompletedTask;
	}

	/// <summary>
	/// Rollback the current trasaction. If there is no transaction, nothing happens.
	/// </summary>
	/// <exception cref="InvalidOperationException">Db context is read-only</exception>
	public void RollbackTransaction()
	{
		if (readOnly)
		{
			throw new InvalidOperationException("Cannot roll-back a transaction on a read-only database");
		}
		if (Database.CurrentTransaction != null)
		{
			Database.RollbackTransaction();
		}
	}

	/// <summary>
	/// Rollback the current trasaction (async). If there is no transaction, nothing happens.
	/// </summary>
	/// <exception cref="InvalidOperationException">Db context is read-only</exception>
	public Task RollbackTransactionAsync()
	{
		if (readOnly)
		{
			throw new InvalidOperationException("Cannot roll-back a transaction on a read-only database");
		}
		if (Database.CurrentTransaction != null)
		{
			return Database.RollbackTransactionAsync();
		}
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public override int SaveChanges()
	{
		return SaveChangesAsync(true, default).ConfigureAwait(false).GetAwaiter().GetResult();
	}

	/// <inheritdoc/>
	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		return SaveChangesAsync(acceptAllChangesOnSuccess, default).ConfigureAwait(false).GetAwaiter().GetResult();
	}

	/// <inheritdoc/>
	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		return SaveChangesAsync(true, cancellationToken);
	}

	/// <inheritdoc/>
	public override Task<int> SaveChangesAsync(bool acceptAllOnSuccess, CancellationToken cancellationToken = default)
	{
		if (readOnly)
		{
			throw new InvalidOperationException("Cannot save changes in a read-only context");
		}
		return base.SaveChangesAsync(acceptAllOnSuccess, cancellationToken);
	}

	/// <summary>
	/// Clear all pending changes
	/// </summary>
	public void ClearChangeTracker()
	{
		ChangeTracker.Clear();
	}
}
