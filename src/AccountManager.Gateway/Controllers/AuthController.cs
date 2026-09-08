using AccountManager.Gateway.Clients;
using AccountManager.Gateway.Clients.Upstream;
using AccountManager.Gateway.Dtos.Requests;
using AccountManager.Gateway.Dtos.Responses;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace AccountManager.Gateway.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : GatewayControllerBase
{
    private readonly IAccountApiClient _apiClient;

    public AuthController(IAccountApiClient apiClient, IMapper mapper) : base(mapper)
    {
        _apiClient = apiClient;
    }

    [HttpPost("token")]
    public async Task<IActionResult> TokenAsync(
        [FromBody] GatewayLoginRequest request,
        CancellationToken cancellationToken)
    {
        var upstream = await _apiClient.AuthenticateAsync(request, cancellationToken);
        return MapUpstream<UpstreamTokenResponse, GatewayTokenResponse>(upstream);
    }
}
