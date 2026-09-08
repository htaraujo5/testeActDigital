using AccountManager.Api.Contracts.Requests;
using AccountManager.Application.Interfaces.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountManager.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> TokenAsync([FromBody] LoginRequest body, CancellationToken cancellationToken)
    {
        var token = await _authService.AuthenticateAsync(body.Username, body.Password, cancellationToken);
        if (token is null)
        {
            return Unauthorized(new
            {
                error = "INVALID_CREDENTIALS",
                message = "Invalid username or password.",
                hint = "Use admin/Admin@123 or operator/Operator@123"
            });
        }

        return Ok(token);
    }
}
