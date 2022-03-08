using Microsoft.AspNetCore.Mvc.Filters;

namespace DigitalRuby.Core.Mvc
{
	/// <summary>
	/// Base controller for all api controllers
	/// </summary>
	[ApiController]
	[Authorize]
	[RateLimitHigh]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public abstract class BaseApiController : Controller
	{
		/// <summary>
		/// Logger
		/// </summary>
		public ILogger Logger { get; }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="logger">Logger</param>
		public BaseApiController(ILogger logger) { Logger = logger; }

		/// <inheritdoc />
		public override void OnActionExecuting(ActionExecutingContext context)
		{
			foreach (var kv in context.ActionArguments)
			{
				if (kv.Value is not null && kv.Value is BaseRequest request)
				{
					IdentifyRequest(request);
				}
			}
		}

		/// <summary>
		/// Identify a request. The request will include the user information making the request, if any
		/// </summary>
		/// <typeparam name="T">Type of request</typeparam>
		private void IdentifyRequest<T>(T request) where T : BaseRequest, new()
		{
			if (User.Identity is not null && User.Identity.IsAuthenticated)
			{
				var idClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
				if (!string.IsNullOrWhiteSpace(idClaim?.Value))
				{
					request.CurrentUserId = idClaim.Value;
				}
			}
			var info = UAParser.Parser.GetDefault().Parse(HttpContext.Request.Headers.UserAgent);
			if (info is not null)
			{
				int.TryParse(info.OS.Major, NumberStyles.None, CultureInfo.InvariantCulture, out int major);
				int.TryParse(info.OS.Minor, NumberStyles.None, CultureInfo.InvariantCulture, out int minor);
				int.TryParse(info.OS.Patch, NumberStyles.None, CultureInfo.InvariantCulture, out int patch);
				request.CurrentDevice = new()
				{
					OSFamily = info.OS.Family,
					OSVersion = new(major, minor, patch),
					DeviceFamily = info.Device.Family,
					DeviceModel = info.Device.Model,
					DeviceBrand = info.Device.Brand,
					IPAddress = HttpContext.GetRemoteIPAddress()?.ToString()
				};
			}
		}
	}
}
