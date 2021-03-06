using StackExchange.Redis;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DigitalRuby.Core.Services.Caching;

/// <summary>
/// Distributed cache item
/// </summary>
public readonly struct DistributedCacheItem
{
	/// <summary>
	/// The item bytes or null if no item found
	/// </summary>
	public byte[]? Bytes { get; init; }

	/// <summary>
	/// The item expiration relative to now or null if none
	/// </summary>
	public TimeSpan? Expiry { get; init; }

	/// <summary>
	/// Whether there is an item
	/// </summary>
	[MemberNotNullWhen(true, nameof(Bytes))]
	[MemberNotNullWhen(true, nameof(Expiry))]
	public bool HasValue => Bytes is not null && Expiry is not null;
}

/// <summary>
/// Distributed cache interface
/// </summary>
public interface IDistributedCache
{
	/// <summary>
	/// Attempt to get an item from the cache
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="cancelToken">Cancel token</param>
	/// <returns>Task that returns the item</returns>
	Task<DistributedCacheItem> GetAsync(string key, CancellationToken cancelToken = default);

	/// <summary>
	/// Set an item in the cache
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="item">Item</param>
	/// <param name="cancelToken">Cancel token</param>
	/// <returns>Task</returns>
	Task SetAsync(string key, DistributedCacheItem item, CancellationToken cancelToken = default);

	/// <summary>
	/// Delete an item from the cache
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="cancelToken">Cancel token</param>
	/// <returns>Task</returns>
	Task DeleteAsync(string key, CancellationToken cancelToken = default);

	/// <summary>
	/// Key change event, get notified if a key changes outside of this machine
	/// </summary>
	event Action<string>? KeyChanged;

	/// <summary>
	/// Whether to publish key change events from this machine back to this machine, default is false
	/// </summary>
	bool PublishKeyChangedEventsBackToThisMachine { get; set; }
}

/// <summary>
/// Distributed redis cache
/// </summary>
[Binding(ServiceLifetime.Singleton)]
public class DistributedRedisCache : BackgroundService, IDistributedCache, IDistributedLockFactory
{
	private readonly IConnectionMultiplexer connectionMultiplexer;
	private readonly ILogger<DistributedRedisCache> logger;

	private ChannelMessageQueue? changeQueue;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="connectionMultiplexer">Connection multiplexer</param>
	/// <param name="logger">Logger</param>
	public DistributedRedisCache(IConnectionMultiplexer connectionMultiplexer, ILogger<DistributedRedisCache> logger)
	{
		this.connectionMultiplexer = connectionMultiplexer;
		this.logger = logger;
	}

	/// <inheritdoc />
	public Task DeleteAsync(string key, CancellationToken cancelToken = default)
	{
		return PerformOperation(async () =>
		{
			await connectionMultiplexer.GetDatabase().KeyDeleteAsync(key);
			await connectionMultiplexer.GetSubscriber().PublishAsync("key.changed", Environment.MachineName + "\t" + key);
			return true;
		});
	}

	/// <inheritdoc />
	public Task<DistributedCacheItem> GetAsync(string key, CancellationToken cancelToken = default)
	{
		return PerformOperation(async () =>
		{
			var item = await connectionMultiplexer.GetDatabase().StringGetWithExpiryAsync(key);
			if (item.Value.HasValue)
			{
				return new DistributedCacheItem { Bytes = item.Value, Expiry = item.Expiry };
			}
			return default;
		});
	}

	/// <inheritdoc />
	public Task SetAsync(string key, DistributedCacheItem item, CancellationToken cancelToken = default)
	{
		if (!item.HasValue)
		{
			throw new ArgumentException("Cannot add a null item or null expiration to redis cache, key: " + key);
		}

		return PerformOperation(async () =>
		{
			await connectionMultiplexer.GetDatabase().StringSetAsync(key, item.Bytes, expiry: item.Expiry);
			await connectionMultiplexer.GetSubscriber().PublishAsync("key.changed", Environment.MachineName + "\t" + key);
			return true;
		});
	}

	/// <inheritdoc />
	public event Action<string>? KeyChanged;

	/// <inheritdoc />
	public bool PublishKeyChangedEventsBackToThisMachine { get; set; }

	private async Task<T?> PerformOperation<T>(Func<Task<T>> operation)
	{
		T? returnValue = default;
		await PerformOperationInternal(async () => returnValue = await operation());
		return returnValue;
	}

	private async Task PerformOperationInternal(Func<Task> operation)
	{
		try
		{
			await operation();
		}
		catch (RedisCommandException ex)
		{
			if (ex.Message.Contains("replica", StringComparison.OrdinalIgnoreCase))
			{
				logger.Error("Command failure on replica, re-init connection multiplexer and trying again...", ex);
				connectionMultiplexer.Configure();
				RegisterChangeQueue();
				await operation();
				return;
			}

			// some other error, fail
			throw;
		}
	}

	private void RegisterChangeQueue()
	{
		try
		{
			var queue = changeQueue;
			changeQueue = null;
			queue?.Unsubscribe();
			queue = connectionMultiplexer.GetSubscriber().Subscribe("key.changed");
			queue.OnMessage(msg =>
			{
				string[] pieces = msg.Message.ToString().Split('\t');
				if (pieces.Length == 2 && (PublishKeyChangedEventsBackToThisMachine || pieces[0] != Environment.MachineName))
				{
					KeyChanged?.Invoke(msg.Message);
				}
			});
			changeQueue = queue;
		}
		catch (Exception ex)
		{
			logger.Error("Error registering change queue", ex);
		}
	}

	private class DistributedLock : IAsyncDisposable
	{
		private readonly IConnectionMultiplexer connection;
		private readonly string lockKey;
		private readonly string lockToken;

		public DistributedLock(IConnectionMultiplexer connection, string lockKey, string lockToken)
		{
			this.connection = connection;
			this.lockKey = lockKey;
			this.lockToken = lockToken;
		}

		public async ValueTask DisposeAsync()
		{
			await connection.GetDatabase().LockReleaseAsync(lockKey, lockToken);
		}
	}

	private static readonly TimeSpan distributedLockSleepTime = TimeSpan.FromMilliseconds(100.0);

	/// <inheritdoc />
	public async Task<IAsyncDisposable?> TryAcquireLockAsync(string key, TimeSpan lockTime, TimeSpan timeout = default)
	{
		var db = connectionMultiplexer.GetDatabase();
		Stopwatch timer = Stopwatch.StartNew();
		string lockKey = "DistributedLock_" + key;
		string lockToken = Guid.NewGuid().ToString("N");

		do
		{
			if (await db.LockTakeAsync(lockKey, lockToken, lockTime))
			{
				return new DistributedLock(connectionMultiplexer, lockKey, lockToken);
			}
			if (timeout > distributedLockSleepTime)
			{
				await Task.Delay(distributedLockSleepTime);
			}
		}
		while (timer.Elapsed < timeout);

		return null;
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			// make sure pub/sub is up
			if (changeQueue is null)
			{
				RegisterChangeQueue();
			}
			await Task.Delay(10000, stoppingToken);
		}
	}
}

/// <summary>
/// Interface for distributed locks
/// </summary>
public interface IDistributedLockFactory
{
	/// <summary>
	/// Attempt to acquire a distributed lock
	/// </summary>
	/// <param name="key">Lock key</param>
	/// <param name="lockTime">Duration to acquire the lock before it auto-expires</param>
	/// <param name="timeout">Time out to acquire the lock or default to only make one attempt to acquire the lock</param>
	/// <returns>The lock or null if the lock could not be acquired</returns>
	Task<IAsyncDisposable?> TryAcquireLockAsync(string key, TimeSpan lockTime, TimeSpan timeout = default);
}
