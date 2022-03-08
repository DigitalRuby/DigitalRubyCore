namespace DigitalRuby.Core.Services.Authentication;

/// <summary>
/// Authentication service
/// </summary>
public interface IAuthenticationService
{
	/// <summary>
	/// Attempt to login with a password
	/// </summary>
	/// <param name="request">Login request</param>
	/// <returns>Login response</returns>
	Task<LoginWithPasswordResponse> LoginWithPasswordAsync(LoginWithPasswordRequest request);

	/// <summary>
	/// Attempt to login with a refresh token
	/// </summary>
	/// <param name="request">Request</param>
	/// <returns>Login with refresh token response or null if failure</returns>
	Task<LoginWithRefreshTokenResponse> LoginWithRefreshTokenAsync(LoginWithRefreshTokenRequest request);

	/// <summary>
	/// Create a password hash from a password and user id
	/// </summary>
	/// <param name="password">Password</param>
	/// <param name="userId">User id</param>
	/// <returns>Hashed password</returns>
	byte[] CreatePasswordHash(string password, string userId);
}

/// <inheritdoc />
[Binding(ServiceLifetime.Singleton)]
public class AuthenticationService : BackgroundService, IAuthenticationService
{
	private static readonly TimeSpan refreshTokenLifeTime = TimeSpan.FromDays(30.1);
	private static readonly TimeSpan removeExpiredTokensDelay = TimeSpan.FromHours(1.0);

	private readonly IJwtTokenService tokenService;
	private readonly ISecretHasher hasher;
	private readonly IAuthenticationRepository authenticationRepository;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="tokenService">Token service</param>
	/// <param name="hasher">Hasher</param>
	/// <param name="authenticationRepository">Authentication repository</param>
	public AuthenticationService(IJwtTokenService tokenService,
		ISecretHasher hasher,
		IAuthenticationRepository authenticationRepository)
	{
		this.tokenService = tokenService;
		this.hasher = hasher;
		this.authenticationRepository = authenticationRepository;
	}

	/// <inheritdoc />
	public async Task<LoginWithPasswordResponse> LoginWithPasswordAsync(LoginWithPasswordRequest request)
	{
		// note- request will not have a user id as this is an anonymous call

		// if request has a password and user name
		if (!string.IsNullOrWhiteSpace(request.Password) && !string.IsNullOrWhiteSpace(request.Email))
		{
			// attempt to grab the user
			GetUserResponse? foundUser = await authenticationRepository.GetUserByEmailAsync(new GetUserByEmailRequest(request.Email));

			// if we have a user with a password and an email address, grab them
			if (foundUser is not null && foundUser.PasswordHash is not null && !string.IsNullOrWhiteSpace(foundUser.Email))
			{
				string userId = foundUser.Id;
				var passwordHash = CreatePasswordHash(request.Password, userId);
				if (foundUser.PasswordHash.SequenceEqual(passwordHash))
				{
					var jwtToken = tokenService.CreateToken(new CreateTokenRequest(userId.ToString(CultureInfo.InvariantCulture), request.Email));
					var refreshToken = await CreateRefreshTokenAsync(userId, Guid.Empty, request.CurrentDevice);
					return new LoginWithPasswordResponse { Id = userId, UserName = foundUser.Email, Token = jwtToken, RefreshToken = refreshToken };
				}
			}
		}

		throw new UnauthorizedException("Login failed");
	}

	/// <inheritdoc />
	public async Task<LoginWithRefreshTokenResponse> LoginWithRefreshTokenAsync(LoginWithRefreshTokenRequest request)
	{
		if (!string.IsNullOrWhiteSpace(request.CurrentUserId) && request.RefreshToken != Guid.Empty)
		{
			// attempt to grab the user
			GetUserResponse? foundUser = await authenticationRepository.GetUserByIdAsync(new GetUserByIdRequest(request.CurrentUserId));

			// if we have a valid user
			if (foundUser is not null && !string.IsNullOrWhiteSpace(foundUser.Email))
			{
				var currentRefreshToken = request.RefreshToken;

				// attempt to rotate the refresh token
				var newRefreshToken = await CreateRefreshTokenAsync(foundUser.Id, currentRefreshToken, request.CurrentDevice);
				if (newRefreshToken != Guid.Empty)
				{
					// we have a new valid refresh token, create a new access token
					var jwtToken = tokenService.CreateToken(new CreateTokenRequest(foundUser.Id, foundUser.Email));
					return new LoginWithRefreshTokenResponse { Id = foundUser.Id, UserName = foundUser.Email, Token = jwtToken, RefreshToken = newRefreshToken };
				}
			}
		}

		throw new UnauthorizedException("Login with refresh token failed");
	}

	/// <inheritdoc />
	public byte[] CreatePasswordHash(string password, string userId)
	{
		return hasher.GetHash(Encoding.UTF8.GetBytes(password.Trim()), Encoding.ASCII.GetBytes(userId.ToString(CultureInfo.InvariantCulture)));
	}

	/// <inheritdoc />
	private async Task<Guid> CreateRefreshTokenAsync(string userId, Guid previousToken, DeviceInfo? device)
	{
		// if a previous token was provided, validate it
		if (previousToken != Guid.Empty)
		{
			GetRefreshTokenResponse? previousRefreshToken = await authenticationRepository.GetRefreshTokenAsync(new GetRefreshTokenRequest(userId, previousToken));

			if (previousRefreshToken is null)
			{
				// return failure, no token found
				return Guid.Empty;
			}

			// remove the token since we are refreshing
			await authenticationRepository.RemoveRefreshTokenAsync(new DeleteRefreshTokenRequest(userId, previousToken));
		}

		// create a new refresh token for this user
		var now = DateTimeOffset.UtcNow;
		var newToken = new AddRefreshTokenRequest(userId, Guid.NewGuid(), now + refreshTokenLifeTime, device);
		await authenticationRepository.AddRefreshTokenAsync(newToken);

		return newToken.Token;
	}

	/// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(removeExpiredTokensDelay, stoppingToken);
			await authenticationRepository.RemoveExpiredRefreshTokensAsync(stoppingToken);
		}
    }
}
