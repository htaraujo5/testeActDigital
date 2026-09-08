using AccountManager.Application.Common;
using AccountManager.Application.Services;
using AccountManager.Domain.Enums;

namespace AccountManager.Application.Commands.CreditAccount;

public sealed class CreditAccountCommandHandler
    : ICommandHandler<CreditAccountCommand, ApplicationResult<MoneyMutationResult>>
{
    private readonly AccountMutationService _mutations;

    public CreditAccountCommandHandler(AccountMutationService mutations) => _mutations = mutations;

    public Task<ApplicationResult<MoneyMutationResult>> HandleAsync(
        CreditAccountCommand command,
        CancellationToken cancellationToken) =>
        _mutations.ExecuteAsync(
            command.AccountId,
            command.Amount,
            command.IdempotencyKey,
            command.Actor,
            command.SourceIp,
            command.UserAgent,
            command.CorrelationId,
            command.ApiInstanceId,
            LedgerEntryType.Credit,
            cancellationToken);
}
