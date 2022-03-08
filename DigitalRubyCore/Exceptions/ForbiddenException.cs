namespace FeatureFlags.Core.Exceptions;

/// <summary>
/// Forbidden exception
/// </summary>
public class ForbiddenException : Exception
{
	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="message">Message</param>
	public ForbiddenException(string message) : base(message) { }
}

