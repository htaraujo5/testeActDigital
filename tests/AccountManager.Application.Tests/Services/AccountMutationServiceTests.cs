using AccountManager.Application.Common;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Interfaces.Resilience;
using AccountManager.Application.Services;
using AccountManager.Application.Tests.Fakes;
using AccountManager.Domain.Enums;
using AccountManager.Domain.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountManager.Application.Tests.Services;

public class AccountMutationServiceTests
{
    private readonly FakeWriteRepository _write = new();
    private readonly FakeCoordinationGate _gate = new();
    private readonly PassthroughCircuitBreaker _circuits = new();
    private readonly AccountMutationService _sut;

    public AccountMutationServiceTests()
    {
        _sut = new AccountMutationService(
            _write,
            _gate,
            _circuits,
            NullLogger<AccountMutationService>.Instance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task ExecuteAsync_rejects_invalid_amount(decimal amount)
    {
        var result = await Execute(amount: amount);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.InvalidAmount);
    }

    [Fact]
    public async Task ExecuteAsync_requires_idempotency_key()
    {
        var result = await Execute(idempotencyKey: " ");

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrorCodes.IdempotencyKeyRequired);
    }

    [Fact]
    public async Task ExecuteAsync_returns_503_when_write_circuit_is_open()
    {
        _circuits.Open(CircuitTarget.DbWrite);

        var result = await Execute();

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrorCodes.CircuitOpenDbWrite);
    }

    [Fact]
    public async Task ExecuteAsync_detects_idempotency_conflict_from_redis_reservation()
    {
        _gate.Reservation = new(false, true, null);

        var result = await Execute();

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrorCodes.IdempotencyConflict);
    }

    [Fact]
    public async Task ExecuteAsync_replays_cached_redis_payload()
    {
        var cached = """
            {"transactionId":"11111111-1111-1111-1111-111111111111","accountId":"22222222-2222-2222-2222-222222222222","operation":"credit","amount":50,"balance":50,"outcome":"success","reasonCode":null,"idempotentReplay":false}
            """;
        _gate.Reservation = new(false, false, cached);

        var result = await Execute();

        result.Succeeded.Should().BeTrue();
        result.Value!.IdempotentReplay.Should().BeTrue();
        result.Value.Balance.Should().Be(50);
    }

    [Fact]
    public async Task ExecuteAsync_credits_successfully_and_completes_idempotency()
    {
        _write.NextPersistResult = new MutationPersistResult(
            Guid.NewGuid(),
            true,
            "success",
            null,
            100,
            0,
            100);

        var result = await Execute(amount: 100);

        result.Succeeded.Should().BeTrue();
        result.Value!.Operation.Should().Be("credit");
        result.Value.Balance.Should().Be(100);
        result.Value.IdempotentReplay.Should().BeFalse();
        _gate.CompletedPayload.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_returns_domain_rejection_from_persist()
    {
        _write.NextPersistResult = new MutationPersistResult(
            Guid.NewGuid(),
            false,
            "rejected",
            DomainErrorCodes.InsufficientFunds,
            500,
            10,
            10);

        var result = await Execute(amount: 500, type: LedgerEntryType.Debit);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.InsufficientFunds);
        result.Value.Should().NotBeNull();
    }

    private Task<ApplicationResult<AccountManager.Application.Commands.MoneyMutationResult>> Execute(
        decimal amount = 10,
        string idempotencyKey = "key-1",
        LedgerEntryType type = LedgerEntryType.Credit) =>
        _sut.ExecuteAsync(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            amount,
            idempotencyKey,
            "tester",
            null,
            null,
            "corr",
            "api-test",
            type,
            CancellationToken.None);
}
