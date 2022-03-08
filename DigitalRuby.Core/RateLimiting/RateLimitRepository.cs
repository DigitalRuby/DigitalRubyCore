using StackExchange.Redis;

#pragma warning disable IDE0060 // Remove unused parameter

namespace DigitalRuby.Core.RateLimiting;

/// <summary>
/// Rate limit request
/// </summary>
/// <param name="Key">Key</param>
/// <param name="Window">Window</param>
public record RateLimitRequest(string Key, RateLimitWindow Window);

/// <summary>
/// Rate limit response
/// </summary>
/// <param name="Count">Total count</param>
/// <param name="WindowIndex">Current window index</param>
/// <param name="OverLimit">Whether rate limit is exceeded</param>
public record RateLimitResponse(long Count, int WindowIndex, bool OverLimit);

/// <summary>
/// Rate limit repository interface
/// </summary>
public interface IRateLimitRepository
{
	/// <summary>
	/// Perform rate limiting
	/// </summary>
	/// <param name="request">Rate limit request</param>
	/// <param name="cancelToken">Cancel token</param>
	/// <returns>Task of response</returns>
	public Task<RateLimitResponse> RateLimitAsync(RateLimitRequest request, CancellationToken cancelToken);

	/// <summary>
	/// Clear rate limit for a key
	/// </summary>
	/// <param name="request">Rate limit request</param>
	/// <param name="cancelToken">Cancel token</param>
	/// <returns>Task of response</returns>
	public Task ClearRateLimitAsync(RateLimitRequest request, CancellationToken cancelToken);
}

/// <inheritdoc />
[Binding(ServiceLifetime.Singleton)]
public class RateLimitRedisRepository : IRateLimitRepository
{
	private readonly IConnectionMultiplexer connection;
	private readonly IDateTimeProvider dateTimeProvider;
	private readonly ConcurrentDictionary<int, string> windowJsonCache = new();
	private readonly string scriptText;

	private LoadedLuaScript? rateLimitScript;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="connection">Redis connection</param>
	/// <param name="dateTimeProvider">Date/time provider</param>
	public RateLimitRedisRepository(IConnectionMultiplexer connection, IDateTimeProvider dateTimeProvider)
	{
		this.connection = connection;
		this.dateTimeProvider = dateTimeProvider;
		this.scriptText = @"
local limits = cjson.decode(ARGV[1])
local now = tonumber(ARGV[2])
local firstCount = 0
for i, limit in ipairs(limits) do
    local duration = limit[1]
    local bucket = ':' .. i
    local key = KEYS[1] .. bucket
    local count = redis.call('INCR', key)
    redis.call('EXPIRE', key, duration) -- set window to maximum
    local countNumber = tonumber(count)
    if countNumber > limit[2] then
        return {1, countNumber, i} -- over limit
    elseif firstCount == 0 then
        firstCount = countNumber
    end
end
return {0, firstCount, 0} -- under limit";
	}

	/// <inheritdoc />
	public Task<RateLimitResponse> RateLimitAsync(RateLimitRequest request, CancellationToken cancelToken)
	{
		return RateLimitRedisCacheAsync(request, cancelToken);
	}

	/// <inheritdoc />
	public async Task ClearRateLimitAsync(RateLimitRequest request, CancellationToken cancelToken)
	{
		var db = connection.GetDatabase();
		RedisKey[] keys = new RedisKey[request.Window.Entries.Length];
		for (int i = 0; i < request.Window.Entries.Length; i++)
		{
			keys[i] = request.Key + ":" + (i + 1).ToString(CultureInfo.InvariantCulture);
		}
		await db.ScriptEvaluateAsync("for k in KEYS do redis.call('del', k) end", keys);
	}

	private async Task LoadScriptAsync(CancellationToken cancelToken)
	{
		// recreate script, it gets nulled out periodically on errors
		if (rateLimitScript is null)
		{
			LuaScript luaScript = LuaScript.Prepare(scriptText);
			var server = GetServer();
			rateLimitScript = await server.ScriptLoadAsync(luaScript);
		}
	}

	private IServer GetServer()
	{
		var endpoints = connection.GetEndPoints();
		IServer? result = null;
		foreach (var endpoint in endpoints)
		{
			var server = connection.GetServer(endpoint);
			if (server.IsReplica || !server.IsConnected)
			{
				continue;
			}
			if (result != null)
			{
				throw new InvalidOperationException("Rate limiter requires exactly one master endpoint (found " + server.EndPoint + " and " + result.EndPoint + ")");
			}
			result = server;
		}
		if (result == null)
		{
			throw new InvalidOperationException("Requires exactly one master endpoint (found none)");
		}
		return result;
	}

	private async Task<RateLimitResponse> RateLimitRedisCacheAsync(RateLimitRequest request, CancellationToken cancelToken)
	{
		try
		{
			await LoadScriptAsync(cancelToken);
			var db = connection.GetDatabase();
			long now = dateTimeProvider.UtcNow.ToUnixTimeSeconds();

			// script to store count at list index 0 and original ttl at list index 1
			//  handling case where key does not exist and creating it with the initial ttl
			//  new keys will expire in 1 week, the max time to rate limit at
			string windowJson = windowJsonCache.GetOrAdd(request.Window.Id, id =>
			{
				List<int[]> windows = new();
				for (int i = 0; i < request.Window.Entries.Length; i++)
				{
					windows.Add(new[] { request.Window.Entries[i].Seconds, request.Window.Entries[i].Attempts });
				}
				return System.Text.Json.JsonSerializer.Serialize(windows);
			});

			RedisResult result = await db.ScriptEvaluateAsync(rateLimitScript!.Hash, new RedisKey[] { request.Key },
				new RedisValue[] { windowJson, now });
			RedisValue[] objs = (RedisValue[])result;
			if (objs[0] == 1)
			{
				// rate limit!
				return new RateLimitResponse((int)objs[1] - 1, (int)objs[2], true);
			}
			return new RateLimitResponse((int)objs[1], 0, false);
		}
		catch (RedisServerException ex)
		{
			// error from redis, null out the script so we can re-create it
			if (ex.Message.Contains("noscript", StringComparison.OrdinalIgnoreCase))
			{
				rateLimitScript = null;
			}
			throw;
		}
	}
}

#pragma warning restore IDE0060 // Remove unused parameter
