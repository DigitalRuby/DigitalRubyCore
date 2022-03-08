namespace DigitalRuby.Core.Services.Authentication;

/// <summary>
/// Login with password request
/// </summary>
public class LoginWithPasswordRequest : BaseRequest
{
	/// <summary>
	/// Email
	/// </summary>
	public string Email { get; set; } = string.Empty;

	/// <summary>
	/// Password
	/// </summary>
	public string Password { get; set; } = string.Empty;
}
