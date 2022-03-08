using System.Security.Cryptography;

namespace DigitalRuby.Core.Services.Authentication;

/// <summary>
/// Assists with setting up jwt to services
/// </summary>
public static class JwtHelper
{
	/// <summary>
	/// Add jwt configuration
	/// </summary>
	/// <param name="services">Services</param>
	/// <param name="configuration">Configuration</param>
	public static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
	{
		AddJwtAuthentication(services,
			configuration["Jwt:PublicKey"],
			configuration["Jwt:PrivateKey"],
			configuration["Jwt:Audience"],
			configuration["Jwt:Issuer"]);
	}

	/// <summary>
	/// Add jwt authentication
	/// </summary>
	/// <param name="services">Services</param>
	/// <param name="publicKey">Public key</param>
	/// <param name="privateKey">Private key, can be null if only a public key for validation is needed</param>
	/// <param name="audience">Audience</param>
	/// <param name="issuer">Issuer</param>
	/// <param name="cookieName">Optional cookie name to read jwt token from</param>
	public static void AddJwtAuthentication(this IServiceCollection services,
		string publicKey,
		string? privateKey,
		string audience,
		string issuer,
		string? cookieName = null)
	{
		// https://vmsdurano.com/-net-core-3-1-signing-jwt-with-rsa/

		// Generate RSA keys:
		// ssh-keygen -t rsa -b 4096 -m PEM -f jwtRS256.key
		// openssl rsa -in jwtRS256.key -pubout -outform PEM -out jwtRS256.key.pub
		// cat jwtRS256.key
		// cat jwtRS256.key.pub

		if (File.Exists(privateKey))
		{
			privateKey = File.ReadAllText(privateKey);
		}
		if (File.Exists(publicKey))
		{
			publicKey = File.ReadAllText(publicKey);
		}

		SigningCredentials? credentialsPrivate = null;
		if (!string.IsNullOrWhiteSpace(privateKey))
		{
			RSA rsaPrivate = RSA.Create();
			rsaPrivate.ImportFromPem(privateKey);
			var securityKeyPrivate = new RsaSecurityKey(rsaPrivate);
			credentialsPrivate = new SigningCredentials(securityKeyPrivate, SecurityAlgorithms.RsaSha256);
			services.AddSingleton<IJwtTokenService>(new JwtTokenService(new TokenServiceOptions
			{
				Audience = audience,
				Issuer = issuer,
				Expiration = TimeSpan.FromDays(1.1),
				Credentials = credentialsPrivate
			}));
		}

		RSA rsaPublic = RSA.Create();
		rsaPublic.ImportFromPem(publicKey);
		var securityKeyPublic = new RsaSecurityKey(rsaPublic);

		services.AddAuthorization();
		services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opt =>
		{
			if (!string.IsNullOrWhiteSpace(cookieName))
			{
				opt.Events = new()
				{
					OnMessageReceived = context =>
					{
						var request = context.HttpContext.Request;
						var cookies = request.Cookies;
						if (cookies.TryGetValue(cookieName, out var accessTokenValue))
						{
							context.Token = accessTokenValue;
						}
						return Task.CompletedTask;
					}
				};
			}
			opt.TokenValidationParameters = new()
			{
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,
				ValidIssuer = issuer,
				ValidAudience = audience,
				IssuerSigningKey = securityKeyPublic
			};
		});
	}
}
