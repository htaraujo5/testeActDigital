using AccountManager.Application.Common;
using AccountManager.Application.Services;
using AccountManager.Domain.Enums;

namespace AccountManager.Application.Commands.DebitAccount;

public sealed class DebitAccountCommandHandler
    : ICommandHandler<DebitAccountCommand, ApplicationResult<MoneyMutationResult>>
{
    private readonly AccountMutationService _mutations;

    public DebitAccountCommandHandler(AccountMutationService mutations) => _mutations = mutations;

    public Task<ApplicationResult<MoneyMutationResult>> HandleAsync(
        DebitAccountCommand command,
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
            LedgerEntryType.Debit,
            cancellationToken);
}
