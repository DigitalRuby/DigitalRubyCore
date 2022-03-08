using DigitalRuby.Core.Networking;
using DigitalRuby.Core.RateLimiting;

using Microsoft.AspNetCore.Mvc.Filters;

using System.Text.RegularExpressions;

namespace DigitalRuby.Core.Mvc;

/// <summary>
/// Rate limit attribute
/// </summary>
public class RateLimitAttribute : ActionFilterAttribute
{
	private static readonly RateLimitWindow defaultWindow = new()
	{
		Id = 1,
		Entries = new[]
		{
			new RateLimitWindowEntry
			{
				Attempts = 5,
				Seconds = 60
			}
		}
	};
	private static readonly Regex defaultRegex = new("[235][0-9][0-9]", RegexOptions.Compiled);

	private readonly RateLimitWindow window;
	private readonly Regex statusCodesToIgnoreRegex;

	/// <summary>
	/// Default constructor
	/// </summary>
	public RateLimitAttribute() : this(null, null) { }

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="window">Window or null for default of 5 requests / minute</param>
	/// <param name="statusCodexToIgnoreRegex">Regex of http status codes to ignore, default is to not rate limit 2xx, 3xx, 5xx</param>
	public RateLimitAttribute(RateLimitWindow? window, string? statusCodexToIgnoreRegex = null)
	{
		this.window = window ?? defaultWindow;
		this.statusCodesToIgnoreRegex = string.IsNullOrWhiteSpace(statusCodexToIgnoreRegex) ? defaultRegex : new Regex(statusCodexToIgnoreRegex);
	}

	/// <inheritdoc />
	public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		try
		{
			var ip = context.HttpContext.GetRemoteIPAddress();
			bool isInternal = ip.IsInternal();

			if (!isInternal)
			{
				var key = "RateLimit_" + context.HttpContext.Request.Path + "_" + ip!.ToString();
				var rateLimiter = context.HttpContext.RequestServices.GetRequiredService<IRateLimitService>();
				var request = new RateLimitRequest(key, window);
				var response = await rateLimiter.RateLimitAsync(request, default);
				if (response.OverLimit)
				{
					context.HttpContext.Response.StatusCode = 429;
					context.HttpContext.Response.Headers["Content-Type"] = "application/json";
					await context.HttpContext.Response.WriteAsync("{\"error\":true,\"message\":\"Too many requests, try again later\"}");
					await context.HttpContext.Response.CompleteAsync();
					return;
				}
			}
		}
		catch
		{
			// rate limiter should not bring down the end point
		}

		await base.OnActionExecutionAsync(context, next);
	}

	/// <inheritdoc />
	public override void OnActionExecuted(ActionExecutedContext context)
	{
		var ip = context.HttpContext.GetRemoteIPAddress();
		bool isInternal = ip.IsInternal();

		if (!isInternal)
		{
			try
			{
				var key = "RateLimit_" + context.HttpContext.Request.Path + "_" + ip!.ToString();

				// if no exception and status code was one that should not be rate limited, clear the rate limit for the key
				if (context.Exception is null && statusCodesToIgnoreRegex.IsMatch(context.HttpContext.Response.StatusCode.ToString()))
				{
					var rateLimiter = context.HttpContext.RequestServices.GetRequiredService<IRateLimitService>();

					// clear rate limit in background
					rateLimiter.ClearRateLimitAsync(new RateLimitRequest(key, window), default).GetAwaiter();
				}
			}
			catch
			{
				// rate limiter should not bring down the end point
			}
		}

		base.OnActionExecuted(context);
	}
}

/// <summary>
/// Rate limit attribute with a higher number of requests allowed
/// </summary>
public class RateLimitHighAttribute : RateLimitAttribute
{
	private static readonly RateLimitWindow defaultWindow = new()
	{
		Id = 2,
		Entries = new[]
		{
			new RateLimitWindowEntry
			{
				Attempts = 30,
				Seconds = 60
			},
			new RateLimitWindowEntry
			{
				Attempts = 100,
				Seconds = 180
			}
		}
	};

	/// <summary>
	/// Constructor
	/// </summary>
	public RateLimitHighAttribute() : base(defaultWindow) { }
}
