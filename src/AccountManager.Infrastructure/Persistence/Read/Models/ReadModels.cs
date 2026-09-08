namespace AccountManager.Infrastructure.Persistence.Read.Models;

public sealed class AccountBalanceReadModel
{
    public Guid AccountId { get; set; }
    public decimal Balance { get; set; }
    public DateTimeOffset AsOfUtc { get; set; }
    public long Version { get; set; }
}

public sealed class TransactionReadRow
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public decimal BalanceAfter { get; set; }
}
