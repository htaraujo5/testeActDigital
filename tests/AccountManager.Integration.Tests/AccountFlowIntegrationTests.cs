using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace AccountManager.Integration.Tests;

[Collection("api")]
public sealed class AccountFlowIntegrationTests
{
    private readonly ApiFactoryFixture _fixture;

    public AccountFlowIntegrationTests(ApiFactoryFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Credit_debit_balance_idempotency_and_history_work()
    {
        using var client = _fixture.CreateClient();
        await _fixture.AuthenticateAsync(client);

        var accountId = Guid.NewGuid();
        var creditKey = Guid.NewGuid().ToString("N");

        var creditResponse = await SendMoneyAsync(client, HttpMethod.Post, $"/api/v1/accounts/{accountId}/credits", 100m, creditKey);
        creditResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var replayResponse = await SendMoneyAsync(client, HttpMethod.Post, $"/api/v1/accounts/{accountId}/credits", 100m, creditKey);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayBody = await replayResponse.Content.ReadFromJsonAsync<MutationDto>();
        replayBody!.IdempotentReplay.Should().BeTrue();

        var debitKey = Guid.NewGuid().ToString("N");
        var debitResponse = await SendMoneyAsync(client, HttpMethod.Post, $"/api/v1/accounts/{accountId}/debits", 40m, debitKey);
        debitResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var balance = await client.GetFromJsonAsync<BalanceDto>($"/api/v1/accounts/{accountId}/balance");
        balance!.Balance.Should().Be(60m);

        var history = await client.GetFromJsonAsync<TransactionListDto>($"/api/v1/accounts/{accountId}/transactions?take=10");
        history!.Items.Should().HaveCountGreaterThanOrEqualTo(2);

        var overdraft = await SendMoneyAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/accounts/{accountId}/debits",
            1000m,
            Guid.NewGuid().ToString("N"));
        overdraft.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var balanceAfterReject = await client.GetFromJsonAsync<BalanceDto>($"/api/v1/accounts/{accountId}/balance");
        balanceAfterReject!.Balance.Should().Be(60m);
    }

    [Fact]
    public async Task Credit_without_idempotency_key_returns_bad_request()
    {
        using var client = _fixture.CreateClient();
        await _fixture.AuthenticateAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/accounts/{Guid.NewGuid()}/credits",
            new { amount = 10m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Credit_with_invalid_amount_returns_bad_request()
    {
        using var client = _fixture.CreateClient();
        await _fixture.AuthenticateAsync(client);

        var response = await SendMoneyAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/accounts/{Guid.NewGuid()}/credits",
            0m,
            Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Idempotency_conflict_when_payload_differs()
    {
        using var client = _fixture.CreateClient();
        await _fixture.AuthenticateAsync(client);

        var accountId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("N");

        (await SendMoneyAsync(client, HttpMethod.Post, $"/api/v1/accounts/{accountId}/credits", 10m, key))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var conflict = await SendMoneyAsync(client, HttpMethod.Post, $"/api/v1/accounts/{accountId}/credits", 20m, key);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Health_endpoints_are_public()
    {
        using var client = _fixture.CreateClient();
        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> SendMoneyAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        decimal amount,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(new { amount })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }
}
