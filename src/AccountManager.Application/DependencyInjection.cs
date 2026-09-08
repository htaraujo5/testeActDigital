using AccountManager.Application.Commands;
using AccountManager.Application.Commands.CreditAccount;
using AccountManager.Application.Commands.DebitAccount;
using AccountManager.Application.Common;
using AccountManager.Application.Queries.GetBalance;
using AccountManager.Application.Queries.GetTransactions;
using AccountManager.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AccountManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AccountMutationService>();

        services.AddScoped<ICommandHandler<CreditAccountCommand, ApplicationResult<MoneyMutationResult>>, CreditAccountCommandHandler>();
        services.AddScoped<ICommandHandler<DebitAccountCommand, ApplicationResult<MoneyMutationResult>>, DebitAccountCommandHandler>();
        services.AddScoped<IQueryHandler<GetBalanceQuery, ApplicationResult<BalanceResult>>, GetBalanceQueryHandler>();
        services.AddScoped<IQueryHandler<GetTransactionsQuery, ApplicationResult<TransactionListResult>>, GetTransactionsQueryHandler>();

        return services;
    }
}
