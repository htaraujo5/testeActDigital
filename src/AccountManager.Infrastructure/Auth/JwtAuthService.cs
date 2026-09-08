using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AccountManager.Application.Interfaces.Auth;
using AccountManager.Infrastructure.Persistence.Write.Context;
using AccountManager.Infrastructure.Persistence.Write.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AccountManager.Infrastructure.Auth;

public sealed class JwtAuthService : IAuthService
{
    private readonly WriteDbContext _db;
    private readonly JwtOptions _options;
    private readonly PasswordHasher<UserModel> _passwordHasher = new();

    public JwtAuthService(WriteDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<TokenResult?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _db.Users.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Username == username.Trim() && x.IsActive,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password.Trim());
        if (verify == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var expires = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString("N")),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString("N")),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds;

        return new TokenResult(accessToken, "Bearer", expiresIn, user.Username, user.Role);
    }
}
