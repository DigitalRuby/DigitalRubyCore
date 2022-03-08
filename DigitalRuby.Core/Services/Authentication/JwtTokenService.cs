namespace DigitalRuby.Core.Services.Authentication;

/// <summary>
/// Create token request
/// </summary>
/// <param name="Id">User id</param>
/// <param name="UserName">User name</param>
public record CreateTokenRequest(string Id, string UserName);

/// <summary>
/// Service to get tokens
/// </summary>
public interface IJwtTokenService
{
	/// <summary>
	/// Create a jwt token
	/// </summary>
	/// <param name="request">Create token request</param>
	/// <returns>Token</returns>
	string CreateToken(CreateTokenRequest request);
}

/// <summary>
/// Token service options
/// </summary>
public class TokenServiceOptions
{
	/// <summary>
	/// Expiration
	/// </summary>
	public TimeSpan Expiration { get; set; }

	/// <summary>
	/// Issuer
	/// </summary>
	public string Issuer { get; set; } = string.Empty;

	/// <summary>
	/// Audience
	/// </summary>
	public string Audience { get; set; } = string.Empty;

	/// <summary>
	/// Signing credentials
	/// </summary>
	public Microsoft.IdentityModel.Tokens.SigningCredentials? Credentials { get; set; }
}

/// <inheritdoc />
public class JwtTokenService : IJwtTokenService
{
	private readonly TokenServiceOptions options;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="options">Options</param>
	public JwtTokenService(TokenServiceOptions options)
	{
		this.options = options;
	}

	/// <inheritdoc />
	public string CreateToken(CreateTokenRequest request)
	{
		// TODO: Implement refresh tokens per https://github.com/cornflourblue/dotnet-5-jwt-refresh-tokens-api

		var claims = new[]
		{
			new Claim(ClaimTypes.Name, request.Id),
			new Claim(ClaimTypes.NameIdentifier, request.UserName)
		};

		var tokenDescriptor = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(options.Issuer,
			options.Audience,
			claims,
			expires: DateTime.UtcNow.Add(options.Expiration),
			notBefore: DateTime.UtcNow.Subtract(TimeSpan.FromHours(2.0)),
			signingCredentials: options.Credentials);

		return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
	}
}
