namespace FeatureFlags.Core.RateLimiting
{
	/// <summary>
	/// Interface for rate limiting
	/// </summary>
	public interface IRateLimitService
	{
		/// <summary>
		/// Rate limit
		/// </summary>
		/// <param name="request">Rate limit request</param>
		/// <param name="cancelToken">Cancel token</param>
		/// <returns>Task of response</returns>
		Task<RateLimitResponse> RateLimitAsync(RateLimitRequest request, CancellationToken cancelToken);

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
	public class RateLimitService : IRateLimitService
	{
		private readonly IRateLimitRepository rateLimitRepository;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="rateLimitRepository">Rate limit repository</param>
		public RateLimitService(IRateLimitRepository rateLimitRepository)
		{
			this.rateLimitRepository = rateLimitRepository;
		}

		/// <inheritdoc />
		public Task<RateLimitResponse> RateLimitAsync(RateLimitRequest request, CancellationToken cancelToken)
		{
			return rateLimitRepository.RateLimitAsync(request, cancelToken);
		}

		/// <inheritdoc />
		public Task ClearRateLimitAsync(RateLimitRequest request, CancellationToken cancelToken)
		{
			return rateLimitRepository.ClearRateLimitAsync(request, cancelToken);
		}
	}
}
