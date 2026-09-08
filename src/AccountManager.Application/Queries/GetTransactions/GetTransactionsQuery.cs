namespace AccountManager.Application.Queries.GetTransactions;

public sealed record GetTransactionsQuery(Guid AccountId, int Take);
