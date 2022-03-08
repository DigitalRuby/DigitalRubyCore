using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

using System.IO;

#pragma warning disable IDE0037 // Use inferred member name

// Microsoft's json formatter:
// see https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/JsonConsoleFormatter.cs

namespace DigitalRuby.Core.Logging;

/// <summary>
/// Options for formatting logs to json console
/// </summary>
public class JsonConsoleFormatterOptions : ConsoleFormatterOptions
{
	/// <summary>
	/// Constructor
	/// </summary>
    public JsonConsoleFormatterOptions()
    {
        IncludeScopes = true;
    }
}

/// <summary>
/// Format log for json console
/// </summary>
public class JsonConsoleFormatter : ConsoleFormatter
{
	/// <summary>
	/// Formatter name
	/// </summary>
	public new const string Name = "custom_json";

	private static readonly JsonSerializerSettings logJsonSettings = new()
	{
		Culture = System.Globalization.CultureInfo.InvariantCulture,
		DefaultValueHandling = DefaultValueHandling.Ignore,
		PreserveReferencesHandling = PreserveReferencesHandling.None,
		ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
		Formatting = Formatting.None,
		MaxDepth = 5
	};

	private static readonly JsonSerializer logSerializer = JsonSerializer.Create(logJsonSettings);

	private readonly IHostEnvironment environment;

	private readonly bool writeScopes;
	private readonly bool utc;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="options">Options</param>
	/// <param name="env">Environment</param>
	public JsonConsoleFormatter(JsonConsoleFormatterOptions options, IHostEnvironment env) : base(Name)
	{
		writeScopes = options.IncludeScopes;
		utc = options.UseUtcTimestamp;
		environment = env;
	}

	/// <inheritdoc />
	public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
	{
		try
		{
			var msg = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
			if (string.IsNullOrWhiteSpace(msg))
			{
				return;
			}

			int pos = msg.IndexOf('{');
			if (pos >= 0)
			{
				msg = msg[..pos];
			}
			msg = msg.Trim();

			Dictionary<string, object> state = new();
			object stateObj = state;
			if (logEntry.State is System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> values)
			{
				foreach (var kv in values)
				{
					// aggregate state
					if (kv.Key == "state")
					{
						state.Clear();
						stateObj = kv.Value;
						break;
					}

					// copy over state
					state[kv.Key] = kv.Value;
				}
			}

			var obj = new
			{
				State = stateObj,
				Message = msg.Replace("\r", string.Empty).Replace("\n", " -- "),
				Timestamp = (utc ? DateTime.UtcNow : DateTime.Now),
				Exception = logEntry.Exception?.ToString()?.Replace("\r", string.Empty)?.Replace("\n", " -- "),
				Category = logEntry.Category,
				EventId = logEntry.EventId,
				Scopes = GetScopes(scopeProvider)
			};
			var writer = new MaxDepthJsonTextWriter(textWriter, logJsonSettings);
			logSerializer.Serialize(writer, obj);
			textWriter.WriteLine();
		}
		catch (Exception ex)
		{
			if (!environment.IsProduction())
			{
				Console.WriteLine("Skipped logging {0} because of error: {1}", logEntry.Category, ex.Message);
			}
		}
	}

    private object? GetScopes(IExternalScopeProvider scopeProvider)
	{
		if (!writeScopes)
		{
			return null;
		}

		List<object> scopes = new();
		scopeProvider.ForEachScope((scope, state) =>
		{
			Dictionary<string, object?> subScopes = new();
			subScopes["Message"] = scope?.ToString();
			if (scope is IEnumerable<KeyValuePair<string, object>> scopeItems)
			{
				foreach (KeyValuePair<string, object> item in scopeItems)
				{
					subScopes[item.Key] = item.Value;
				}
			}
			state.Add(subScopes);
		}, scopes);
		return scopes;
	}
}

#pragma warning restore IDE0037 // Use inferred member name