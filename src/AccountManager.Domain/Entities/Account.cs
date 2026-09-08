using AccountManager.Domain.Results;

namespace AccountManager.Domain.Entities;

public sealed class Account
{
    public Guid Id { get; }
    public decimal Balance { get; private set; }
    public long Version { get; private set; }

    public Account(Guid id, decimal balance = 0m, long version = 0)
    {
        if (balance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Balance cannot be negative.");
        }

        Id = id;
        Balance = balance;
        Version = version;
    }

    public DomainResult ApplyCredit(decimal amount)
    {
        if (amount <= 0)
        {
            return DomainResult.Fail(DomainErrorCodes.InvalidAmount, "Amount must be greater than zero.");
        }

        var before = Balance;
        Balance += amount;
        Version++;
        return DomainResult.Ok(before, Balance);
    }

    public DomainResult ApplyDebit(decimal amount)
    {
        if (amount <= 0)
        {
            return DomainResult.Fail(DomainErrorCodes.InvalidAmount, "Amount must be greater than zero.");
        }

        if (Balance < amount)
        {
            return DomainResult.Fail(
                DomainErrorCodes.InsufficientFunds,
                "Insufficient funds for debit.",
                Balance,
                Balance);
        }

        var before = Balance;
        Balance -= amount;
        Version++;
        return DomainResult.Ok(before, Balance);
    }
}
