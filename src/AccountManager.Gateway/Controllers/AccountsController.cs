using AccountManager.Gateway.Clients;
using AccountManager.Gateway.Clients.Upstream;
using AccountManager.Gateway.Dtos.Requests;
using AccountManager.Gateway.Dtos.Responses;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace AccountManager.Gateway.Controllers;

[ApiController]
[Route("api/v1/accounts")]
public sealed class AccountsController : GatewayControllerBase
{
    private readonly IAccountApiClient _apiClient;

    public AccountsController(IAccountApiClient apiClient, IMapper mapper) : base(mapper)
    {
        _apiClient = apiClient;
    }

    [HttpPost("{accountId:guid}/credits")]
    public async Task<IActionResult> CreditAsync(
        Guid accountId,
        [FromBody] GatewayMoneyRequest request,
        CancellationToken cancellationToken)
    {
        var upstream = await _apiClient.CreditAsync(
            accountId,
            request,
            Request.Headers.Authorization.ToString(),
            Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty,
            ResolveCorrelationId(),
            cancellationToken);

        return MapUpstream<UpstreamMoneyResponse, GatewayMoneyResponse>(upstream);
    }

    [HttpPost("{accountId:guid}/debits")]
    public async Task<IActionResult> DebitAsync(
        Guid accountId,
        [FromBody] GatewayMoneyRequest request,
        CancellationToken cancellationToken)
    {
        var upstream = await _apiClient.DebitAsync(
            accountId,
            request,
            Request.Headers.Authorization.ToString(),
            Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty,
            ResolveCorrelationId(),
            cancellationToken);

        return MapUpstream<UpstreamMoneyResponse, GatewayMoneyResponse>(upstream);
    }

    [HttpGet("{accountId:guid}/balance")]
    public async Task<IActionResult> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var upstream = await _apiClient.GetBalanceAsync(
            accountId,
            Request.Headers.Authorization.ToString(),
            ResolveCorrelationId(),
            cancellationToken);

        return MapUpstream<UpstreamBalanceResponse, GatewayBalanceResponse>(upstream);
    }

    [HttpGet("{accountId:guid}/transactions")]
    public async Task<IActionResult> GetTransactionsAsync(
        Guid accountId,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        var upstream = await _apiClient.GetTransactionsAsync(
            accountId,
            take ?? 50,
            Request.Headers.Authorization.ToString(),
            ResolveCorrelationId(),
            cancellationToken);

        return MapUpstream<UpstreamTransactionListResponse, GatewayTransactionListResponse>(upstream);
    }

    private string ResolveCorrelationId() =>
        Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? HttpContext.Items["CorrelationId"]?.ToString()
        ?? Guid.NewGuid().ToString("N");
}
