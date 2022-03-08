namespace DigitalRuby.Core.Exceptions;

/// <summary>
/// Not found exception
/// </summary>
public class NotFoundException : Exception
{
	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="message">Message</param>
	public NotFoundException(string message = "") : base(message) { }
}

