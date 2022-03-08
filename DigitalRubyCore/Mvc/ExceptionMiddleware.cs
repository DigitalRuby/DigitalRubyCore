using Microsoft.AspNetCore.Diagnostics;

namespace FeatureFlags.Core.Mvc;

/// <summary>
/// Exception middleware helper
/// </summary>
public static class ExceptionMiddleware
{
	private static readonly JsonSerializerSettings exceptionSerializerSettings = new()
	{
		StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
	};

	private class UnhandledException { }

	/// <summary>
	/// Add exception middleware to request pipeline
	/// </summary>
	/// <param name="app">Application</param>
	public static void UseUnhandledExceptionMiddleware(this IApplicationBuilder app)
	{
		// add exception middleware
		app.UseExceptionHandler(exceptionHandlerApp =>
		{
			// execute the middleware
			exceptionHandlerApp.Run(async context =>
			{
				var logger = context.RequestServices.GetRequiredService<ILogger<UnhandledException>>(); // can't use static class here...
				var env = context.RequestServices.GetRequiredService<IHostEnvironment>();

				// pull out the exception if we can
				string? errorMessage = null;
				context.Response.ContentType = "application/json";
				var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
				var exception = exceptionHandlerPathFeature?.Error;
				var exceptionName = exception?.GetType()?.Name;

				// use common exception names to set http response status codes
				if (exceptionName is not null)
				{
					if (exceptionName.Contains("notfound", StringComparison.OrdinalIgnoreCase))
					{
						context.Response.StatusCode = StatusCodes.Status404NotFound;
						errorMessage = "Not found";
					}
					else if (exceptionName.Contains("notauthorized", StringComparison.OrdinalIgnoreCase) ||
						exceptionName.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
						exceptionName.Contains("authorizationdenied", StringComparison.OrdinalIgnoreCase))
					{
						context.Response.StatusCode = StatusCodes.Status401Unauthorized;
						errorMessage = "Unauthorized";
					}
					else if (exceptionName.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
						exceptionName.Contains("accessdenied", StringComparison.OrdinalIgnoreCase))
					{
						context.Response.StatusCode = StatusCodes.Status403Forbidden;
						errorMessage = "Forbidden";
					}
					else if (exceptionName.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
						exceptionName.Contains("argument", StringComparison.OrdinalIgnoreCase) ||
						exceptionName.Contains("invalid", StringComparison.OrdinalIgnoreCase))
					{
						context.Response.StatusCode = StatusCodes.Status400BadRequest;
						errorMessage = "Validation failed";
					}
				}

				// default case
				if (errorMessage is null)
				{
					context.Response.StatusCode = StatusCodes.Status500InternalServerError;
					errorMessage = "Internal server error";
				}

				if (env.IsProduction())
				{
					// append just the error message, we don't want anything else leaking out
					errorMessage += ": " + exception?.Message ?? "Unknown Error";
				}
				else
				{
					// dev env, we can show the entire error
					errorMessage += ": " + exception?.ToString() ?? "Unknown Error";
				}

				// make sure it fits in json
				errorMessage = JsonConvert.SerializeObject(errorMessage, exceptionSerializerSettings);

				var ip = context.GetRemoteIPAddress();
				logger.Error("Unhandled exception", exception, new { IPAddress = ip?.ToString() });

				await context.Response.WriteAsync($"{{\"error\":true,\"message\":{errorMessage}}}");
			});
		});
	}
}

