using Microsoft.AspNetCore.Authentication;

namespace DigitalRuby.Core.Services.Authentication;

/// <summary>
/// Authorizes via api key header
/// </summary>
public class ApiKeyAuthorizationMiddleware : AuthenticationHandler<ApiKeyAuthorizationMiddlewareOptions>
{
	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="options">Options</param>
	/// <param name="logger">Logger</param>
	/// <param name="urlEncoder">Url encoder</param>
	/// <param name="clock">Clock</param>
	public ApiKeyAuthorizationMiddleware(IOptionsMonitor<ApiKeyAuthorizationMiddlewareOptions> options,
		ILoggerFactory logger,
		UrlEncoder urlEncoder,
		ISystemClock clock) :
		base(options, logger, urlEncoder, clock)
	{

	}

	/// <inheritdoc />
	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		return Task.FromResult<AuthenticateResult>(AuthenticateResult.NoResult());
	}
}

/// <summary>
/// Options for ApiKeyAuthorizationMiddleware
/// </summary>
public class ApiKeyAuthorizationMiddlewareOptions : AuthenticationSchemeOptions
{
}
