using AccountManager.Gateway.Clients;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace AccountManager.Gateway.Controllers;

public abstract class GatewayControllerBase : ControllerBase
{
    private readonly IMapper _mapper;

    protected GatewayControllerBase(IMapper mapper) => _mapper = mapper;

    protected IActionResult MapUpstream<TUpstream, TGateway>(UpstreamApiResponse<TUpstream> upstream)
    {
        if (!string.IsNullOrWhiteSpace(upstream.ApiInstance))
        {
            Response.Headers["X-Api-Instance"] = upstream.ApiInstance;
        }

        if (!string.IsNullOrWhiteSpace(upstream.CorrelationId))
        {
            Response.Headers["X-Correlation-Id"] = upstream.CorrelationId;
        }

        if (upstream.IsSuccessStatusCode && upstream.Body is not null)
        {
            var mapped = _mapper.Map<TGateway>(upstream.Body);
            return StatusCode(upstream.StatusCode, mapped);
        }

        return StatusCode(upstream.StatusCode, upstream.ErrorBody ?? new { error = "UPSTREAM_ERROR" });
    }
}
