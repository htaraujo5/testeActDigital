using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AccountManager.Api.Contracts.Requests;
using AccountManager.Api.Mapping;
using AccountManager.Application.Commands;
using AccountManager.Application.Commands.CreditAccount;
using AccountManager.Application.Commands.DebitAccount;
using AccountManager.Application.Common;
using AccountManager.Application.Queries.GetBalance;
using AccountManager.Application.Queries.GetTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly ICommandHandler<CreditAccountCommand, ApplicationResult<MoneyMutationResult>> _creditHandler;
    private readonly ICommandHandler<DebitAccountCommand, ApplicationResult<MoneyMutationResult>> _debitHandler;
    private readonly IQueryHandler<GetBalanceQuery, ApplicationResult<BalanceResult>> _balanceHandler;
    private readonly IQueryHandler<GetTransactionsQuery, ApplicationResult<TransactionListResult>> _transactionsHandler;
    private readonly string _apiInstanceId;

    public AccountsController(
        ICommandHandler<CreditAccountCommand, ApplicationResult<MoneyMutationResult>> creditHandler,
        ICommandHandler<DebitAccountCommand, ApplicationResult<MoneyMutationResult>> debitHandler,
        IQueryHandler<GetBalanceQuery, ApplicationResult<BalanceResult>> balanceHandler,
        IQueryHandler<GetTransactionsQuery, ApplicationResult<TransactionListResult>> transactionsHandler,
        IConfiguration configuration)
    {
        _creditHandler = creditHandler;
        _debitHandler = debitHandler;
        _balanceHandler = balanceHandler;
        _transactionsHandler = transactionsHandler;
        _apiInstanceId = configuration["ApiInstanceId"] ?? Environment.MachineName;
    }

    [HttpPost("{accountId:guid}/credits")]
    public async Task<IActionResult> CreditAsync(
        Guid accountId,
        [FromBody] MoneyRequest body,
        CancellationToken cancellationToken)
    {
        var context = ResolveMutationContext(accountId, body.Amount);
        var command = new CreditAccountCommand(
            context.AccountId,
            context.Amount,
            context.IdempotencyKey,
            context.Actor,
            context.SourceIp,
            context.UserAgent,
            context.CorrelationId,
            context.ApiInstanceId);

        var result = await _creditHandler.HandleAsync(command, cancellationToken);
        return ApplicationResultMapper.ToMutationActionResult(result);
    }

    [HttpPost("{accountId:guid}/debits")]
    public async Task<IActionResult> DebitAsync(
        Guid accountId,
        [FromBody] MoneyRequest body,
        CancellationToken cancellationToken)
    {
        var context = ResolveMutationContext(accountId, body.Amount);
        var command = new DebitAccountCommand(
            context.AccountId,
            context.Amount,
            context.IdempotencyKey,
            context.Actor,
            context.SourceIp,
            context.UserAgent,
            context.CorrelationId,
            context.ApiInstanceId);

        var result = await _debitHandler.HandleAsync(command, cancellationToken);
        return ApplicationResultMapper.ToMutationActionResult(result);
    }

    [HttpGet("{accountId:guid}/balance")]
    public async Task<IActionResult> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var result = await _balanceHandler.HandleAsync(new GetBalanceQuery(accountId), cancellationToken);
        return ApplicationResultMapper.ToActionResult(result);
    }

    [HttpGet("{accountId:guid}/transactions")]
    public async Task<IActionResult> GetTransactionsAsync(
        Guid accountId,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        var result = await _transactionsHandler.HandleAsync(
            new GetTransactionsQuery(accountId, take ?? 50),
            cancellationToken);
        return ApplicationResultMapper.ToActionResult(result);
    }

    private MutationContext ResolveMutationContext(Guid accountId, decimal amount)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString()
            ?? Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        return new MutationContext(
            accountId,
            amount,
            idempotencyKey,
            ResolveActor(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            correlationId,
            _apiInstanceId);
    }

    private string ResolveActor()
    {
        return User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            ?? User.FindFirstValue("unique_name")
            ?? "anonymous";
    }

    private sealed record MutationContext(
        Guid AccountId,
        decimal Amount,
        string IdempotencyKey,
        string Actor,
        string? SourceIp,
        string? UserAgent,
        string CorrelationId,
        string ApiInstanceId);
}
