using FeatureFlags.Core.Caching.Serialization;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

using Polly;
using Polly.Contrib.DuplicateRequestCollapser;
using Polly.Wrap;

namespace FeatureFlags.Core.Caching
{
	/// <summary>
	/// Managed cache interface. A managed cache aggregates multiple caches, such as memory and distributed cache (redis, etc.).
	/// </summary>
	public interface IManagedCache : IDisposable
	{
		/// <summary>
		/// Get or create an item from the cache.
		/// </summary>
		/// <typeparam name="T">Type of item</typeparam>
		/// <param name="key">Cache key</param>
		/// <param name="cacheTime">Cache time</param>
		/// <param name="factory">Factory method if no item is in the cache</param>
		/// <param name="cancelToken">Cancel token</param>
		/// <returns>Task of return of type T</returns>
		Task<T> GetOrCreateAsync<T>(string key, TimeSpan cacheTime, Func<CancellationToken, Task<T>> factory, CancellationToken cancelToken = default);

		/// <summary>
		/// Attempts to retrieve value of T by key.
		/// </summary>
		/// <typeparam name="T">Type of object to get</typeparam>
		/// <param name="key">Cache key</param>
		/// <param name="cancelToken">Cancel token</param>
		/// <returns>Result of null if nothing found with the key</returns>
		Task<T?> GetAsync<T>(string key, CancellationToken cancelToken = default);

		/// <summary>
		/// Sets value T by key.
		/// </summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="key">Cache key to set</param>
		/// <param name="obj">Object to set</param>
		/// <param name="cacheTime">Duration to cache object</param>
		/// <param name="cancelToken">Cancel token</param>
		/// <returns>Task</returns>
		Task SetAsync<T>(string key, T obj, TimeSpan cacheTime, CancellationToken cancelToken = default);

		/// <summary>
		/// Attempts to delete an entry of T type by key. If there is no key found, nothing happens.
		/// </summary>
		/// <typeparam name="T">The type object object to delete</typeparam>
		/// <param name="key">The key to delete</param>
		/// <returns>Task</returns>
		Task DeleteAsync<T>(string key, CancellationToken cancelToken = default);
	}

	/// <summary>
	/// Null managed cache, always executes factory
	/// </summary>
	public sealed class NullManagedCache : IManagedCache
	{
		/// <inheritdoc />
		public void Dispose() { }

		/// <inheritdoc />
		public Task DeleteAsync<T>(string key, CancellationToken cancelToken = default) => Task.CompletedTask;

		/// <inheritdoc />
		public Task<T?> GetAsync<T>(string key, CancellationToken cancelToken = default) => Task.FromResult<T?>(default);

		/// <inheritdoc />
		public Task<T> GetOrCreateAsync<T>(string key, TimeSpan cacheTime, Func<CancellationToken, Task<T>> factory, CancellationToken cancelToken = default) =>
			factory(cancelToken);

		/// <inheritdoc />
		public Task SetAsync<T>(string key, T obj, TimeSpan cacheTime, CancellationToken cancelToken = default) => Task.CompletedTask;
	}

	/// <summary>
	/// Cache implementation
	/// </summary>
	[Binding(ServiceLifetime.Singleton)]
	public sealed class ManagedCache : AsyncPolicy, IManagedCache, IKeyStrategy, IDisposable, IHostedService
	{
		private static readonly TimeSpan defaultCacheTime = TimeSpan.FromMinutes(5.0);

		private readonly ISerializer serializer = new JsonLZ4Serializer(); //hardcoded for now, we can do fancy type & serializer specification stuff later.
		private readonly IMemoryCache memoryCache;
		private readonly FeatureFlags.Core.Caching.IDistributedCache distributedCache;
		private readonly ILogger logger;
		private readonly AsyncPolicyWrap cachePolicy;
		private readonly AsyncPolicy distributedCacheCircuitBreakPolicy;

		private bool running = true;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="memoryCache">Memory cache</param>
		/// <param name="distributedCache">Distributed cache</param>
		/// <param name="logger">Logger</param>
		public ManagedCache(IMemoryCache memoryCache, FeatureFlags.Core.Caching.IDistributedCache distributedCache, ILogger<ManagedCache> logger)
		{
			this.memoryCache = memoryCache;
			this.distributedCache = distributedCache;
			this.logger = logger;

			// create collapser, this will ensure keys do not cache storm
			var collapser = AsyncRequestCollapserPolicy.Create(this);

			// wrap this class (the cache policy) behind the collapser policy
			this.cachePolicy = PolicyWrap.WrapAsync(collapser, this);

			// circuit break if distributed cache goes down, re-enable circuit attempts after 5 seconds
			distributedCacheCircuitBreakPolicy = Policy.Handle<Exception>().CircuitBreakerAsync(5, TimeSpan.FromSeconds(5.0));

			this.distributedCache.KeyChanged += DistributedCacheKeyChanged;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			distributedCache.KeyChanged -= DistributedCacheKeyChanged;
			running = false;
		}

		/// <inheritdoc />
		public async Task<T?> GetAsync<T>(string key, CancellationToken cancelToken = default)
		{
			ValidateType<T>();

			// L1 lookup
			var result = memoryCache.Get<T>(key);
			if (result is not null)
			{
				return result;
			}

			DistributedCacheItem distributedCacheItem = default;
			try
			{
				// L2 lookup
				distributedCacheItem = await distributedCacheCircuitBreakPolicy.ExecuteAsync(() => distributedCache.GetAsync(key, cancelToken));
			}
			catch (Exception ex)
			{
				logger.Error($"Distributed cache error on {nameof(GetAsync)}", ex, state: new { Key = key, Type = typeof(T) });
			}

			// not found from distributed cache, give up
			if (!distributedCacheItem.HasValue)
			{
				return default;
			}

			// deserialize and return value from distributed cache
			var deserializedResult = DeserializeObject<T>(distributedCacheItem.Bytes);
			return deserializedResult;
		}

		/// <inheritdoc />
		public async Task SetAsync<T>(string key, T obj, TimeSpan cacheTime, CancellationToken cancelToken = default)
		{
			ValidateType<T>();

			key = FormatKey<T>(key);
			memoryCache.Set(key, obj, cacheTime);
			var distributedCacheBytes = SerializeObject(obj);
			try
			{
				await distributedCacheCircuitBreakPolicy.ExecuteAsync(() => distributedCache.SetAsync(key, new DistributedCacheItem { Bytes = distributedCacheBytes, Expiry = cacheTime }));
			}
			catch (Exception ex)
			{
				// don't fail the call, we can stomach redis being down
				logger.Error($"Distributed cache error on {nameof(SetAsync)}", ex, state: new { Key = key, Type = typeof(T).FullName });
			}
		}

		/// <inheritdoc />
		public Task DeleteAsync<T>(string key, CancellationToken cancelToken = default)
		{
			ValidateType<T>();

			key = FormatKey<T>(key);
			memoryCache.Remove(key);

			// note- unlike SetAsync, we don't catch the exception here, a deletion that fails in distributed cache is bad and we need it to propagate all the way out
			return distributedCacheCircuitBreakPolicy.ExecuteAsync(() => distributedCache.DeleteAsync(key, cancelToken));
		}

		/// <inheritdoc />
		public Task<T> GetOrCreateAsync<T>(string key, TimeSpan cacheTime, Func<CancellationToken, Task<T>> factory, CancellationToken cancelToken = default)
		{
			ValidateType<T>();

			key = FormatKey<T>(key);
			var pollyContext = new Context(key, new Dictionary<string, object> { { "CacheTime", cacheTime } });
			return cachePolicy.ExecuteAsync((ctx, cancelToken) => factory(cancelToken), pollyContext, cancelToken);
		}

		/// <summary>
		/// The polly policy implementation to GetOrCreateAsync a cache item
		/// </summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="factory">Factory method</param>
		/// <param name="context">Context</param>
		/// <param name="cancellationToken">Cancel token</param>
		/// <param name="continueOnCapturedContext">Whether to continue on captured context</param>
		/// <returns>Task of return value of T</returns>
		protected override Task<T> ImplementationAsync<T>(Func<Context, CancellationToken, Task<T>> factory,
			Context context,
			CancellationToken cancellationToken,
			bool continueOnCapturedContext)
		{
			// get the cache key
			string key = context.OperationKey;
			logger.Debug($"Get or create {key}");

			return memoryCache.GetOrCreateAsync<T>(key, async entry =>
			{
				logger.Debug($"Get or create {key} not in memory cache");

				if (!context.TryGetValue("CacheTime", out object? value) || value is not TimeSpan cacheTime)
				{
					cacheTime = defaultCacheTime;
				}
				entry.Size = 128; // TODO: Make this correct?

				try
				{
					// attempt to grab from distributed cache
					DistributedCacheItem distributedCacheItem = await distributedCacheCircuitBreakPolicy.ExecuteAsync(() => distributedCache.GetAsync(key, cancellationToken));
					if (distributedCacheItem.HasValue)
					{
						logger.Debug($"Get or create {key} in distributed cache");

						// grabbed from distributed cache, use that value and don't invoke the factory
						entry.AbsoluteExpirationRelativeToNow = distributedCacheItem.Expiry;
						return DeserializeObject<T>(distributedCacheItem.Bytes);
					}
					else
					{
						logger.Debug($"Get or create {key} not in distributed cache");
					}
				}
				catch (Exception ex)
				{
					// eat error but log it, we don't want serializer or redis to fail the entire call
					logger.Error($"Distributed cache read error on {nameof(GetOrCreateAsync)}", ex, state: new { Key = key, Type = typeof(T).FullName });
				}

				// get the item from the factory
				var item = await factory(context, cancellationToken);

				// add to distributed cache
				var distributedCacheBytes = SerializeObject<T>(item);

				try
				{
					await distributedCacheCircuitBreakPolicy.ExecuteAsync(() => distributedCache.SetAsync(key, new DistributedCacheItem { Bytes = distributedCacheBytes, Expiry = cacheTime }));
				}
				catch (Exception ex)
				{
					// don't fail the call, we can stomach redis being down
					logger.Error($"Distributed cache write error on {nameof(GetOrCreateAsync)}", ex, state: new { Key = key, Type = typeof(T).FullName });
				}

				return item;
			});
		}

		private void DistributedCacheKeyChanged(string key)
		{
			logger.Debug("Distributed cache key changed: " + key);
			memoryCache.Remove(key);
		}

		/// <inheritdoc />
		string IKeyStrategy.GetKey(Context context)
		{
			// get the key to collapse on
			return context.OperationKey;
		}

		private T DeserializeObject<T>(byte[] bytes)
		{
			return serializer.Deserialize<T>(bytes);
		}

		private byte[] SerializeObject<T>(in T obj)
		{
			return serializer.Serialize(obj);
		}

		private string FormatKey<T>(string key)
		{
			return $"{serializer.TypeString}-{typeof(T).FullName}-{key}";
		}

		private async Task MemoryCompactionTask(CancellationToken stoppingToken)
		{
			const double maxMemory = 1024 * 1024 * 512; // 512 gb max memory hard-coded for now, we start compacting as we go over this
			MemoryCache? memoryCacheImpl = memoryCache as MemoryCache;
			while (!stoppingToken.IsCancellationRequested && running)
			{
				try
				{
					long managedHeap = GC.GetTotalMemory(false);

					// if we hit our memory limit, start compacting by half
					if (managedHeap > maxMemory)
					{
						memoryCacheImpl?.Compact(0.5);
						GC.Collect();
						logger.Debug($"Compacted cache by half due to memory pressure. Max ram = {maxMemory}, gc heap = {managedHeap}.");
					}
				}
				catch (Exception ex)
				{
					logger.Error("Error compacting memory cache", ex);
				}
				await Task.Delay(10000, stoppingToken);
			}
		}

		private static void ValidateType<T>()
		{
			Type t = typeof(T);
			if (t.IsInterface)
			{
				throw new InvalidOperationException("Interfaces cannot be cached");
			}
			else if (t.IsPrimitive)
			{
				throw new InvalidOperationException("Primitives cannot be cached");
			}
		}

		/// <inheritdoc />
		Task IHostedService.StartAsync(CancellationToken cancellationToken)
		{
			// kick off memory compaction in background
			MemoryCompactionTask(cancellationToken).GetAwaiter();
			return Task.CompletedTask;
		}

		/// <inheritdoc />
		Task IHostedService.StopAsync(CancellationToken cancellationToken)
		{
			running = false;
			return Task.CompletedTask;
		}
	}
}
