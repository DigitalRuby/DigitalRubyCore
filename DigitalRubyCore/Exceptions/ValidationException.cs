namespace FeatureFlags.Core.Exceptions;

/// <summary>
/// Validation exception
/// </summary>
public class ValidationException : Exception
{
	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="message">Message</param>
	public ValidationException(string message) : base(message) { }
}

