using AccountManager.Application.Interfaces.Resilience;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountManager.Api.Controllers;

[ApiController]
[Route("meta")]
public sealed class MetaController : ControllerBase
{
    private readonly ICircuitBreakerRegistry _circuits;
    private readonly string _apiInstanceId;

    public MetaController(ICircuitBreakerRegistry circuits, IConfiguration configuration)
    {
        _circuits = circuits;
        _apiInstanceId = configuration["ApiInstanceId"] ?? Environment.MachineName;
    }

    [HttpGet("circuits")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult GetCircuits() => Ok(new
    {
        instance = _apiInstanceId,
        dbWrite = _circuits.GetState(CircuitTarget.DbWrite),
        dbRead = _circuits.GetState(CircuitTarget.DbRead),
        redis = _circuits.GetState(CircuitTarget.Redis)
    });

    [HttpGet("instance")]
    [AllowAnonymous]
    public IActionResult GetInstance() => Ok(new { instance = _apiInstanceId });
}
