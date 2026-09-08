using AccountManager.Application.Commands.CreditAccount;
using AccountManager.Application.Commands.DebitAccount;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Services;
using AccountManager.Application.Tests.Fakes;
using AccountManager.Domain.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountManager.Application.Tests.Commands;

public class CommandHandlerTests
{
    [Fact]
    public async Task CreditAccountCommandHandler_delegates_to_mutation_service()
    {
        var write = new FakeWriteRepository
        {
            NextPersistResult = new MutationPersistResult(
                Guid.NewGuid(), true, "success", null, 25, 0, 25)
        };
        var service = new AccountMutationService(
            write,
            new FakeCoordinationGate(),
            new PassthroughCircuitBreaker(),
            NullLogger<AccountMutationService>.Instance);
        var handler = new CreditAccountCommandHandler(service);

        var result = await handler.HandleAsync(
            new CreditAccountCommand(Guid.NewGuid(), 25, "k1", "a", null, null, "c", "api"),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Operation.Should().Be("credit");
    }

    [Fact]
    public async Task DebitAccountCommandHandler_returns_insufficient_funds()
    {
        var write = new FakeWriteRepository
        {
            NextPersistResult = new MutationPersistResult(
                Guid.NewGuid(),
                false,
                "rejected",
                DomainErrorCodes.InsufficientFunds,
                99,
                1,
                1)
        };
        var service = new AccountMutationService(
            write,
            new FakeCoordinationGate(),
            new PassthroughCircuitBreaker(),
            NullLogger<AccountMutationService>.Instance);
        var handler = new DebitAccountCommandHandler(service);

        var result = await handler.HandleAsync(
            new DebitAccountCommand(Guid.NewGuid(), 99, "k2", "a", null, null, "c", "api"),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.InsufficientFunds);
    }
}
