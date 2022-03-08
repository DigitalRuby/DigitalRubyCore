namespace DigitalRuby.Core.Exceptions;

/// <summary>
/// Unauthorized exception
/// </summary>
public class UnauthorizedException : Exception
{
	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="message">Message</param>
	public UnauthorizedException(string message) : base(message) { }
}

