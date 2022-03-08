namespace FeatureFlags.Core.RateLimiting;

/// <summary>
/// Rate limit window
/// </summary>
public class RateLimitWindow
{
	/// <summary>
	/// Id of the window, default is 1
	/// </summary>
	public int Id { get; set; } = 1;

	/// <summary>
	/// Rate limit entries
	/// </summary>
	public RateLimitWindowEntry[] Entries { get; set; } = Array.Empty<RateLimitWindowEntry>();
}

/// <summary>
/// Rate limit window entry
/// </summary>
public class RateLimitWindowEntry
{
	/// <summary>
	/// Limnit to Attempts in Seconds time
	/// </summary>
	public int Attempts { get; set; }

	/// <summary>
	/// Limit to Attempts in Seconds time
	/// </summary>
	public int Seconds { get; set; }
}