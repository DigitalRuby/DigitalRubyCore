using DigitalRuby.Core.Cryptography;
using DigitalRuby.Core.Exceptions;
using DigitalRuby.Core.Services;

using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;

namespace DigitalRuby.Core.Tests.Services.Authentication;

/// <summary>
/// Tests for authentication service
/// </summary>
[TestFixture]
public class AuthenticationTests : IJwtTokenService, IAuthenticationRepository
{
	private readonly List<dynamic> users = new();
	private readonly List<AddRefreshTokenRequest> tokens = new();

	private const string userName = "bob@domain.info";
	private const string password = "test!23❤️";

	/// <inheritdoc />
	public string CreateToken(CreateTokenRequest request) => "token123";

	private Core.Services.Authentication.IAuthenticationService SetupTest()
	{
		var service = new Core.Services.Authentication.AuthenticationService(this, new Argon2Hasher_V2(null, null), this);
		var user = new ExpandoObject();
		users.Clear();
		tokens.Clear();
		user.TryAdd("Id", "1");
		user.TryAdd("Email", userName);
		user.TryAdd("FirstName", "Bob");
		user.TryAdd("LastName", "Cratchet");
		user.TryAdd("PasswordHash", service.CreatePasswordHash(password, "1"));
		users.Add(user);
		return service;
	}

	/// <summary>
	/// Test fail and success logins
	/// </summary>
	/// <returns>Task</returns>
	[Test]
	public async Task TestLogin()
	{
		var service = SetupTest();

		// login fails with no request params
		Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginWithPasswordAsync(new LoginWithPasswordRequest
		{

		}));

		// login fails with mismatch user id and correct password
		Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginWithPasswordAsync(new LoginWithPasswordRequest
		{
			Password = password,
			Email = userName + "a"
		}));

		// login fails with bad password and correct user id
		Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginWithPasswordAsync(new LoginWithPasswordRequest
		{
			Password = password + "wrong",
			Email = userName
		}));

		// login fails with ascii version of unicode password
		Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginWithPasswordAsync(new LoginWithPasswordRequest
		{
			Password = "test!23?",
			Email = userName
		}));

		// login success
		var result = await service.LoginWithPasswordAsync(new LoginWithPasswordRequest
		{
			Password = password,
			Email = userName
		});
		Assert.IsNotNull(result);
		Assert.AreEqual("1", result!.Id);
		Assert.AreEqual(userName, result.UserName);
		Assert.AreEqual("token123", result.Token);
		Assert.AreNotEqual(Guid.Empty, result.RefreshToken);
	}

	/// <summary>
	/// Test refresh tokens are working
	/// </summary>
	/// <returns>Task</returns>
	[Test]
	public async Task TestRefreshToken()
	{
		var service = SetupTest();

		// Guid.Empty fails
		Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginWithRefreshTokenAsync(new LoginWithRefreshTokenRequest
		{
			CurrentUserId = "1"
		}));

		// refresh token fails because there are none
		Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginWithRefreshTokenAsync(new LoginWithRefreshTokenRequest
		{
			CurrentUserId = "1",
			RefreshToken = Guid.NewGuid()
		}));

		// login to create a refersh token
		var loginResponse = await service.LoginWithPasswordAsync(new LoginWithPasswordRequest
		{
			Email = userName,
			Password = password
		});

		// wrong token should still fail
		Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginWithRefreshTokenAsync(new LoginWithRefreshTokenRequest
		{
			CurrentUserId = "1",
			RefreshToken = Guid.NewGuid()
		}));

		// refreshing token should not fail
		var refreshResponse = await service.LoginWithRefreshTokenAsync(new LoginWithRefreshTokenRequest
		{
			CurrentUserId = "1",
			RefreshToken = loginResponse.RefreshToken
		});

		// now old token should fail
		Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginWithRefreshTokenAsync(new LoginWithRefreshTokenRequest
		{
			CurrentUserId = "1",
			RefreshToken = loginResponse.RefreshToken
		}));

		// newly refresh token should succeed
		var refreshResponse2 = service.LoginWithRefreshTokenAsync(new LoginWithRefreshTokenRequest
		{
			CurrentUserId = "1",
			RefreshToken = refreshResponse.RefreshToken,
			CurrentDevice = new DeviceInfo
			{
				DeviceBrand = "iPhone",
				DeviceFamily = "phone",
				DeviceModel = "13 pro max",
				IPAddress = "99.99.99.99",
				OSFamily = "iOS",
				OSVersion = new(15, 1, 3)
			}
		});

		// should only be one refresh token
		Assert.AreEqual(1, tokens.Count);
		var token = tokens.First();
		Assert.IsNotNull(token.DeviceInfo);
		Assert.IsNotNull(token.DeviceInfo!.OSVersion);
		Assert.AreEqual("iPhone", token.DeviceInfo.DeviceBrand);
		Assert.AreEqual("phone", token.DeviceInfo.DeviceFamily);
		Assert.AreEqual("13 pro max", token.DeviceInfo.DeviceModel);
		Assert.AreEqual("iOS", token.DeviceInfo.OSFamily);
		Assert.AreEqual(15, token.DeviceInfo.OSVersion!.Value.Major);
		Assert.AreEqual(1, token.DeviceInfo.OSVersion.Value.Minor);
		Assert.AreEqual(3, token.DeviceInfo.OSVersion.Value.Patch);
	}

	/// <inheritdoc />
    public Task<GetUserResponse?> GetUserByIdAsync(GetUserByIdRequest request, CancellationToken cancelToken = default)
    {
		var obj = users.FirstOrDefault(u => u.Id == request.Id);
		if (obj is null)
		{
			return Task.FromResult<GetUserResponse?>(null);
		}
		return Task.FromResult<GetUserResponse?>(new GetUserResponse(obj.Id, obj.Email, obj.PasswordHash));
    }

	/// <inheritdoc />
	public Task<GetUserResponse?> GetUserByEmailAsync(GetUserByEmailRequest request, CancellationToken cancelToken = default)
    {
		var obj = users.FirstOrDefault(u => u.Email == request.Email);
		if (obj is null)
		{
			return Task.FromResult<GetUserResponse?>(null);
		}
		return Task.FromResult<GetUserResponse?>(new GetUserResponse(obj.Id, obj.Email, obj.PasswordHash));
	}

	/// <inheritdoc />
	public Task<GetRefreshTokenResponse?> GetRefreshTokenAsync(GetRefreshTokenRequest request, CancellationToken cancelToken = default)
    {
		var obj = tokens.FirstOrDefault(t => t.UserId == request.UserId && t.Token == request.CurrentRefreshToken);
		if (obj is null)
		{
			return Task.FromResult<GetRefreshTokenResponse?>(null);
		}
		return Task.FromResult<GetRefreshTokenResponse?>(new GetRefreshTokenResponse(obj.UserId, obj.Token, obj.Expires, obj.DeviceInfo));
    }

	/// <inheritdoc />
	public Task AddRefreshTokenAsync(AddRefreshTokenRequest request, CancellationToken cancelToken = default)
    {
		tokens.Add(request);
		return Task.CompletedTask;
    }

	/// <inheritdoc />
	public Task RemoveRefreshTokenAsync(DeleteRefreshTokenRequest request, CancellationToken cancelToken = default)
    {
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
			if (tokens[i].UserId == request.UserId && tokens[i].Token == request.CurrentRefreshToken)
			{
				tokens.RemoveAt(i);
			}
        }
		return Task.CompletedTask;
    }
}
