using Microsoft.AspNetCore.Mvc;

namespace AccountManager.Gateway.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Liveness do BFF (processo no ar). Não verifica APIs upstream.</summary>
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "live", service = "AccountManager.Gateway" });
}
