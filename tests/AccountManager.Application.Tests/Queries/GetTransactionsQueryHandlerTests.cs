using AccountManager.Application.Common;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Interfaces.Resilience;
using AccountManager.Application.Queries.GetTransactions;
using AccountManager.Application.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountManager.Application.Tests.Queries;

public class GetTransactionsQueryHandlerTests
{
    private readonly FakeReadRepository _read = new();
    private readonly FakeWriteRepository _write = new();
    private readonly FakeCoordinationGate _gate = new();
    private readonly PassthroughCircuitBreaker _circuits = new();
    private readonly GetTransactionsQueryHandler _sut;

    public GetTransactionsQueryHandlerTests()
    {
        _sut = new GetTransactionsQueryHandler(
            _read,
            _write,
            _gate,
            _circuits,
            NullLogger<GetTransactionsQueryHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_returns_read_transactions_when_replica_healthy()
    {
        var accountId = Guid.NewGuid();
        _gate.ReplicaHealthy = true;
        _read.Transactions =
        [
            new TransactionReadModel(Guid.NewGuid(), accountId, "credit", 10, DateTimeOffset.UtcNow, 10)
        ];

        var result = await _sut.HandleAsync(new GetTransactionsQuery(accountId, 20), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Source.Should().Be("read");
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_clamps_take_and_uses_write_fallback()
    {
        var accountId = Guid.NewGuid();
        _gate.ReplicaHealthy = false;
        _write.SeedTransactions(
            new TransactionReadModel(Guid.NewGuid(), accountId, "debit", 5, DateTimeOffset.UtcNow, 5));

        var result = await _sut.HandleAsync(new GetTransactionsQuery(accountId, 500), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Source.Should().Be("write");
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_fails_when_write_circuit_open_after_read_failure()
    {
        _gate.ReplicaHealthy = false;
        _circuits.Open(CircuitTarget.DbWrite);

        var result = await _sut.HandleAsync(new GetTransactionsQuery(Guid.NewGuid(), 10), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrorCodes.CircuitOpen);
    }
}
