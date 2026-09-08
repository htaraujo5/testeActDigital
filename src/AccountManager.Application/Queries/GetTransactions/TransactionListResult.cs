using AccountManager.Application.Interfaces.Persistence;

namespace AccountManager.Application.Queries.GetTransactions;

public sealed record TransactionListResult(
    Guid AccountId,
    IReadOnlyList<TransactionReadModel> Items,
    string Source);
