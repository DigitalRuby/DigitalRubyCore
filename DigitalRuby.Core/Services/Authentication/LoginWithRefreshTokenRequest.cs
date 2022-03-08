namespace DigitalRuby.Core.Services.Authentication;

/// <summary>
/// Request to login with a refresh token
/// </summary>
public class LoginWithRefreshTokenRequest : BaseRequest
{
	/// <summary>
	/// The refresh token to log in with
	/// </summary>
	public Guid RefreshToken { get; set; }
}
