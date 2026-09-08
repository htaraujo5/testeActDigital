using AccountManager.Domain.Entities;
using AccountManager.Domain.Results;
using FluentAssertions;

namespace AccountManager.Domain.Tests;

public class AccountTests
{
    [Fact]
    public void Credit_increases_balance()
    {
        var account = new Account(Guid.NewGuid(), 100m);
        var result = account.ApplyCredit(50m);

        result.Succeeded.Should().BeTrue();
        account.Balance.Should().Be(150m);
        result.BalanceBefore.Should().Be(100m);
        result.BalanceAfter.Should().Be(150m);
        account.Version.Should().Be(1);
    }

    [Fact]
    public void Debit_with_sufficient_funds_decreases_balance()
    {
        var account = new Account(Guid.NewGuid(), 100m);
        var result = account.ApplyDebit(40m);

        result.Succeeded.Should().BeTrue();
        account.Balance.Should().Be(60m);
        account.Version.Should().Be(1);
    }

    [Fact]
    public void Debit_with_insufficient_funds_is_rejected_and_balance_unchanged()
    {
        var account = new Account(Guid.NewGuid(), 30m);
        var result = account.ApplyDebit(50m);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.InsufficientFunds);
        account.Balance.Should().Be(30m);
        account.Version.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Invalid_amount_is_rejected(decimal amount)
    {
        var account = new Account(Guid.NewGuid(), 100m);

        account.ApplyCredit(amount).ErrorCode.Should().Be(DomainErrorCodes.InvalidAmount);
        account.ApplyDebit(amount).ErrorCode.Should().Be(DomainErrorCodes.InvalidAmount);
        account.Balance.Should().Be(100m);
    }

    [Fact]
    public void Constructor_rejects_negative_balance()
    {
        var act = () => new Account(Guid.NewGuid(), -1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Multiple_operations_bump_version()
    {
        var account = new Account(Guid.NewGuid(), 0m);
        account.ApplyCredit(10m);
        account.ApplyCredit(5m);
        account.ApplyDebit(3m);

        account.Balance.Should().Be(12m);
        account.Version.Should().Be(3);
    }
}
