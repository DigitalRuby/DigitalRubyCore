namespace DigitalRuby.Core.Services.Authentication;

/// <summary>
/// Login with password response
/// </summary>
public class LoginWithPasswordResponse : BaseResponse
{
	/// <summary>
	/// User id
	/// </summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// User name
	/// </summary>
	public string UserName { get; set; } = string.Empty;

	/// <summary>
	/// JWT token
	/// </summary>
	public string Token { get; set; } = string.Empty;

	/// <summary>
	/// A token which can refresh a new jwt token without having to provide credentials, these expire periodically so refresh calls need to be made to keep them up to date
	/// </summary>
	public Guid RefreshToken { get; set; }
}