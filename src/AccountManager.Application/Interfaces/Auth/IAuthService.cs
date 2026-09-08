namespace AccountManager.Application.Interfaces.Auth;

public interface IAuthService
{
    Task<TokenResult?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
}

public sealed record TokenResult(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string Username,
    string Role);
