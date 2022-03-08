namespace FeatureFlags.Core.Logging;

/// <summary>
/// Logging helper methods
/// </summary>
public static class LoggingHelpers
{
	private static readonly EventId emptyEventId = new();

	/// <summary>
	/// Makes it easier to log a state object
	/// </summary>
	/// <param name="logger">Logger</param>
	/// <param name="level">Log level</param>
	/// <param name="message">Message</param>
	/// <param name="exception">Exception, if any</param>
	/// <param name="state">State, if any</param>
	/// <param name="args">Additional format arguments for message, if any</param>
	public static void Log(this ILogger logger, LogLevel level, string message, Exception? exception = null, object? state = null, params string[] args)
	{
		logger.Log<object?>(level, emptyEventId, state, exception, (state, exception) =>
		{
			return string.Format(message, args);
		});
	}

	/// <summary>
	/// Log critical with state
	/// </summary>
	/// <param name="logger">Logger</param>
	/// <param name="message">Message</param>
	/// <param name="exception">Exception, if any</param>
	/// <param name="state">State, if any</param>
	/// <param name="args">Additional format arguments for message, if any</param>
	public static void Critical(this ILogger logger, string message, Exception? exception = null, object? state = null, params string[] args)
	{
		Log(logger, LogLevel.Critical, message, exception, state, args);
	}

	/// <summary>
	/// Log error with state
	/// </summary>
	/// <param name="logger">Logger</param>
	/// <param name="message">Message</param>
	/// <param name="exception">Exception, if any</param>
	/// <param name="state">State, if any</param>
	/// <param name="args">Additional format arguments for message, if any</param>
	public static void Error(this ILogger logger, string message, Exception? exception = null, object? state = null, params string[] args)
	{
		Log(logger, LogLevel.Error, message, exception, state, args);
	}

	/// <summary>
	/// Log warning with state
	/// </summary>
	/// <param name="logger">Logger</param>
	/// <param name="message">Message</param>
	/// <param name="exception">Exception, if any</param>
	/// <param name="state">State, if any</param>
	/// <param name="args">Additional format arguments for message, if any</param>
	public static void Warning(this ILogger logger, string message, Exception? exception = null, object? state = null, params string[] args)
	{
		Log(logger, LogLevel.Warning, message, exception, state, args);
	}

	/// <summary>
	/// Log information with state
	/// </summary>
	/// <param name="logger">Logger</param>
	/// <param name="message">Message</param>
	/// <param name="exception">Exception, if any</param>
	/// <param name="state">State, if any</param>
	/// <param name="args">Additional format arguments for message, if any</param>
	public static void Information(this ILogger logger, string message, Exception? exception = null, object? state = null, params string[] args)
	{
		Log(logger, LogLevel.Information, message, exception, state, args);
	}

	/// <summary>
	/// Log debug with state
	/// </summary>
	/// <param name="logger">Logger</param>
	/// <param name="message">Message</param>
	/// <param name="exception">Exception, if any</param>
	/// <param name="state">State, if any</param>
	/// <param name="args">Additional format arguments for message, if any</param>
	public static void Debug(this ILogger logger, string message, Exception? exception = null, object? state = null, params string[] args)
	{
		Log(logger, LogLevel.Debug, message, exception, state, args);
	}

	/// <summary>
	/// Log trace with state
	/// </summary>
	/// <param name="logger">Logger</param>
	/// <param name="message">Message</param>
	/// <param name="exception">Exception, if any</param>
	/// <param name="state">State, if any</param>
	/// <param name="args">Additional format arguments for message, if any</param>
	public static void Trace(this ILogger logger, string message, Exception? exception = null, object? state = null, params string[] args)
	{
		Log(logger, LogLevel.Trace, message, exception, state, args);
	}
}
