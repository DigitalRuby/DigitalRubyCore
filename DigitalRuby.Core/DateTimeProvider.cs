namespace DigitalRuby.Core;

/// <summary>
/// Date/time provider
/// </summary>
public interface IDateTimeProvider
{
	/// <summary>
	/// Get current date/time
	/// </summary>
	DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Implementation of IDateTimeProvider
/// </summary>
[Binding(ServiceLifetime.Singleton)]
public class DateTimeProvider : IDateTimeProvider
{
	/// <inheritdoc />
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}