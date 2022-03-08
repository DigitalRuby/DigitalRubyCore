namespace DigitalRuby.Core.Services.Authentication;

/// <summary>
/// Authentication repository interface
/// </summary>
public interface IAuthenticationRepository
{
    /// <summary>
    /// Get a user by id
    /// </summary>
    /// <param name="request">Get by id request</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>User or null if not found</returns>
    public Task<GetUserResponse?> GetUserByIdAsync(GetUserByIdRequest request, CancellationToken cancelToken = default);

    /// <summary>
    /// Get a user by email
    /// </summary>
    /// <param name="request">Get by email request</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>User or null if not found</returns>
    public Task<GetUserResponse?> GetUserByEmailAsync(GetUserByEmailRequest request, CancellationToken cancelToken = default);

    /// <summary>
    /// Get a refresh token
    /// </summary>
    /// <param name="request">Get refresh token request</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Refresh token response or null if not found</returns>
    public Task<GetRefreshTokenResponse?> GetRefreshTokenAsync(GetRefreshTokenRequest request, CancellationToken cancelToken = default);

    /// <summary>
    /// Add a refresh token
    /// </summary>
    /// <param name="request">Add refresh token request</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    public Task AddRefreshTokenAsync(AddRefreshTokenRequest request, CancellationToken cancelToken = default);

    /// <summary>
    /// Delete a refresh token
    /// </summary>
    /// <param name="request">Delete refresh token request</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    public Task RemoveRefreshTokenAsync(DeleteRefreshTokenRequest request, CancellationToken cancelToken = default);

    /// <summary>
    /// Remove expired refresh tokens if possible
    /// </summary>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    public Task RemoveExpiredRefreshTokensAsync(CancellationToken cancelToken = default) => Task.CompletedTask;
}

/// <summary>
/// Get user by id request
/// </summary>
/// <param name="Id">User id</param>
public record GetUserByIdRequest(string Id);

/// <summary>
/// Get user by email request
/// </summary>
/// <param name="Email">User email</param>
public record GetUserByEmailRequest(string Email);

/// <summary>
/// Get user by id response
/// </summary>
/// <param name="Id">User id</param>
/// <param name="Email">User email</param>
/// <param name="PasswordHash">User password hash</param>
public record GetUserResponse(string Id, string Email, byte[]? PasswordHash);

/// <summary>
/// Get a refresh token request
/// </summary>
/// <param name="UserId">User id</param>
/// <param name="CurrentRefreshToken">Current refresh token</param>
public record GetRefreshTokenRequest(string UserId, Guid CurrentRefreshToken);

/// <summary>
/// Delete a refresh token request
/// </summary>
/// <param name="UserId">User id</param>
/// <param name="CurrentRefreshToken">Current refresh token</param>
public record DeleteRefreshTokenRequest(string UserId, Guid CurrentRefreshToken);

/// <summary>
/// Remove a refresh token request
/// </summary>
/// <param name="UserId">User id</param>
/// <param name="CurrentRefreshToken">Current refresh token</param>
public record RemoveRefreshTokenrequest(string UserId, Guid CurrentRefreshToken);

/// <summary>
/// Get refresh token response
/// </summary>
/// <param name="UserId">User id</param>
/// <param name="CurrentRefreshToken">Current refresh token</param>
/// <param name="Expires">When the token expires</param>
/// <param name="DeviceInfo">Device info</param>
public record GetRefreshTokenResponse(string UserId, Guid CurrentRefreshToken, DateTimeOffset Expires, DeviceInfo? DeviceInfo);

/// <summary>
/// Refresh token
/// </summary>
/// <param name="UserId">User id</param>
/// <param name="Token">Token</param>
/// <param name="Expires">Expiration</param>
/// <param name="DeviceInfo">Device info</param>
public record AddRefreshTokenRequest(string UserId, Guid Token, DateTimeOffset Expires, DeviceInfo? DeviceInfo);
