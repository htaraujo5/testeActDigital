using AccountManager.Application.Common;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Interfaces.Resilience;
using AccountManager.Application.Queries.GetBalance;
using AccountManager.Application.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountManager.Application.Tests.Queries;

public class GetBalanceQueryHandlerTests
{
    private readonly FakeReadRepository _read = new();
    private readonly FakeWriteRepository _write = new();
    private readonly FakeCoordinationGate _gate = new();
    private readonly PassthroughCircuitBreaker _circuits = new();
    private readonly GetBalanceQueryHandler _sut;

    public GetBalanceQueryHandlerTests()
    {
        _sut = new GetBalanceQueryHandler(
            _read,
            _write,
            _gate,
            _circuits,
            NullLogger<GetBalanceQueryHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_prefers_read_replica_when_healthy()
    {
        var accountId = Guid.NewGuid();
        _gate.ReplicaHealthy = true;
        _read.Balance = new BalanceReadModel(accountId, 120m, DateTimeOffset.UtcNow, 3);

        var result = await _sut.HandleAsync(new GetBalanceQuery(accountId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Balance.Should().Be(120m);
        result.Value.Source.Should().Be("read");
    }

    [Fact]
    public async Task HandleAsync_falls_back_to_write_when_replica_unhealthy()
    {
        var accountId = Guid.NewGuid();
        _gate.ReplicaHealthy = false;

        var result = await _sut.HandleAsync(new GetBalanceQuery(accountId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Balance.Should().Be(0m);
        result.Value.Source.Should().Be("write");
    }

    [Fact]
    public async Task HandleAsync_fails_when_both_paths_unavailable()
    {
        _gate.ReplicaHealthy = false;
        _circuits.Open(CircuitTarget.DbWrite);

        var result = await _sut.HandleAsync(new GetBalanceQuery(Guid.NewGuid()), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrorCodes.CircuitOpen);
    }

    [Fact]
    public async Task HandleAsync_falls_back_when_read_throws()
    {
        var accountId = Guid.NewGuid();
        _gate.ReplicaHealthy = true;
        _read.Exception = new InvalidOperationException("replica down");

        var result = await _sut.HandleAsync(new GetBalanceQuery(accountId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Source.Should().Be("write");
    }
}
